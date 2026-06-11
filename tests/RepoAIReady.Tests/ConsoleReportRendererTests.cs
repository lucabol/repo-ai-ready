using RepoAIReady.Reporting;

namespace RepoAIReady.Tests;

public sealed class ConsoleReportRendererTests
{
	[Fact]
	public void Areas_ReturnsAllJudgedFundamentals()
	{
		var areas = ConsoleReportRenderer.Areas(SampleReport());

		Assert.Equal(
			["Documentation", "Style/validation", "Testing", "Build infra", "AI context"],
			areas.Select(area => area.Name).ToArray());
	}

	[Fact]
	public void PrioritizedRemediationActions_DeduplicatesSameActionAcrossSources()
	{
		var actions = ConsoleReportRenderer.PrioritizedRemediationActions(SampleReport());

		Assert.Single(actions, action => action.Contains("Add e2e tests.", StringComparison.OrdinalIgnoreCase));
	}

	private static Rules.AiReadinessReport SampleReport() =>
		new(
			"microsoft/vscode",
			"large_repo",
			75,
			new(
				Score(18, "README.md exists", "Add area READMEs."),
				Score(14, "Linting is configured", "Run type checking in CI."),
				Score(12, "Tests are present", "Add e2e tests."),
				Score(16, "CI workflows exist", "Pin tool versions."),
				Score(15, "Copilot instructions exist", "Add path-specific instructions.")),
			["Documentation: README.md exists"],
			["Testing: Add e2e tests."],
			[]);

	private static Rules.FundamentalScore Score(int score, string evidence, string gap) =>
		new(score, [evidence], [gap]);
}
