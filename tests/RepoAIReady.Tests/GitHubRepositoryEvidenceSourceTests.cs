using RepoAIReady.GitHub;

namespace RepoAIReady.Tests;

public sealed class GitHubRepositoryEvidenceSourceTests
{
	[Fact]
	public void ShouldRefreshInspectedPath_RefreshesFilesWithoutContent()
	{
		var rootListingFile = new EvidenceFile("README.md", "file", null, "https://example.test/readme", "sha", Truncated: false);

		Assert.True(GitHubRepositoryEvidenceSource.ShouldRefreshInspectedPath(rootListingFile));
	}

	[Fact]
	public void AddOrReplaceWithRicherEvidence_PrefersFetchedFileContent()
	{
		var files = new Dictionary<string, EvidenceFile>(StringComparer.OrdinalIgnoreCase);
		var rootListingFile = new EvidenceFile("README.md", "file", null, "https://example.test/readme", "sha1", Truncated: false);
		var fetchedFile = new EvidenceFile("README.md", "file", "# Project", "https://example.test/readme", "sha2", Truncated: false);

		GitHubRepositoryEvidenceSource.AddOrReplaceWithRicherEvidence(files, rootListingFile);
		GitHubRepositoryEvidenceSource.AddOrReplaceWithRicherEvidence(files, fetchedFile);

		Assert.Equal("# Project", files["README.md"].Content);
	}
}
