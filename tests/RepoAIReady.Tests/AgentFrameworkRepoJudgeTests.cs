using RepoAIReady.Agent;
using RepoAIReady.Rules;

namespace RepoAIReady.Tests;

public sealed class AgentFrameworkRepoJudgeTests
{
	[Fact]
	public async Task EvaluateAsync_ReturnsTypedReportThroughMicrosoftAgentFramework()
	{
		var judge = new AgentFrameworkRepoJudge(
			"# AI Readiness Repository Judge",
			new RuleBasedReadinessChatClient(new RuleBasedReadinessEvaluator()));

		var report = await judge.EvaluateAsync(SampleEvidence.VscodeLike(), CancellationToken.None);

		Assert.Equal("microsoft/vscode", report.Repo);
		Assert.InRange(report.OverallScore, 0, 100);
		Assert.Equal(5, new[]
		{
			report.Fundamentals.Documentation,
			report.Fundamentals.StyleAndValidation,
			report.Fundamentals.Testing,
			report.Fundamentals.BuildInfrastructure,
			report.Fundamentals.AiContext
		}.Length);
	}
}
