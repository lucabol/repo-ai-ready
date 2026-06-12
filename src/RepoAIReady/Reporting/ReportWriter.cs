using System.Text.Json;
using System.Text.Json.Serialization;
using RepoAIReady.Cli;
using RepoAIReady.Rules;

namespace RepoAIReady.Reporting;

public sealed record ReportRun(DirectoryInfo Directory, FileInfo IndexMarkdown, FileInfo AggregateJson);

public sealed record RepositoryEvaluationFailure(
	[property: JsonPropertyName("repo")] string Repo,
	[property: JsonPropertyName("stage")] string Stage,
	[property: JsonPropertyName("exit_code")] int ExitCode,
	[property: JsonPropertyName("error_type")] string ErrorType,
	[property: JsonPropertyName("message")] string Message);

public sealed record ReportAggregate(
	[property: JsonPropertyName("reports")] IReadOnlyList<AiReadinessReport> Reports,
	[property: JsonPropertyName("failures")] IReadOnlyList<RepositoryEvaluationFailure> Failures);

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
		CancellationToken cancellationToken) =>
		await WriteAsync(outputDirectory, reports, [], format, cancellationToken);

	public async Task<ReportRun> WriteAsync(
		DirectoryInfo outputDirectory,
		IReadOnlyList<AiReadinessReport> reports,
		IReadOnlyList<RepositoryEvaluationFailure> failures,
		ReportFormat format,
		CancellationToken cancellationToken)
	{
		var writeMarkdown = format is ReportFormat.Markdown or ReportFormat.All;
		var writeJson = format is ReportFormat.Json or ReportFormat.All;
		var runDirectory = new DirectoryInfo(Path.Combine(outputDirectory.FullName, $"reports-{DateTime.UtcNow:yyyyMMdd-HHmmss}"));
		runDirectory.Create();
		var reposDirectory = Directory.CreateDirectory(Path.Combine(runDirectory.FullName, "repos"));

		foreach (var report in reports)
		{
			var repoDirectory = Directory.CreateDirectory(Path.Combine(reposDirectory.FullName, SanitizeRepo(report.Repo)));
			if (writeMarkdown)
			{
				await File.WriteAllTextAsync(Path.Combine(repoDirectory.FullName, "report.md"), MarkdownReportRenderer.Render(report), cancellationToken);
			}

			if (writeJson)
			{
				await File.WriteAllTextAsync(Path.Combine(repoDirectory.FullName, "report.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
			}
		}

		var index = new FileInfo(Path.Combine(runDirectory.FullName, "index.md"));
		var aggregate = new FileInfo(Path.Combine(runDirectory.FullName, "aggregate-report.json"));
		// The run-level index and aggregate are always written; the selected format controls only per-repository artifacts.
		await File.WriteAllTextAsync(index.FullName, RenderIndex(reports, failures, writeMarkdown, writeJson), cancellationToken);
		await File.WriteAllTextAsync(aggregate.FullName, SerializeAggregate(reports, failures), cancellationToken);
		return new ReportRun(runDirectory, index, aggregate);
	}

	private static string SerializeAggregate(IReadOnlyList<AiReadinessReport> reports, IReadOnlyList<RepositoryEvaluationFailure> failures) =>
		failures.Count == 0
			? JsonSerializer.Serialize(reports, JsonOptions)
			: JsonSerializer.Serialize(new ReportAggregate(reports, failures), JsonOptions);

	private static string RenderIndex(IReadOnlyList<AiReadinessReport> reports, IReadOnlyList<RepositoryEvaluationFailure> failures, bool writeMarkdown, bool writeJson)
	{
		var lines = new List<string>
		{
			"# AI Readiness Report",
			string.Empty,
			$"Generated: {DateTimeOffset.UtcNow:O}",
			"Aggregate JSON: [aggregate-report.json](aggregate-report.json)",
			string.Empty,
			"| Repository | Score | Type | Report |",
			"|---|---:|---|---|"
		};

		if (reports.Count == 0)
		{
			lines.Add("| _No successful repository reports were produced._ |  |  |  |");
		}

		foreach (var report in reports.OrderByDescending(r => r.OverallScore))
		{
			var slug = SanitizeRepo(report.Repo);
			lines.Add($"| `{Escape(report.Repo)}` | {report.OverallScore}/100 | `{Escape(report.RepositoryType)}` | {RenderReportLinks(slug, writeMarkdown, writeJson)} |");
		}

		if (failures.Count > 0)
		{
			lines.Add(string.Empty);
			lines.Add("## Repository failures");
			lines.Add(string.Empty);
			lines.Add("| Repository | Stage | Exit code | Error | Message |");
			lines.Add("|---|---|---:|---|---|");

			foreach (var failure in failures.OrderBy(static failure => failure.Repo, StringComparer.OrdinalIgnoreCase))
			{
				lines.Add($"| `{Escape(failure.Repo)}` | {Escape(failure.Stage)} | {failure.ExitCode} | `{Escape(failure.ErrorType)}` | {Escape(failure.Message)} |");
			}
		}

		lines.Add(string.Empty);
		return string.Join(Environment.NewLine, lines);
	}

	private static string RenderReportLinks(string slug, bool writeMarkdown, bool writeJson)
	{
		var links = new List<string>();
		if (writeMarkdown)
		{
			links.Add($"[Markdown](repos/{slug}/report.md)");
		}

		if (writeJson)
		{
			links.Add($"[JSON](repos/{slug}/report.json)");
		}

		return links.Count == 0 ? "Not written" : string.Join(" · ", links);
	}

	private static string Escape(string value) =>
		MarkdownReportRenderer.Escape(value);

	private static string SanitizeRepo(string repo)
	{
		var invalid = Path.GetInvalidFileNameChars().ToHashSet();
		return string.Concat(repo.Select(ch => ch == '/' ? '-' : invalid.Contains(ch) ? '-' : char.ToLowerInvariant(ch)));
	}
}
