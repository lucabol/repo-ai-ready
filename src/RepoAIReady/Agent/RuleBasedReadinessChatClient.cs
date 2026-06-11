using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RepoAIReady.Rules;

namespace RepoAIReady.Agent;

public sealed class RuleBasedReadinessChatClient(RuleBasedReadinessEvaluator evaluator) : IChatClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public Task<ChatResponse> GetResponseAsync(
		IEnumerable<ChatMessage> messages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		var payloadText = messages
			.Reverse()
			.Select(m => m.Text)
			.FirstOrDefault(t => t.Contains("\"evidence\"", StringComparison.OrdinalIgnoreCase) || t.Contains("\"Evidence\"", StringComparison.Ordinal));

		if (payloadText is null)
		{
			throw new InvalidOperationException("The judge prompt did not include repository evidence.");
		}

		var payload = JsonSerializer.Deserialize<AgentPromptPayload>(payloadText, JsonOptions)
			?? throw new InvalidOperationException("Could not deserialize agent prompt payload.");
		var report = evaluator.Evaluate(payload.Rubric, payload.Evidence);
		var json = JsonSerializer.Serialize(report, JsonOptions);

		return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
		{
			CreatedAt = DateTimeOffset.UtcNow,
			FinishReason = ChatFinishReason.Stop,
			ModelId = "repo-ai-ready-rule-based"
		});
	}

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

	public object? GetService(Type serviceType, object? serviceKey = null) =>
		serviceType == typeof(ChatClientMetadata)
			? new ChatClientMetadata("repo-ai-ready", null, "rule-based")
			: null;

	public void Dispose()
	{
	}
}
