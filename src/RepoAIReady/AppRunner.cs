using System.Reflection;
using RepoAIReady.Agent;
using RepoAIReady.Cli;
using RepoAIReady.GitHub;
using RepoAIReady.Reporting;
using RepoAIReady.Rules;
using Microsoft.Extensions.AI;
using Octokit;
using Spectre.Console;

namespace RepoAIReady;

public static class AppRunner
{
	public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
	{
		if (IsHelpCommand(args))
		{
			Console.WriteLine(AppOptions.Usage);
			return 0;
		}

		if (IsVersionCommand(args))
		{
			Console.WriteLine(GetVersion());
			return 0;
		}

		AppOptions options;
		try
		{
			var environment = AppEnvironment.Load(args, new DirectoryInfo(Directory.GetCurrentDirectory()));
			options = AppOptions.Parse(args, environment);
		}
		catch (UsageException ex)
		{
			Console.Error.WriteLine(ex.Message);
			Console.Error.WriteLine(AppOptions.Usage);
			return 2;
		}

		if (!File.Exists(options.JudgeFile.FullName))
		{
			Console.Error.WriteLine($"Judge file not found: {options.JudgeFile.FullName}");
			return 2;
		}

		var rubric = await File.ReadAllTextAsync(options.JudgeFile.FullName, cancellationToken);
		var evidenceSource = new GitHubRepositoryEvidenceSource(options.GitHubToken);
		IChatClient chatClient;
		try
		{
			chatClient = CreateChatClient(options);
		}
		catch (UsageException ex)
		{
			Console.Error.WriteLine(ex.Message);
			Console.Error.WriteLine(AppOptions.Usage);
			return 2;
		}

		var judge = new AgentFrameworkRepoJudge(rubric, chatClient);
		var results = new List<AiReadinessReport>();

		try
		{
			var evaluated = SupportsLiveProgress()
				? await EvaluateWithLiveProgressAsync(options.Repositories, evidenceSource, judge, options.MaxParallelism, cancellationToken)
				: await EvaluateWithLineProgressAsync(options.Repositories, evidenceSource, judge, options.MaxParallelism, cancellationToken);

			results.AddRange(evaluated);
		}
		catch (RateLimitExceededException ex)
		{
			Console.Error.WriteLine($"GitHub API rate limit exceeded. Retry after {ex.GetRetryAfterTimeSpan()} or pass --github-token/--token with a PAT.");
			return 3;
		}
		catch (NotFoundException ex)
		{
			Console.Error.WriteLine($"GitHub repository or evidence path not found: {ex.Message}");
			return 4;
		}
		catch (ApiException ex)
		{
			Console.Error.WriteLine($"GitHub API request failed: {ex.Message}");
			return 5;
		}
		catch (CopilotBackendException ex)
		{
			Console.Error.WriteLine(ex.Message);
			if (ex.InnerException is not null)
			{
				Console.Error.WriteLine($"Underlying Copilot error: {ex.InnerException.Message}");
			}

			return 6;
		}
		finally
		{
			chatClient.Dispose();
		}

		var writer = new ReportWriter();
		var run = await writer.WriteAsync(options.OutputDirectory, results, options.Format, cancellationToken);

		if (options.Format is ReportFormat.Console or ReportFormat.All)
		{
			new ConsoleReportRenderer().Render(results, run);
		}

		var lowestScore = results.Count == 0 ? 0 : results.Min(r => r.OverallScore);
		return lowestScore >= options.MinScore ? 0 : 1;
	}

	private static IChatClient CreateChatClient(AppOptions options) =>
		options.Backend switch
		{
			JudgeBackend.Copilot => new GitHubCopilotChatClient(options.CopilotToken, options.Model, Directory.GetCurrentDirectory()),
			JudgeBackend.OpenAi => OpenAiChatClientFactory.Create(options),
			JudgeBackend.Deterministic => new RuleBasedReadinessChatClient(new RuleBasedReadinessEvaluator()),
			_ => throw new UsageException($"Unsupported backend: {options.Backend}")
		};

	private static bool IsHelpCommand(IReadOnlyList<string> args) =>
		args.Count == 1 && args[0] is "--help" or "-h" or "-?";

	private static bool IsVersionCommand(IReadOnlyList<string> args) =>
		args.Count == 1 && args[0] is "--version" or "-v";

