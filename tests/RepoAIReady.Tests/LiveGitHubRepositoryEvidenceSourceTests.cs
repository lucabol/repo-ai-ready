using RepoAIReady.Cli;
using RepoAIReady.GitHub;

namespace RepoAIReady.Tests;

public sealed class LiveGitHubRepositoryEvidenceSourceTests
{
	[Fact]
	public async Task CollectAsync_CanReadPublicRepository_WhenLiveTestsEnabled()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("RUN_LIVE_GITHUB_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var source = new GitHubRepositoryEvidenceSource(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
		var evidence = await source.CollectAsync(new RepositorySlug("octokit", "octokit.net"), CancellationToken.None);

		Assert.Equal("octokit/octokit.net", evidence.FullName);
		Assert.True(evidence.Exists("README.md"));
	}
}
