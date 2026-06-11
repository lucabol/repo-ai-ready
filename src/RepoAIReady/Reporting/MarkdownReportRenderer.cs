using RepoAIReady.Rules;

namespace RepoAIReady.Reporting;

public static class MarkdownReportRenderer
{
	public static string Render(AiReadinessReport report)
	{
		var lines = new List<string>
		{
			$"# AI Readiness Evaluation: {report.Repo}",
			string.Empty,
			$"Overall score: **{report.OverallScore}/100**",
			$"Repository type: `{report.RepositoryType}`",
			string.Empty,
			"| Fundamental | Score | Evidence | Gaps |",
			"|---|---:|---|---|"
		};

		AddFundamental(lines, "Documentation", report.Fundamentals.Documentation);
		AddFundamental(lines, "Style and Validation", report.Fundamentals.StyleAndValidation);
		AddFundamental(lines, "Testing", report.Fundamentals.Testing);
		AddFundamental(lines, "Build Infrastructure", report.Fundamentals.BuildInfrastructure);
		AddFundamental(lines, "AI Context", report.Fundamentals.AiContext);

		AddList(lines, "Top strengths", report.TopStrengths);
		AddList(lines, "Highest-impact improvements", report.HighestImpactImprovements);
		AddList(lines, "Uncertainties", report.Uncertainties);

		return string.Join(Environment.NewLine, lines) + Environment.NewLine;
	}

	private static void AddFundamental(List<string> lines, string name, FundamentalScore score)
	{
		lines.Add($"| {Escape(name)} | {score.Score}/20 | {Escape(string.Join("<br>", score.Evidence))} | {Escape(string.Join("<br>", score.Gaps))} |");
	}

	private static void AddList(List<string> lines, string title, IReadOnlyList<string> items)
	{
		lines.Add(string.Empty);
		lines.Add($"## {title}");
		lines.Add(string.Empty);

		if (items.Count == 0)
		{
			lines.Add("- None");
			return;
		}

		for (var i = 0; i < items.Count; i++)
		{
			lines.Add($"{i + 1}. {items[i]}");
		}
	}

	private static string Escape(string value) =>
		value.Replace("|", "\\|", StringComparison.Ordinal).Replace(Environment.NewLine, "<br>", StringComparison.Ordinal);
}
