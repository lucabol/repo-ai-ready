using RepoAIReady.Cli;

namespace RepoAIReady.Tests;

public sealed class AppOptionsTests
{
	[Fact]
	public void Parse_AcceptsJudgeFileAndMultipleRepos()
	{
		var options = AppOptions.Parse([
			"--judge-file", "custom-judge.md",
			"microsoft/vscode",
			"dotnet/runtime",
			"--output", "out",
			"--format", "all",
			"--min-score", "75"
		], new Dictionary<string, string>());

		Assert.Equal("custom-judge.md", options.JudgeFile.Name);
		Assert.Equal(["microsoft/vscode", "dotnet/runtime"], options.Repositories.Select(r => r.FullName).ToArray());
		Assert.Equal(ReportFormat.All, options.Format);
		Assert.Equal(JudgeBackend.Copilot, options.Backend);
		Assert.Equal(75, options.MinScore);
		Assert.Equal(4, options.MaxParallelism);
	}

	[Fact]
	public void Parse_UsesDefaultJudgeFileWhenJudgeFileOmitted()
	{
		var options = AppOptions.Parse(["microsoft/vscode"], new Dictionary<string, string>());

		Assert.Equal(AppOptions.DefaultJudgeFile.FullName, options.JudgeFile.FullName);
		Assert.Equal(["microsoft/vscode"], options.Repositories.Select(r => r.FullName).ToArray());
	}

	[Theory]
	[InlineData("--judge")]
	[InlineData("-j")]
	public void Parse_AcceptsLegacyJudgeFileAliases(string option)
	{
		var options = AppOptions.Parse([option, "legacy-judge.md", "microsoft/vscode"], new Dictionary<string, string>());

		Assert.Equal("legacy-judge.md", options.JudgeFile.Name);
		Assert.Equal(["microsoft/vscode"], options.Repositories.Select(r => r.FullName).ToArray());
	}

	[Fact]
	public void Parse_AcceptsLegacyPositionalJudgeFile()
	{
		var options = AppOptions.Parse(["legacy-judge.md", "microsoft/vscode"], new Dictionary<string, string>());

		Assert.Equal("legacy-judge.md", options.JudgeFile.Name);
		Assert.Equal(["microsoft/vscode"], options.Repositories.Select(r => r.FullName).ToArray());
	}

	[Fact]
	public void Parse_LoadsGitHubTokenFromEnvironmentValues()
	{
		var options = AppOptions.Parse(
			["judge.md", "microsoft/vscode"],
			new Dictionary<string, string> { ["GITHUB_TOKEN"] = "from-env-file" });

		Assert.Equal("from-env-file", options.GitHubToken);
	}

	[Fact]
	public void Parse_CommandLineTokenOverridesEnvironmentValues()
	{
		var options = AppOptions.Parse(
			["judge.md", "microsoft/vscode", "--github-token", "from-cli"],
			new Dictionary<string, string> { ["GITHUB_TOKEN"] = "from-env-file" });

		Assert.Equal("from-cli", options.GitHubToken);
	}

	[Fact]
	public void Parse_DoesNotUseGitHubTokenAsCopilotToken()
	{
		var options = AppOptions.Parse(
			["judge.md", "microsoft/vscode"],
			new Dictionary<string, string> { ["GITHUB_TOKEN"] = "repo-read-token" });

		Assert.Equal("repo-read-token", options.GitHubToken);
		Assert.Null(options.CopilotToken);
	}

	[Fact]
	public void Parse_LoadsCopilotTokenSeparately()
	{
		var options = AppOptions.Parse(
			["judge.md", "microsoft/vscode"],
			new Dictionary<string, string>
			{
				["GITHUB_TOKEN"] = "repo-read-token",
				["COPILOT_TOKEN"] = "copilot-token"
			});

		Assert.Equal("repo-read-token", options.GitHubToken);
		Assert.Equal("copilot-token", options.CopilotToken);
	}

	[Fact]
	public void Parse_AcceptsEnvFileOption()
	{
		var options = AppOptions.Parse(["judge.md", "microsoft/vscode", "--env-file", "secrets.env"], new Dictionary<string, string>());

		Assert.Equal("secrets.env", options.EnvFile?.Name);
	}

	[Fact]
	public void Parse_AcceptsParallelismOption()
	{
		var options = AppOptions.Parse(["judge.md", "microsoft/vscode", "--parallelism", "8"], new Dictionary<string, string>());

		Assert.Equal(8, options.MaxParallelism);
	}

	[Fact]
	public void Parse_LoadsParallelismFromEnvironment()
	{
		var options = AppOptions.Parse(
			["judge.md", "microsoft/vscode"],
			new Dictionary<string, string> { ["REPOAI_PARALLELISM"] = "3" });

		Assert.Equal(3, options.MaxParallelism);
	}

	[Fact]
	public void Parse_RejectsInvalidParallelism()
	{
		var ex = Assert.Throws<UsageException>(() => AppOptions.Parse(["judge.md", "microsoft/vscode", "--parallelism", "0"], new Dictionary<string, string>()));

		Assert.Contains("1 to 16", ex.Message);
	}

	[Fact]
	public void Parse_AcceptsExplicitBackends()
	{
		var deterministic = AppOptions.Parse(["judge.md", "microsoft/vscode", "--backend", "deterministic"]);
		var openAi = AppOptions.Parse(["judge.md", "microsoft/vscode", "--backend", "openai"]);

		Assert.Equal(JudgeBackend.Deterministic, deterministic.Backend);
		Assert.Equal(JudgeBackend.OpenAi, openAi.Backend);
	}

	[Fact]
	public void Parse_RejectsMalformedRepo()
	{
		var ex = Assert.Throws<UsageException>(() => AppOptions.Parse(["judge.md", "not-a-repo"]));
		Assert.Contains("org/repo", ex.Message);
	}
}
