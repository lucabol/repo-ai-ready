using RepoAIReady.Cli;
using RepoAIReady.Reporting;
using RepoAIReady.Rules;

namespace RepoAIReady.Tests;

public sealed class ReportWriterTests
{
	[Theory]
	[InlineData(ReportFormat.Markdown, true, false)]
	[InlineData(ReportFormat.Json, false, true)]
	[InlineData(ReportFormat.Console, false, false)]
	[InlineData(ReportFormat.All, true, true)]
	public async Task WriteAsync_HonorsPerRepositoryFormatAndKeepsRunOutputsConsistent(
		ReportFormat format,
		bool expectedMarkdown,
		bool expectedJson)
	{
		var temp = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "repoaiready-tests", Guid.NewGuid().ToString("N")));
		var report = new RuleBasedReadinessEvaluator().Evaluate("rubric", SampleEvidence.VscodeLike());

		try
		{
			var run = await new ReportWriter().WriteAsync(temp, [report], format, CancellationToken.None);

			Assert.True(File.Exists(run.IndexMarkdown.FullName));
			Assert.True(File.Exists(run.AggregateJson.FullName));
			Assert.Equal(expectedMarkdown, File.Exists(Path.Combine(run.Directory.FullName, "repos", "microsoft-vscode", "report.md")));
			Assert.Equal(expectedJson, File.Exists(Path.Combine(run.Directory.FullName, "repos", "microsoft-vscode", "report.json")));

			var index = await File.ReadAllTextAsync(run.IndexMarkdown.FullName, CancellationToken.None);
			Assert.Contains("microsoft/vscode", index, StringComparison.Ordinal);
			Assert.Contains("Aggregate JSON: [aggregate-report.json](aggregate-report.json)", index, StringComparison.Ordinal);
			Assert.Equal(expectedMarkdown, index.Contains("[Markdown](repos/microsoft-vscode/report.md)", StringComparison.Ordinal));
			Assert.Equal(expectedJson, index.Contains("[JSON](repos/microsoft-vscode/report.json)", StringComparison.Ordinal));
			if (!expectedMarkdown && !expectedJson)
			{
				Assert.Contains("Not written", index, StringComparison.Ordinal);
			}
		}
		finally
		{
			if (temp.Exists)
			{
				temp.Delete(recursive: true);
			}
		}
	}

	[Fact]
	public async Task WriteAsync_EscapesIndexMarkdownTableContent()
	{
		var temp = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "repoaiready-tests", Guid.NewGuid().ToString("N")));
		var report = new AiReadinessReport(
			"owner/repo|name",
			"type\nwith\r\nbreaks|pipe",
			80,
			new(
				Score(16),
				Score(16),
				Score(16),
				Score(16),
				Score(16)),
			[],
			[],
			[]);

		try
		{
			var run = await new ReportWriter().WriteAsync(temp, [report], ReportFormat.All, CancellationToken.None);

			var index = await File.ReadAllTextAsync(run.IndexMarkdown.FullName, CancellationToken.None);
			Assert.Contains("`owner/repo\\|name`", index, StringComparison.Ordinal);
			Assert.Contains("`type<br>with<br>breaks\\|pipe`", index, StringComparison.Ordinal);
		}
		finally
		{
			if (temp.Exists)
			{
				temp.Delete(recursive: true);
			}
		}
	}

	[Fact]
	public async Task WriteAsync_IncludesRepositoryFailuresInIndexAndAggregate()
	{
		var temp = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "repoaiready-tests", Guid.NewGuid().ToString("N")));
		var report = new RuleBasedReadinessEvaluator().Evaluate("rubric", SampleEvidence.VscodeLike());
		var failure = new RepositoryEvaluationFailure(
			"example/broken",
			"Processing evidence...",
			5,
			nameof(InvalidOperationException),
			"boom while collecting evidence");

		try
		{
			var run = await new ReportWriter().WriteAsync(temp, [report], [failure], ReportFormat.Console, CancellationToken.None);

			var index = await File.ReadAllTextAsync(run.IndexMarkdown.FullName, CancellationToken.None);
			Assert.Contains("## Repository failures", index, StringComparison.Ordinal);
			Assert.Contains("example/broken", index, StringComparison.Ordinal);
			Assert.Contains("boom while collecting evidence", index, StringComparison.Ordinal);

			var aggregate = await File.ReadAllTextAsync(run.AggregateJson.FullName, CancellationToken.None);
			Assert.Contains("\"reports\"", aggregate, StringComparison.Ordinal);
			Assert.Contains("\"failures\"", aggregate, StringComparison.Ordinal);
			Assert.Contains("microsoft/vscode", aggregate, StringComparison.Ordinal);
			Assert.Contains("example/broken", aggregate, StringComparison.Ordinal);
		}
		finally
		{
			if (temp.Exists)
			{
				temp.Delete(recursive: true);
			}
		}
	}

	private static FundamentalScore Score(int score) =>
		new(score, [], []);
}
