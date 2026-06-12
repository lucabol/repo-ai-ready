namespace RepoAIReady.Rules;

public static class AiReadinessReportValidator
{
	private const string DefaultRepositoryType = "single_repo";
	private static readonly HashSet<string> ValidRepositoryTypes = new(StringComparer.Ordinal)
	{
		"single_repo",
		"monorepo",
		"large_repo"
	};

	public static AiReadinessReport Normalize(AiReadinessReport? report, string fallbackRepo)
	{
		ArgumentNullException.ThrowIfNull(report);

		var fundamentals = NormalizeFundamentals(report.Fundamentals);
		return new AiReadinessReport(
			string.IsNullOrWhiteSpace(report.Repo) ? fallbackRepo : report.Repo,
			NormalizeRepositoryType(report.RepositoryType),
			OverallScore(fundamentals),
			fundamentals,
			NormalizeList(report.TopStrengths),
			NormalizeList(report.HighestImpactImprovements),
			NormalizeList(report.Uncertainties));
	}

	private static FundamentalsBlock NormalizeFundamentals(FundamentalsBlock? fundamentals) =>
		new(
			NormalizeScore(fundamentals?.Documentation),
			NormalizeScore(fundamentals?.StyleAndValidation),
			NormalizeScore(fundamentals?.Testing),
			NormalizeScore(fundamentals?.BuildInfrastructure),
			NormalizeScore(fundamentals?.AiContext));

	private static FundamentalScore NormalizeScore(FundamentalScore? score) =>
		new(
			score is null ? 0 : Math.Clamp(score.Score, 0, 20),
			NormalizeList(score?.Evidence),
			NormalizeList(score?.Gaps));

	private static IReadOnlyList<string> NormalizeList(IEnumerable<string?>? items) =>
		items?.Where(static item => item is not null).Cast<string>().ToArray() ?? [];

	private static string NormalizeRepositoryType(string? repositoryType)
	{
		var normalized = repositoryType?.Trim().ToLowerInvariant();
		return normalized is not null && ValidRepositoryTypes.Contains(normalized)
			? normalized
			: DefaultRepositoryType;
	}

	private static int OverallScore(FundamentalsBlock fundamentals) =>
		fundamentals.Documentation.Score +
		fundamentals.StyleAndValidation.Score +
		fundamentals.Testing.Score +
		fundamentals.BuildInfrastructure.Score +
		fundamentals.AiContext.Score;
}
