using RepoAIReady.Rules;
using Spectre.Console;

namespace RepoAIReady.Reporting;

public sealed class ConsoleReportRenderer
{
	public void Render(IReadOnlyList<AiReadinessReport> reports, ReportRun run)
	{
		AnsiConsole.Write(new Rule("[bold cyan]AI Readiness Results[/]").RuleStyle("cyan"));
		AnsiConsole.WriteLine();

		var table = new Table()
			.Border(TableBorder.Rounded)
			.AddColumn("Repository")
			.AddColumn(new TableColumn("Score").RightAligned())
			.AddColumn("Status")
			.AddColumn("Type");

		foreach (var report in reports.OrderByDescending(r => r.OverallScore))
		{
			table.AddRow(
				Markup.Escape(report.Repo),
				ScoreMarkup(report.OverallScore, 100),
				StatusMarkup(report.OverallScore, 100),
				Markup.Escape(report.RepositoryType));
		}

		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();

		foreach (var report in reports.OrderByDescending(r => r.OverallScore))
		{
			RenderRepositoryDashboard(report);
		}

		AnsiConsole.MarkupLineInterpolated($"[green]Detailed report written to[/] {run.Directory.FullName}");
	}

	private static void RenderRepositoryDashboard(AiReadinessReport report)
	{
		AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(report.Repo)}[/] [dim]({Markup.Escape(report.RepositoryType)})[/]").LeftJustified());

		var areas = new Table()
			.Border(TableBorder.SimpleHeavy)
			.AddColumn("Area")
			.AddColumn(new TableColumn("Score").RightAligned())
			.AddColumn("Status")
			.AddColumn("Next action");

		foreach (var area in Areas(report))
		{
			areas.AddRow(
				Markup.Escape(area.Name),
				ScoreMarkup(area.Score.Score, 20),
				StatusMarkup(area.Score.Score, 20),
				Markup.Escape(Trim(area.Score.Gaps.FirstOrDefault() ?? "No immediate remediation needed.", 58)));
		}

		AnsiConsole.Write(areas);
		RenderInlineList("Strengths", "green", report.TopStrengths, maxItems: 3);
		RenderInlineList("Remediation actions", "yellow", PrioritizedRemediationActions(report), maxItems: 5);
		RenderInlineList("Uncertainties", "grey", report.Uncertainties, maxItems: 3);
		AnsiConsole.WriteLine();
	}

	private static void RenderInlineList(string title, string color, IReadOnlyList<string> items, int maxItems)
	{
		if (items.Count == 0)
		{
			return;
		}

		AnsiConsole.MarkupLine($"[bold {color}]{Markup.Escape(title)}[/]");
		var index = 1;
		foreach (var item in items.Where(static item => !string.IsNullOrWhiteSpace(item)).Take(maxItems))
		{
			AnsiConsole.MarkupLine($"  [dim]{index}.[/] {Markup.Escape(Trim(item, 120))}");
			index++;
		}
	}

	internal static IReadOnlyList<string> PrioritizedRemediationActions(AiReadinessReport report)
	{
		var actions = report.HighestImpactImprovements
			.Concat(Areas(report)
				.OrderBy(area => area.Score.Score)
				.SelectMany(area => area.Score.Gaps.Select(gap => $"{area.Name}: {gap}")))
			.Where(static action => !string.IsNullOrWhiteSpace(action));

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<string>();
		foreach (var action in actions)
		{
			if (!seen.Add(NormalizeAction(action)))
			{
				continue;
			}

			result.Add(action);
			if (result.Count == 5)
			{
				break;
			}
		}

		return result;
	}

	private static string NormalizeAction(string action)
	{
		var colon = action.IndexOf(':', StringComparison.Ordinal);
		return colon >= 0 ? action[(colon + 1)..].Trim() : action.Trim();
	}

	internal static IReadOnlyList<AreaSummary> Areas(AiReadinessReport report) =>
	[
		new("Documentation", report.Fundamentals.Documentation),
		new("Style/validation", report.Fundamentals.StyleAndValidation),
		new("Testing", report.Fundamentals.Testing),
		new("Build infra", report.Fundamentals.BuildInfrastructure),
		new("AI context", report.Fundamentals.AiContext)
	];

	private static string ScoreMarkup(int score, int max)
	{
		var pct = (double)score / max;
		var color = pct >= 0.8 ? "green" : pct >= 0.5 ? "yellow" : "red";
		return $"[{color}]{score}/{max}[/]";
	}

	private static string StatusMarkup(int score, int max)
	{
		var pct = (double)score / max;
		return pct >= 0.8
			? "[green]Ready[/]"
			: pct >= 0.5
				? "[yellow]Needs work[/]"
				: "[red]At risk[/]";
	}

	private static string Trim(string value, int maxLength)
	{
		var normalized = value.ReplaceLineEndings(" ").Trim();
		return normalized.Length <= maxLength ? normalized : normalized[..(maxLength - 1)] + "…";
	}
}

internal sealed record AreaSummary(string Name, FundamentalScore Score);
