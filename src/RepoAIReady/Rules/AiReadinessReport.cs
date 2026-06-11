using System.Text.Json.Serialization;

namespace RepoAIReady.Rules;

public sealed record AiReadinessReport(
	[property: JsonPropertyName("repo")] string Repo,
	[property: JsonPropertyName("repository_type")] string RepositoryType,
	[property: JsonPropertyName("overall_score")] int OverallScore,
	[property: JsonPropertyName("fundamentals")] FundamentalsBlock Fundamentals,
	[property: JsonPropertyName("top_strengths")] IReadOnlyList<string> TopStrengths,
	[property: JsonPropertyName("highest_impact_improvements")] IReadOnlyList<string> HighestImpactImprovements,
	[property: JsonPropertyName("uncertainties")] IReadOnlyList<string> Uncertainties);

public sealed record FundamentalsBlock(
	[property: JsonPropertyName("documentation")] FundamentalScore Documentation,
	[property: JsonPropertyName("style_and_validation")] FundamentalScore StyleAndValidation,
	[property: JsonPropertyName("testing")] FundamentalScore Testing,
	[property: JsonPropertyName("build_infrastructure")] FundamentalScore BuildInfrastructure,
	[property: JsonPropertyName("ai_context")] FundamentalScore AiContext);

public sealed record FundamentalScore(
	[property: JsonPropertyName("score")] int Score,
	[property: JsonPropertyName("evidence")] IReadOnlyList<string> Evidence,
	[property: JsonPropertyName("gaps")] IReadOnlyList<string> Gaps);
