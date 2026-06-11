using RepoAIReady.Rules;

namespace RepoAIReady.Tests;

public sealed class RuleBasedReadinessEvaluatorTests
{
	[Fact]
	public void Evaluate_ScoresVscodeLikeRepositoryAsHighlyReady()
	{
		var report = new RuleBasedReadinessEvaluator().Evaluate("rubric", SampleEvidence.VscodeLike());

		Assert.Equal("microsoft/vscode", report.Repo);
		Assert.True(report.OverallScore >= 85);
		Assert.True(report.Fundamentals.AiContext.Score >= 18);
		Assert.Contains(report.TopStrengths, s => s.StartsWith("AI Context:", StringComparison.Ordinal));
	}
}
