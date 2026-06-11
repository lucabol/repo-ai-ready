using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using RepoAIReady.Cli;
using System.ClientModel;

namespace RepoAIReady.Agent;

public static class OpenAiChatClientFactory
{
	public static IChatClient Create(AppOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.OpenAiKey))
		{
			throw new UsageException("--backend openai requires --openai-key or OPENAI_API_KEY.");
		}

		var model = string.IsNullOrWhiteSpace(options.Model) ? "gpt-4o-mini" : options.Model;
		if (string.IsNullOrWhiteSpace(options.OpenAiEndpoint))
		{
			return new ChatClient(model, options.OpenAiKey).AsIChatClient();
		}

		var clientOptions = new OpenAIClientOptions
		{
			Endpoint = new Uri(options.OpenAiEndpoint)
		};

		return new ChatClient(model, new ApiKeyCredential(options.OpenAiKey), clientOptions).AsIChatClient();
	}
}
