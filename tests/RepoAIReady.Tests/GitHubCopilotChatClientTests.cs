using GitHub.Copilot;
using RepoAIReady.Agent;
#pragma warning disable GHCP001 // Tests verify the explicit Copilot permission denial returned by the SDK.
using PermissionDecisionReject = GitHub.Copilot.Rpc.PermissionDecisionReject;
#pragma warning restore GHCP001

namespace RepoAIReady.Tests;

public sealed class GitHubCopilotChatClientTests
{
	[Fact]
	public async Task PermissionHandler_RejectsPermissionRequestsByDefault()
	{
		var request = new PermissionRequestRead
		{
			Path = "README.md",
			Intention = "inspect repository evidence"
		};

		var decision = await GitHubCopilotChatClient.DenyPermissionRequestAsync(request, null!);

#pragma warning disable GHCP001 // Tests verify the explicit Copilot permission denial returned by the SDK.
		var rejection = Assert.IsType<PermissionDecisionReject>(decision);
#pragma warning restore GHCP001
		Assert.NotNull(rejection.Feedback);
		Assert.Contains("read-only", rejection.Feedback, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("Permission-gated tool access is disabled", rejection.Feedback, StringComparison.Ordinal);
	}
}
