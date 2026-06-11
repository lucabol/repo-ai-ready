using RepoAIReady.Reporting;
using Spectre.Console;

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

	[Fact]
	public void NormalizeDetail_PreservesFullTextWhileFlatteningLineEndings()
	{
		var detail = "Document the local setup commands." + Environment.NewLine + "Include generated-file guidance.";

		var normalized = ConsoleReportRenderer.NormalizeDetail(detail);

		Assert.Equal("Document the local setup commands. Include generated-file guidance.", normalized);
	}

	[Fact]
	public void Render_PreservesLongConsoleDetailsInNarrowConsoleInsteadOfTruncating()
	{
		var report = SampleReport(
			documentationGap: "No standalone CONTRIBUTING.md with full contribution workflow, local setup, coding conventions, review expectations, and generated-file guidance TAILNEXTACTION",
			topStrength: "Comprehensive README and docs give agents strong project purpose, language, setup, testing, release, and troubleshooting context TAILSTRENGTH",
			improvement: "Add enforced style validation with cargo fmt --check, cargo clippy, and documented local commands TAILIMPROVEMENT");

		var output = RenderToString(report);

		Assert.Contains("TAILNEXTACTION", output, StringComparison.Ordinal);
		Assert.Contains("TAILSTRENGTH", output, StringComparison.Ordinal);
		Assert.Contains("TAILIMPROVEMENT", output, StringComparison.Ordinal);
		Assert.DoesNotContain("…", output, StringComparison.Ordinal);
	}

	private static Rules.AiReadinessReport SampleReport() =>
		SampleReport("Add area READMEs.", "Documentation: README.md exists", "Testing: Add e2e tests.");

	private static Rules.AiReadinessReport SampleReport(string documentationGap, string topStrength, string improvement) =>
		new(
			"microsoft/vscode",
			"large_repo",
			75,
			new(
				Score(18, "README.md exists", documentationGap),
				Score(14, "Linting is configured", "Run type checking in CI."),
				Score(12, "Tests are present", "Add e2e tests."),
				Score(16, "CI workflows exist", "Pin tool versions."),
				Score(15, "Copilot instructions exist", "Add path-specific instructions.")),
			[topStrength],
			[improvement],
			[]);

	private static Rules.FundamentalScore Score(int score, string evidence, string gap) =>
		new(score, [evidence], [gap]);

	private static string RenderToString(Rules.AiReadinessReport report)
	{
		using var writer = new StringWriter();
		var console = AnsiConsole.Create(new AnsiConsoleSettings
		{
			Out = new AnsiConsoleOutput(writer),
			Interactive = InteractionSupport.No
		});
		console.Profile.Width = 100;

		var runDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "repoaiready-render-tests"));
		var run = new ReportRun(
			runDirectory,
			new FileInfo(Path.Combine(runDirectory.FullName, "index.md")),
			new FileInfo(Path.Combine(runDirectory.FullName, "aggregate-report.json")));

		new ConsoleReportRenderer(console).Render([report], run);
		return writer.ToString();
	}
}
