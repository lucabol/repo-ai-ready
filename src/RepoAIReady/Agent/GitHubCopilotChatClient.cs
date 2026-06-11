using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace RepoAIReady.Agent;

public sealed class GitHubCopilotChatClient(string? copilotToken, string? model, string workingDirectory) : IChatClient
{
	private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(5);

	public async Task<ChatResponse> GetResponseAsync(
		IEnumerable<ChatMessage> messages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		var prompt = BuildPrompt(messages);
		var clientOptions = new CopilotClientOptions
		{
			Mode = CopilotClientMode.CopilotCli,
			WorkingDirectory = workingDirectory,
			GitHubToken = string.IsNullOrWhiteSpace(copilotToken) ? null : copilotToken,
			UseLoggedInUser = string.IsNullOrWhiteSpace(copilotToken)
		};

		await using var client = new CopilotClient(clientOptions);
		var started = false;
		try
		{
			await client.StartAsync(cancellationToken);
			started = true;
			await using var session = await client.CreateSessionAsync(new SessionConfig
			{
				ClientName = "RepoAIReady",
				Model = string.IsNullOrWhiteSpace(model) ? null : model,
				WorkingDirectory = workingDirectory,
				OnPermissionRequest = PermissionHandler.ApproveAll,
				Streaming = false
			}, cancellationToken);

			var response = await session.SendAndWaitAsync(new MessageOptions
			{
				Prompt = prompt,
				DisplayPrompt = "Evaluate repository AI readiness and return the requested JSON report."
			}, ResponseTimeout, cancellationToken);

			var responseData = response?.Data
				?? throw new CopilotBackendException("GitHub Copilot returned a response without message data.");
			var text = responseData.Content;
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new CopilotBackendException("GitHub Copilot returned an empty response.");
			}

			return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
			{
				CreatedAt = DateTimeOffset.UtcNow,
				FinishReason = ChatFinishReason.Stop,
				ModelId = responseData.Model ?? model ?? "github-copilot"
			};
		}
		catch (FileNotFoundException ex)
		{
			throw CreateStartupException(ex);
		}
		catch (Win32Exception ex)
		{
			throw CreateStartupException(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw CreateStartupException(ex);
		}
		catch (Exception ex) when (IsCopilotConnectionException(ex))
		{
			throw new CopilotBackendException($"GitHub Copilot failed while judging the repository: {ex.Message}", ex);
		}
		catch (TimeoutException ex)
		{
			throw new CopilotBackendException("GitHub Copilot did not finish judging within the 5 minute timeout.", ex);
		}
		finally
		{
			if (started)
			{
				await client.StopAsync();
			}
		}
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
			? new ChatClientMetadata("github-copilot", null, model ?? "default")
			: null;

	public void Dispose()
	{
	}

	private static string BuildPrompt(IEnumerable<ChatMessage> messages)
	{
		var builder = new StringBuilder();
		builder.AppendLine("Return only valid JSON matching the requested schema. Do not include markdown fences or prose.");
		foreach (var message in messages)
		{
			if (string.IsNullOrWhiteSpace(message.Text))
			{
				continue;
			}

			builder.AppendLine(CultureInfo.InvariantCulture, $"[{message.Role}]");
			builder.AppendLine(message.Text);
		}

		return builder.ToString();
	}

	private static CopilotBackendException CreateStartupException(Exception ex) =>
		new("GitHub Copilot is the default judge backend and uses your logged-in Copilot CLI/SDK account unless --copilot-token or COPILOT_TOKEN is set. It could not start a Copilot session. Check the underlying error below; if you only need offline heuristic judging, use --backend deterministic.", ex);

	private static bool IsCopilotConnectionException(Exception ex) =>
		ex.GetType().FullName is "GitHub.Copilot.ConnectionLostException" or "GitHub.Copilot.RemoteRpcException";
}
