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
			await AnsiConsole.Status()
				.Spinner(Spinner.Known.Dots)
				.StartAsync("Evaluating repositories...", async ctx =>
				{
					foreach (var repo in options.Repositories)
					{
						ctx.Status($"Collecting evidence for [cyan]{Markup.Escape(repo.FullName)}[/]...");
						var evidence = await evidenceSource.CollectAsync(repo, cancellationToken);

						ctx.Status($"Judging [cyan]{Markup.Escape(repo.FullName)}[/] with Microsoft Agent Framework using [cyan]{Markup.Escape(options.Backend.ToString())}[/]...");
						results.Add(await judge.EvaluateAsync(evidence, cancellationToken));
					}
				});
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
}
