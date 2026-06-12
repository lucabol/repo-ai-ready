using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
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

	[Fact]
	public async Task EvaluateAsync_NormalizesTypedReportBeforeReturning()
	{
		var judge = new AgentFrameworkRepoJudge(
			"# AI Readiness Repository Judge",
			new JsonResponseChatClient("""
				{
				  "repo": null,
				  "repository_type": "invalid",
				  "overall_score": 999,
				  "fundamentals": {
				    "documentation": { "score": 50, "evidence": null, "gaps": [null, "gap"] },
				    "style_and_validation": { "score": -5, "evidence": ["style"], "gaps": null },
				    "testing": null,
				    "build_infrastructure": { "score": 25, "evidence": [], "gaps": [] },
				    "ai_context": { "score": 10, "evidence": [], "gaps": [] }
				  },
				  "top_strengths": null,
				  "highest_impact_improvements": null,
				  "uncertainties": null
				}
				"""));

		var report = await judge.EvaluateAsync(SampleEvidence.VscodeLike(), CancellationToken.None);

		Assert.Equal("microsoft/vscode", report.Repo);
		Assert.Equal("single_repo", report.RepositoryType);
		Assert.Equal(50, report.OverallScore);
		Assert.Equal(20, report.Fundamentals.Documentation.Score);
		Assert.Equal(0, report.Fundamentals.StyleAndValidation.Score);
		Assert.Equal(0, report.Fundamentals.Testing.Score);
		Assert.Equal(20, report.Fundamentals.BuildInfrastructure.Score);
		Assert.Equal(10, report.Fundamentals.AiContext.Score);
		Assert.Empty(report.Fundamentals.Documentation.Evidence);
		Assert.Equal(["gap"], report.Fundamentals.Documentation.Gaps);
		Assert.Empty(report.TopStrengths);
		Assert.Empty(report.HighestImpactImprovements);
		Assert.Empty(report.Uncertainties);
	}

	private sealed class JsonResponseChatClient(string json) : IChatClient
	{
		public Task<ChatResponse> GetResponseAsync(
			IEnumerable<ChatMessage> messages,
			ChatOptions? options = null,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
			{
				CreatedAt = DateTimeOffset.UtcNow,
				FinishReason = ChatFinishReason.Stop,
				ModelId = "test"
			});

		public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
			IEnumerable<ChatMessage> messages,
			ChatOptions? options = null,
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var response = await GetResponseAsync(messages, options, cancellationToken);
			yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text)
			{
				FinishReason = ChatFinishReason.Stop
			};
		}

		public object? GetService(Type serviceType, object? serviceKey = null) => null;

		public void Dispose()
		{
		}
	}
}
