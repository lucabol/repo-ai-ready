namespace RepoAIReady.Cli;

public sealed record AppOptions(
	FileInfo JudgeFile,
	IReadOnlyList<RepositorySlug> Repositories,
	DirectoryInfo OutputDirectory,
	ReportFormat Format,
	JudgeBackend Backend,
	string? GitHubToken,
	string? CopilotToken,
	string? OpenAiKey,
	string? OpenAiEndpoint,
	string? Model,
	int MinScore,
	FileInfo? EnvFile)
{
	public const string Usage = """
		Usage:
		  repo-ai-ready <judge.md> <org/repo> [org/repo ...] [--output <dir>] [--format console|markdown|json|all] [--backend copilot|openai|deterministic] [--env-file <path>] [--github-token <token>] [--copilot-token <token>] [--openai-key <key>] [--openai-endpoint <uri>] [--model <model>] [--min-score <0-100>]
		  repo-ai-ready --help
		  repo-ai-ready --version

		Environment:
		  RepoAIReady loads .env from the current directory by default. Use --env-file <path> to load another file.
		  Supported keys: GITHUB_TOKEN, GH_TOKEN, COPILOT_TOKEN, GITHUB_COPILOT_TOKEN, OPENAI_API_KEY, OPENAI_ENDPOINT, REPOAI_MODEL.
		  GITHUB_TOKEN/GH_TOKEN are only used to collect repository evidence. By default, the Copilot backend uses your logged-in Copilot CLI/SDK account; use COPILOT_TOKEN only when you explicitly want token-based Copilot auth.

		Examples:
		  repo-ai-ready ai-readiness-llm-judge.md microsoft/vscode
		  repo-ai-ready --judge ai-readiness-llm-judge.md microsoft/vscode dotnet/runtime --format all --output reports
		  repo-ai-ready ai-readiness-llm-judge.md microsoft/vscode --backend deterministic
		""";

	public static AppOptions Parse(IReadOnlyList<string> args)
	{
		return Parse(args, AppEnvironment.Load(args, new DirectoryInfo(Directory.GetCurrentDirectory())));
	}

	public static AppOptions Parse(IReadOnlyList<string> args, IReadOnlyDictionary<string, string> environment)
	{
		if (args.Count == 0)
		{
			throw new UsageException("Missing required arguments.");
		}

		string? judgePath = null;
		var repos = new List<RepositorySlug>();
		var output = new DirectoryInfo(Directory.GetCurrentDirectory());
		var format = ReportFormat.All;
		var backend = JudgeBackend.Copilot;
		var envFile = default(FileInfo);
		var githubToken = environment.GetValue("GITHUB_TOKEN")
			?? environment.GetValue("GH_TOKEN");
		var copilotToken = environment.GetValue("COPILOT_TOKEN")
			?? environment.GetValue("GITHUB_COPILOT_TOKEN");
		var openAiKey = environment.GetValue("OPENAI_API_KEY");
		var openAiEndpoint = environment.GetValue("OPENAI_ENDPOINT");
		var model = environment.GetValue("REPOAI_MODEL");
		var minScore = 0;

		for (var i = 0; i < args.Count; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "--judge":
				case "-j":
					judgePath = ReadValue(args, ref i, arg);
					break;
				case "--output":
				case "-o":
					output = new DirectoryInfo(ReadValue(args, ref i, arg));
					break;
				case "--format":
				case "-f":
					format = ParseFormat(ReadValue(args, ref i, arg));
					break;
				case "--backend":
					backend = ParseBackend(ReadValue(args, ref i, arg));
					break;
				case "--env-file":
					envFile = new FileInfo(ReadValue(args, ref i, arg));
					break;
				case "--github-token":
				case "--token":
					githubToken = ReadValue(args, ref i, arg);
					break;
				case "--copilot-token":
					copilotToken = ReadValue(args, ref i, arg);
					break;
				case "--openai-key":
					openAiKey = ReadValue(args, ref i, arg);
					break;
				case "--openai-endpoint":
					openAiEndpoint = ReadValue(args, ref i, arg);
					break;
				case "--model":
					model = ReadValue(args, ref i, arg);
					break;
				case "--min-score":
					if (!int.TryParse(ReadValue(args, ref i, arg), out minScore) || minScore is < 0 or > 100)
					{
						throw new UsageException("--min-score must be an integer from 0 to 100.");
					}
					break;
				default:
					if (arg.StartsWith("-", StringComparison.Ordinal))
					{
						throw new UsageException($"Unknown option: {arg}");
					}

					if (judgePath is null)
					{
						judgePath = arg;
					}
					else
					{
						repos.Add(RepositorySlug.Parse(arg));
					}
					break;
			}
		}

		if (judgePath is null)
		{
			throw new UsageException("A judge Markdown file is required.");
		}

		if (repos.Count == 0)
		{
			throw new UsageException("At least one repository in org/repo form is required.");
		}

		return new AppOptions(new FileInfo(judgePath), repos, output, format, backend, githubToken, copilotToken, openAiKey, openAiEndpoint, model, minScore, envFile);
	}

	private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
	{
		if (++index >= args.Count)
		{
			throw new UsageException($"{option} requires a value.");
		}

		return args[index];
	}

	private static ReportFormat ParseFormat(string value) =>
		value.ToLowerInvariant() switch
		{
			"console" => ReportFormat.Console,
			"markdown" => ReportFormat.Markdown,
			"json" => ReportFormat.Json,
			"all" or "both" => ReportFormat.All,
			_ => throw new UsageException($"Unknown format: {value}")
		};

	private static JudgeBackend ParseBackend(string value) =>
		value.ToLowerInvariant() switch
		{
			"copilot" or "github-copilot" or "copilot-sdk" or "copilot-cli" => JudgeBackend.Copilot,
			"openai" or "open-ai" => JudgeBackend.OpenAi,
			"deterministic" or "offline" or "local" => JudgeBackend.Deterministic,
			_ => throw new UsageException($"Unknown backend: {value}")
		};
}

public sealed class UsageException(string message) : Exception(message);
