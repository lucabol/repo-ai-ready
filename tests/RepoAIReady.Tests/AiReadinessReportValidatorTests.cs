using RepoAIReady.Rules;

namespace RepoAIReady.Tests;

public sealed class AiReadinessReportValidatorTests
{
	[Fact]
	public void Normalize_ReplacesNullListsAndInvalidRepositoryType()
	{
		var score = new FundamentalScore(4, null!, null!);
		var report = new AiReadinessReport(
			null!,
			"unknown",
			100,
			new FundamentalsBlock(score, score, score, score, score),
			null!,
			null!,
			null!);

		var normalized = AiReadinessReportValidator.Normalize(report, "owner/repo");

		Assert.Equal("owner/repo", normalized.Repo);
		Assert.Equal("single_repo", normalized.RepositoryType);
		Assert.Empty(normalized.TopStrengths);
		Assert.Empty(normalized.HighestImpactImprovements);
		Assert.Empty(normalized.Uncertainties);
		Assert.Empty(normalized.Fundamentals.Documentation.Evidence);
		Assert.Empty(normalized.Fundamentals.Documentation.Gaps);
	}

	[Fact]
	public void Normalize_ClampsCategoryScoresAndRecomputesOver100OverallScore()
	{
		var fundamentals = new FundamentalsBlock(
			Score(40),
			Score(25),
			Score(20),
			Score(19),
			Score(50));
		var report = Report(fundamentals, overallScore: 154);

		var normalized = AiReadinessReportValidator.Normalize(report, "owner/repo");

		Assert.Equal([20, 20, 20, 19, 20], Scores(normalized));
		Assert.Equal(99, normalized.OverallScore);
	}

	[Fact]
	public void Normalize_ClampsNegativeCategoryScores()
	{
		var fundamentals = new FundamentalsBlock(
			Score(-1),
			Score(10),
			Score(-20),
			Score(0),
			Score(5));
		var report = Report(fundamentals, overallScore: -10);

		var normalized = AiReadinessReportValidator.Normalize(report, "owner/repo");

		Assert.Equal([0, 10, 0, 0, 5], Scores(normalized));
		Assert.Equal(15, normalized.OverallScore);
	}

	[Fact]
	public void Normalize_CreatesEmptyFundamentalsWhenMissing()
	{
		var report = new AiReadinessReport(
			"owner/repo",
			"monorepo",
			80,
			null!,
			["strength"],
			["improvement"],
			["uncertainty"]);

		var normalized = AiReadinessReportValidator.Normalize(report, "fallback/repo");

		Assert.Equal("monorepo", normalized.RepositoryType);
		Assert.Equal([0, 0, 0, 0, 0], Scores(normalized));
		Assert.Equal(0, normalized.OverallScore);
		Assert.Equal(["strength"], normalized.TopStrengths);
	}

	[Fact]
	public void Normalize_RecomputesInconsistentOverallScore()
	{
		var fundamentals = new FundamentalsBlock(
			Score(10),
			Score(10),
			Score(10),
			Score(10),
			Score(10));
		var report = Report(fundamentals, overallScore: 100);

		var normalized = AiReadinessReportValidator.Normalize(report, "owner/repo");

		Assert.Equal(50, normalized.OverallScore);
	}

	[Fact]
	public void Normalize_HandlesMissingIndividualFundamentalScore()
	{
		var fundamentals = new FundamentalsBlock(
			null!,
			Score(10),
			Score(10),
			Score(10),
			Score(10));
		var report = Report(fundamentals, overallScore: 100);

		var normalized = AiReadinessReportValidator.Normalize(report, "owner/repo");

		Assert.Equal(0, normalized.Fundamentals.Documentation.Score);
		Assert.Empty(normalized.Fundamentals.Documentation.Evidence);
		Assert.Empty(normalized.Fundamentals.Documentation.Gaps);
		Assert.Equal(40, normalized.OverallScore);
	}

	private static AiReadinessReport Report(FundamentalsBlock fundamentals, int overallScore) =>
		new("owner/repo", "large_repo", overallScore, fundamentals, [], [], []);

	private static FundamentalScore Score(int score) =>
		new(score, [], []);

	private static int[] Scores(AiReadinessReport report) =>
		[
			report.Fundamentals.Documentation.Score,
			report.Fundamentals.StyleAndValidation.Score,
			report.Fundamentals.Testing.Score,
			report.Fundamentals.BuildInfrastructure.Score,
			report.Fundamentals.AiContext.Score
		];
}
