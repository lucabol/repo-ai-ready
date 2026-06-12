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
		catch (CopilotBackendException ex)
		{
			WriteCopilotBackendError(ex);
			return 6;
		}

		var judge = new AgentFrameworkRepoJudge(rubric, chatClient);
		RepositoryEvaluationBatch batch;

		try
		{
			batch = SupportsLiveProgress()
				? await EvaluateWithLiveProgressAsync(options.Repositories, evidenceSource, judge, options.MaxParallelism, cancellationToken)
				: await EvaluateWithLineProgressAsync(options.Repositories, evidenceSource, judge, options.MaxParallelism, cancellationToken);
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
			WriteCopilotBackendError(ex);
			return 6;
		}
		finally
		{
			chatClient.Dispose();
		}

		WriteFailureDiagnostics(batch.Failures);

		var writer = new ReportWriter();
		var run = await writer.WriteAsync(options.OutputDirectory, batch.Reports, batch.Failures, options.Format, cancellationToken);

		if (options.Format is ReportFormat.Console or ReportFormat.All)
		{
			new ConsoleReportRenderer().Render(batch.Reports, run);
		}

		return DetermineExitCode(batch, options.MinScore);
	}

	private static IChatClient CreateChatClient(AppOptions options) =>
		options.Backend switch
		{
			JudgeBackend.Copilot => CreateCopilotChatClient(options),
			JudgeBackend.OpenAi => OpenAiChatClientFactory.Create(options),
			JudgeBackend.Deterministic => new RuleBasedReadinessChatClient(new RuleBasedReadinessEvaluator()),
			_ => throw new UsageException($"Unsupported backend: {options.Backend}")
		};

	private static void WriteCopilotBackendError(CopilotBackendException ex)
	{
		Console.Error.WriteLine(ex.Message);
		if (ex.InnerException is not null)
		{
			Console.Error.WriteLine($"Underlying Copilot error: {ex.InnerException.Message}");
		}
	}

	private static IChatClient CreateCopilotChatClient(AppOptions options)
	{
		GitHubCopilotChatClient.EnsureBundledCopilotCliPlatformSupported();
		return new GitHubCopilotChatClient(options.CopilotToken, options.Model, Directory.GetCurrentDirectory());
	}

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

	private static async Task<RepositoryEvaluationBatch> EvaluateWithLiveProgressAsync(
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
		var batch = RepositoryEvaluationBatch.Empty;

		await AnsiConsole.Live(RenderProgressTable(progress, maxParallelism))
			.AutoClear(false)
			.StartAsync(async ctx =>
			{
				batch = await EvaluateRepositoriesBatchAsync(
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
					cancellationToken);
			});

		return batch;
	}

	private static Task<RepositoryEvaluationBatch> EvaluateWithLineProgressAsync(
		IReadOnlyList<RepositorySlug> repositories,
		IRepositoryEvidenceSource evidenceSource,
		AgentFrameworkRepoJudge judge,
		int maxParallelism,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"Evaluating {repositories.Count} repositories with up to {maxParallelism} parallel workers...");
		return EvaluateRepositoriesBatchAsync(
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
		var batch = await EvaluateRepositoriesBatchAsync(
			repositories,
			evidenceSource,
			judge,
			maxParallelism,
			updateStatus,
			cancellationToken);

		return batch.Reports;
	}

	internal static async Task<RepositoryEvaluationBatch> EvaluateRepositoriesBatchAsync(
		IReadOnlyList<RepositorySlug> repositories,
		IRepositoryEvidenceSource evidenceSource,
		AgentFrameworkRepoJudge judge,
		int maxParallelism,
		Action<int, RepositoryEvaluationStage>? updateStatus,
		CancellationToken cancellationToken)
	{
		var results = new AiReadinessReport?[repositories.Count];
		var failures = new RepositoryEvaluationFailure?[repositories.Count];
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
				var currentStage = RepositoryEvaluationStage.Pending;
				try
				{
					currentStage = RepositoryEvaluationStage.CollectingEvidence;
					updateStatus?.Invoke(index, currentStage);
					var evidence = await evidenceSource.CollectAsync(repo, ct);

					currentStage = RepositoryEvaluationStage.Judging;
					updateStatus?.Invoke(index, currentStage);
					results[index] = await judge.EvaluateAsync(evidence, ct);

					updateStatus?.Invoke(index, RepositoryEvaluationStage.Done);
				}
				catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
				{
					updateStatus?.Invoke(index, RepositoryEvaluationStage.Failed);
					failures[index] = CreateFailure(repo, currentStage, ex);
				}
			});

		return new RepositoryEvaluationBatch(
			results.Where(static report => report is not null).Select(static report => report!).ToList(),
			failures.Where(static failure => failure is not null).Select(static failure => failure!).ToList());
	}

	internal static int DetermineExitCode(RepositoryEvaluationBatch batch, int minScore)
	{
		if (batch.Failures.Count > 0)
		{
			var distinctFailureExitCodes = batch.Failures
				.Select(static failure => failure.ExitCode)
				.Distinct()
				.ToArray();
			return distinctFailureExitCodes.Length == 1 ? distinctFailureExitCodes[0] : 5;
		}

		var lowestScore = batch.Reports.Count == 0 ? 0 : batch.Reports.Min(static report => report.OverallScore);
		return lowestScore >= minScore ? 0 : 1;
	}

	private static RepositoryEvaluationFailure CreateFailure(RepositorySlug repo, RepositoryEvaluationStage stage, Exception exception) =>
		new(
			repo.FullName,
			StageText(stage),
			MapFailureExitCode(exception),
			exception.GetType().Name,
			FormatFailureMessage(exception));

	private static int MapFailureExitCode(Exception exception) =>
		exception switch
		{
			RateLimitExceededException => 3,
			NotFoundException => 4,
			ApiException => 5,
			CopilotBackendException => 6,
			_ => 5
		};

	private static string FormatFailureMessage(Exception exception)
	{
		var message = exception switch
		{
			RateLimitExceededException ex => $"GitHub API rate limit exceeded. Retry after {ex.GetRetryAfterTimeSpan()} or pass --github-token/--token with a PAT.",
			NotFoundException ex => $"GitHub repository or evidence path not found: {ex.Message}",
			ApiException ex => $"GitHub API request failed: {ex.Message}",
			CopilotBackendException ex when ex.InnerException is not null => $"{ex.Message} Underlying Copilot error: {ex.InnerException.Message}",
			_ => exception.Message
		};

		return string.IsNullOrWhiteSpace(message)
			? "Repository evaluation failed."
			: message.ReplaceLineEndings(" ").Trim();
	}

	private static void WriteFailureDiagnostics(IReadOnlyList<RepositoryEvaluationFailure> failures)
	{
		foreach (var failure in failures)
		{
			Console.Error.WriteLine($"{failure.Repo}: {failure.Stage} failed ({failure.ErrorType}, exit {failure.ExitCode}): {failure.Message}");
		}
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

internal sealed record RepositoryEvaluationBatch(
	IReadOnlyList<AiReadinessReport> Reports,
	IReadOnlyList<RepositoryEvaluationFailure> Failures)
{
	public static RepositoryEvaluationBatch Empty { get; } = new([], []);
}

internal enum RepositoryEvaluationStage
{
	Pending,
	CollectingEvidence,
	Judging,
	Done,
	Failed
}
