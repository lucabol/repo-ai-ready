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

	[Theory]
	[InlineData(".github/skills/test-debugging")]
	[InlineData(".claude/skills/test-debugging")]
	[InlineData(".agents/skills/test-debugging")]
	public void ShouldFetchDirectoryTree_FetchesSkillDirectories(string path)
	{
		var skillDirectory = new EvidenceFile(path, "dir", null, "https://example.test/skill", "sha", Truncated: false);

		Assert.True(GitHubRepositoryEvidenceSource.ShouldFetchDirectoryTree(skillDirectory));
	}

	[Fact]
	public void ShouldFetchDirectoryTree_IgnoresUnrelatedDirectories()
	{
		var docsDirectory = new EvidenceFile("docs", "dir", null, "https://example.test/docs", "sha", Truncated: false);

		Assert.False(GitHubRepositoryEvidenceSource.ShouldFetchDirectoryTree(docsDirectory));
	}

	[Theory]
	[InlineData("RepoAIReady.sln")]
	[InlineData("src/RepoAIReady/RepoAIReady.csproj")]
	[InlineData("tests/RepoAIReady.Tests/RepoAIReady.Tests.csproj")]
	[InlineData("docs/setup/README.md")]
	[InlineData(".github/workflows/ci.yml")]
	[InlineData(".github/instructions/dotnet.instructions.md")]
	public void ShouldTrackTreeFile_IncludesNestedManifestsDocsAndAutomation(string path)
	{
		Assert.True(GitHubRepositoryEvidenceSource.ShouldTrackTreeFile(path));
	}

	[Theory]
	[InlineData("src/RepoAIReady/RepoAIReady.csproj")]
	[InlineData("tests/RepoAIReady.Tests/RepoAIReady.Tests.csproj")]
	[InlineData("docs/setup/validation.md")]
	[InlineData(".github/workflows/ci.yml")]
	[InlineData(".github/copilot-instructions.md")]
	public void ShouldFetchTreeFileContent_FetchesCommandBearingFiles(string path)
	{
		Assert.True(GitHubRepositoryEvidenceSource.ShouldFetchTreeFileContent(path));
	}

	[Fact]
	public void SkillReferencedPaths_ResolvesRepositoryRelativeLinks()
	{
		var skill = new EvidenceFile(
			".github/skills/otel/SKILL.md",
			"file",
			"""
			---
			name: otel
			description: OpenTelemetry guidance. Use when changing telemetry.
			---

			Read [monitoring](../../../extensions/copilot/docs/monitoring/agent_monitoring.md),
			[local reference](references/details.md), [external](https://example.test/doc), and [anchor](#section).
			""",
			"https://example.test/skill",
			"sha",
			Truncated: false);

		var paths = GitHubRepositoryEvidenceSource.SkillReferencedPaths(skill);

		Assert.Contains("extensions/copilot/docs/monitoring/agent_monitoring.md", paths);
		Assert.Contains(".github/skills/otel/references/details.md", paths);
		Assert.DoesNotContain(paths, path => path.Contains("example.test", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(paths, path => path.StartsWith("#", StringComparison.Ordinal));
	}
}