	private static string GetVersion() =>
		typeof(AppRunner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? typeof(AppRunner).Assembly.GetName().Version?.ToString()
		?? "unknown";

	internal static Table RenderProgressTable(IReadOnlyList<RepositoryEvaluationProgress> progress, int maxParallelism)
	{
		var table = new Table()
			.Border(TableBorder.Rounded)
			.Title($"[bold cyan]Evaluating repositories[/] [dim](parallelism: {maxParallelism})[/]")
			.AddColumn("Repository")
			.AddColumn("State");

		foreach (var item in progress)
		{
			table.AddRow(Markup.Escape(item.Repository), StageMarkup(item.Stage));
		}

		return table;
	}

	private static async Task<IReadOnlyList<AiReadinessReport>> EvaluateWithLiveProgressAsync(
		IReadOnlyList<RepositorySlug> repositories,
		IRepositoryEvidenceSource evidenceSource,
		AgentFrameworkRepoJudge judge,
		int maxParallelism,
		CancellationToken cancellationToken)
	{
		var progress = repositories
			.Select(static repo => new RepositoryEvaluationProgress(repo.FullName, RepositoryEvaluationStage.Pending))
			.ToArray();
		var progressLock = new object();
		var results = Array.Empty<AiReadinessReport>();

		await AnsiConsole.Live(RenderProgressTable(progress, maxParallelism))
			.AutoClear(false)
			.StartAsync(async ctx =>
			{
				results = [.. await EvaluateRepositoriesAsync(
					repositories,
					evidenceSource,
					judge,
					maxParallelism,
					(index, stage) =>
					{
						lock (progressLock)
						{
							progress[index] = progress[index] with { Stage = stage };
							ctx.UpdateTarget(RenderProgressTable(progress, maxParallelism));
							ctx.Refresh();
						}
					},
					cancellationToken)];
			});

		return results;
	}

	private static Task<IReadOnlyList<AiReadinessReport>> EvaluateWithLineProgressAsync(
		IReadOnlyList<RepositorySlug> repositories,
		IRepositoryEvidenceSource evidenceSource,
		AgentFrameworkRepoJudge judge,
		int maxParallelism,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"Evaluating {repositories.Count} repositories with up to {maxParallelism} parallel workers...");
		return EvaluateRepositoriesAsync(
			repositories,
			evidenceSource,
			judge,
			maxParallelism,
			(index, stage) => Console.WriteLine($"{repositories[index].FullName}: {StageText(stage)}"),
			cancellationToken);
	}

	internal static async Task<IReadOnlyList<AiReadinessReport>> EvaluateRepositoriesAsync(
		IReadOnlyList<RepositorySlug> repositories,
		IRepositoryEvidenceSource evidenceSource,
		AgentFrameworkRepoJudge judge,
		int maxParallelism,
		Action<int, RepositoryEvaluationStage>? updateStatus,
		CancellationToken cancellationToken)
	{
		var results = new AiReadinessReport?[repositories.Count];
		await Parallel.ForEachAsync(
			Enumerable.Range(0, repositories.Count),
			new ParallelOptions
			{
				MaxDegreeOfParallelism = maxParallelism,
				CancellationToken = cancellationToken
			},
			async (index, ct) =>
			{
				var repo = repositories[index];
				try
				{
					updateStatus?.Invoke(index, RepositoryEvaluationStage.CollectingEvidence);
					var evidence = await evidenceSource.CollectAsync(repo, ct);

					updateStatus?.Invoke(index, RepositoryEvaluationStage.Judging);
					results[index] = await judge.EvaluateAsync(evidence, ct);

					updateStatus?.Invoke(index, RepositoryEvaluationStage.Done);
				}
				catch
				{
					updateStatus?.Invoke(index, RepositoryEvaluationStage.Failed);
					throw;
				}
			});

		return results.Select(report => report ?? throw new InvalidOperationException("Repository evaluation did not produce a report.")).ToList();
	}

	internal static string StageMarkup(RepositoryEvaluationStage stage) =>
		stage switch
		{
			RepositoryEvaluationStage.Pending => "[grey]Pending[/]",
			RepositoryEvaluationStage.CollectingEvidence => "[yellow]Processing evidence...[/]",
			RepositoryEvaluationStage.Judging => "[blue]Judging...[/]",
			RepositoryEvaluationStage.Done => "[green]Done[/]",
			RepositoryEvaluationStage.Failed => "[red]Failed[/]",
			_ => "[grey]Unknown[/]"
		};

	internal static string StageText(RepositoryEvaluationStage stage) =>
		stage switch
		{
			RepositoryEvaluationStage.Pending => "Pending",
			RepositoryEvaluationStage.CollectingEvidence => "Processing evidence...",
			RepositoryEvaluationStage.Judging => "Judging...",
			RepositoryEvaluationStage.Done => "Done",
			RepositoryEvaluationStage.Failed => "Failed",
			_ => "Unknown"
		};

	internal static bool SupportsLiveProgress() => !Console.IsOutputRedirected;
}

internal sealed record RepositoryEvaluationProgress(string Repository, RepositoryEvaluationStage Stage);

internal enum RepositoryEvaluationStage
{
	Pending,
	CollectingEvidence,
	Judging,
	Done,
	Failed
}
