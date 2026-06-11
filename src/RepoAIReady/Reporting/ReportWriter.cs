using System.Text.Json;
using RepoAIReady.Cli;
using RepoAIReady.Rules;

namespace RepoAIReady.Reporting;

public sealed record ReportRun(DirectoryInfo Directory, FileInfo IndexMarkdown, FileInfo AggregateJson);

public sealed class ReportWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	public async Task<ReportRun> WriteAsync(
		DirectoryInfo outputDirectory,
		IReadOnlyList<AiReadinessReport> reports,
		ReportFormat format,
		CancellationToken cancellationToken)
	{
		var runDirectory = new DirectoryInfo(Path.Combine(outputDirectory.FullName, $"reports-{DateTime.UtcNow:yyyyMMdd-HHmmss}"));
		runDirectory.Create();
		var reposDirectory = Directory.CreateDirectory(Path.Combine(runDirectory.FullName, "repos"));

		foreach (var report in reports)
		{
			var repoDirectory = Directory.CreateDirectory(Path.Combine(reposDirectory.FullName, SanitizeRepo(report.Repo)));
			if (format is ReportFormat.Markdown or ReportFormat.All)
			{
				await File.WriteAllTextAsync(Path.Combine(repoDirectory.FullName, "report.md"), MarkdownReportRenderer.Render(report), cancellationToken);
			}

			if (format is ReportFormat.Json or ReportFormat.All)
			{
				await File.WriteAllTextAsync(Path.Combine(repoDirectory.FullName, "report.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
			}
		}

		var index = new FileInfo(Path.Combine(runDirectory.FullName, "index.md"));
		var aggregate = new FileInfo(Path.Combine(runDirectory.FullName, "aggregate-report.json"));
		await File.WriteAllTextAsync(index.FullName, RenderIndex(reports), cancellationToken);
		await File.WriteAllTextAsync(aggregate.FullName, JsonSerializer.Serialize(reports, JsonOptions), cancellationToken);
		return new ReportRun(runDirectory, index, aggregate);
	}

	private static string RenderIndex(IReadOnlyList<AiReadinessReport> reports)
	{
		var lines = new List<string>
		{
			"# AI Readiness Report",
			string.Empty,
			$"Generated: {DateTimeOffset.UtcNow:O}",
			string.Empty,
			"| Repository | Score | Type | Report |",
			"|---|---:|---|---|"
		};

		foreach (var report in reports.OrderByDescending(r => r.OverallScore))
		{
			var slug = SanitizeRepo(report.Repo);
			lines.Add($"| `{report.Repo}` | {report.OverallScore}/100 | `{report.RepositoryType}` | [Markdown](repos/{slug}/report.md) |");
		}

		lines.Add(string.Empty);
		return string.Join(Environment.NewLine, lines);
	}

	private static string SanitizeRepo(string repo)
	{
		var invalid = Path.GetInvalidFileNameChars().ToHashSet();
		return string.Concat(repo.Select(ch => ch == '/' ? '-' : invalid.Contains(ch) ? '-' : char.ToLowerInvariant(ch)));
	}
}
