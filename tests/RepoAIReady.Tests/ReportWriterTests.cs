using RepoAIReady.Cli;
using RepoAIReady.Reporting;
using RepoAIReady.Rules;

namespace RepoAIReady.Tests;

public sealed class ReportWriterTests
{
	[Fact]
	public async Task WriteAsync_WritesPerRepoMarkdownJsonAndIndex()
	{
		var temp = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "repoaiready-tests", Guid.NewGuid().ToString("N")));
		var report = new RuleBasedReadinessEvaluator().Evaluate("rubric", SampleEvidence.VscodeLike());

		var run = await new ReportWriter().WriteAsync(temp, [report], ReportFormat.All, CancellationToken.None);

		Assert.True(File.Exists(run.IndexMarkdown.FullName));
		Assert.True(File.Exists(run.AggregateJson.FullName));
		Assert.True(File.Exists(Path.Combine(run.Directory.FullName, "repos", "microsoft-vscode", "report.md")));
		Assert.True(File.Exists(Path.Combine(run.Directory.FullName, "repos", "microsoft-vscode", "report.json")));
		Assert.Contains("microsoft/vscode", await File.ReadAllTextAsync(run.IndexMarkdown.FullName, CancellationToken.None));

		temp.Delete(recursive: true);
	}
}
