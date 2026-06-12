using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using RepoAIReady.GitHub;
using RepoAIReady.Rules;

namespace RepoAIReady.Agent;

public sealed class AgentFrameworkRepoJudge(string rubric, IChatClient chatClient)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = false
	};

	public async Task<AiReadinessReport> EvaluateAsync(CollectedRepositoryEvidence evidence, CancellationToken cancellationToken)
	{
		var tools = new EvidenceTools(evidence);
		var agent = new ChatClientAgent(
			chatClient,
			"AIReadinessJudge",
			rubric,
			"Judges repository AI readiness from a Markdown rubric and collected GitHub evidence.",
			[
				AIFunctionFactory.Create(tools.GetFileContent),
				AIFunctionFactory.Create(tools.ListDirectory),
				AIFunctionFactory.Create(tools.PathExists)
			],
			loggerFactory: null,
			services: null);

		var session = await agent.CreateSessionAsync(cancellationToken);
		var payload = JsonSerializer.Serialize(new AgentPromptPayload(rubric, evidence), JsonOptions);
		var response = await agent.RunAsync<AiReadinessReport>(
			payload,
			session,
			JsonOptions,
			new ChatClientAgentRunOptions(new ChatOptions()),
			cancellationToken);

		return AiReadinessReportValidator.Normalize(response.Result, evidence.FullName);
	}
}
