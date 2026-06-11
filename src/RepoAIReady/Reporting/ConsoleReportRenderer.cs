using RepoAIReady.Rules;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace RepoAIReady.Reporting;

public sealed class ConsoleReportRenderer
{
	private readonly IAnsiConsole _console;

	public ConsoleReportRenderer()
		: this(AnsiConsole.Console)
	{
	}

	internal ConsoleReportRenderer(IAnsiConsole console)
	{
		_console = console;
	}

	public void Render(IReadOnlyList<AiReadinessReport> reports, ReportRun run)
	{
		_console.Write(new Rule("[bold cyan]AI Readiness Results[/]").RuleStyle("cyan"));
		_console.WriteLine();

		var table = new Table()
			.Border(TableBorder.Rounded)
			.AddColumn("Repository")
			.AddColumn(new TableColumn("Score").RightAligned())
			.AddColumn("Status")
			.AddColumn("Type");

		foreach (var report in reports.OrderByDescending(r => r.OverallScore))
		{
			table.AddRow(
				TextCell(report.Repo),
				MarkupCell(ScoreMarkup(report.OverallScore, 100)),
				MarkupCell(StatusMarkup(report.OverallScore, 100)),
				TextCell(report.RepositoryType));
		}

		_console.Write(table);
		_console.WriteLine();

		foreach (var report in reports.OrderByDescending(r => r.OverallScore))
		{
			RenderRepositoryDashboard(report);
		}

		_console.MarkupLineInterpolated($"[green]Detailed report written to[/] {run.Directory.FullName}");
	}

	private void RenderRepositoryDashboard(AiReadinessReport report)
	{
		_console.Write(new Rule($"[bold]{Markup.Escape(report.Repo)}[/] [dim]({Markup.Escape(report.RepositoryType)})[/]").LeftJustified());

		var areas = new Table()
			.Border(TableBorder.SimpleHeavy)
			.AddColumn("Area")
			.AddColumn(new TableColumn("Score").RightAligned())
			.AddColumn("Status")
			.AddColumn(new TableColumn("Next action") { Width = 56 });

		foreach (var area in Areas(report))
		{
			areas.AddRow(
				TextCell(area.Name),
				MarkupCell(ScoreMarkup(area.Score.Score, 20)),
				MarkupCell(StatusMarkup(area.Score.Score, 20)),
				TextCell(NormalizeDetail(area.Score.Gaps.FirstOrDefault() ?? "No immediate remediation needed.")));
		}

		_console.Write(areas);
		RenderInlineList("Strengths", "green", report.TopStrengths, maxItems: 3);
		RenderInlineList("Remediation actions", "yellow", PrioritizedRemediationActions(report), maxItems: 5);
		RenderInlineList("Uncertainties", "grey", report.Uncertainties, maxItems: 3);
		_console.WriteLine();
	}

	private void RenderInlineList(string title, string color, IReadOnlyList<string> items, int maxItems)
	{
		if (items.Count == 0)
		{
			return;
		}

		_console.Write(MarkupCell($"[bold {color}]{Markup.Escape(title)}[/]"));
		_console.WriteLine();
		var index = 1;
		foreach (var item in items.Where(static item => !string.IsNullOrWhiteSpace(item)).Take(maxItems))
		{
			_console.Write(MarkupCell($"  [dim]{index}.[/] {Markup.Escape(NormalizeDetail(item))}"));
			_console.WriteLine();
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

	private static Markup MarkupCell(string markup) =>
		new(markup) { Overflow = Overflow.Fold };

	private static IRenderable TextCell(string text) =>
		MarkupCell(Markup.Escape(text));

	internal static string NormalizeDetail(string value)
	{
		return value.ReplaceLineEndings(" ").Trim();
	}
}

internal sealed record AreaSummary(string Name, FundamentalScore Score);
