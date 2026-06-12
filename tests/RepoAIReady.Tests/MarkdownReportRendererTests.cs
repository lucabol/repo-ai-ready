using RepoAIReady.Reporting;
using RepoAIReady.Rules;

namespace RepoAIReady.Tests;

public sealed class MarkdownReportRendererTests
{
	[Fact]
	public void Render_EscapesPipesAndAllLineEndingsInTablesAndLists()
	{
		var report = new AiReadinessReport(
			"owner/repo",
			"library",
			80,
			new(
				Score("Evidence with bare\nline and pipe |", "Gap with cr\ronly and crlf\r\nline"),
				Score("Linting is configured", "Run type checking in CI."),
				Score("Tests are present", "Add e2e tests."),
				Score("CI workflows exist", "Pin tool versions."),
				Score("Copilot instructions exist", "Add path-specific instructions.")),
			["Strength line\ncontinues | pipe"],
			["Improve\rthis\r\narea | now"],
			["Unclear\nsignal | value"]);

		var markdown = MarkdownReportRenderer.Render(report);

		Assert.Contains("Evidence with bare<br>line and pipe \\|", markdown, StringComparison.Ordinal);
		Assert.Contains("Gap with cr<br>only and crlf<br>line", markdown, StringComparison.Ordinal);
		Assert.Contains("1. Strength line<br>continues \\| pipe", markdown, StringComparison.Ordinal);
		Assert.Contains("1. Improve<br>this<br>area \\| now", markdown, StringComparison.Ordinal);
		Assert.Contains("1. Unclear<br>signal \\| value", markdown, StringComparison.Ordinal);
		Assert.DoesNotContain("bare\nline", markdown, StringComparison.Ordinal);
		Assert.DoesNotContain("cr\ronly", markdown, StringComparison.Ordinal);
		Assert.DoesNotContain("crlf\r\nline", markdown, StringComparison.Ordinal);
	}

	private static FundamentalScore Score(string evidence, string gap) =>
		new(16, [evidence], [gap]);
}
