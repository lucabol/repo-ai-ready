using System.Runtime.InteropServices;
using System.Xml.Linq;
using RepoAIReady.Agent;

namespace RepoAIReady.Tests;

public sealed class PackagingTests
{
	[Fact]
	public void CopilotBackendPlatformGuardMatchesPackagedRuntime()
	{
		Assert.True(GitHubCopilotChatClient.IsBundledCopilotCliPlatformSupported(isWindows: true, Architecture.X64));
		Assert.False(GitHubCopilotChatClient.IsBundledCopilotCliPlatformSupported(isWindows: true, Architecture.Arm64));
		Assert.False(GitHubCopilotChatClient.IsBundledCopilotCliPlatformSupported(isWindows: false, Architecture.X64));

		var message = GitHubCopilotChatClient.UnsupportedBundledCopilotCliPlatformMessage("Linux", Architecture.X64);
		Assert.Contains("Windows x64", message);
		Assert.Contains("win-x64", message);
		Assert.Contains("--backend deterministic", message);
		Assert.Contains("--backend openai", message);
	}

	[Fact]
	public void CopilotBackendPlatformGuardEnforcesCurrentRuntime()
	{
		if (GitHubCopilotChatClient.IsBundledCopilotCliPlatformSupported())
		{
			GitHubCopilotChatClient.EnsureBundledCopilotCliPlatformSupported();
			return;
		}

		var ex = Assert.Throws<CopilotBackendException>(GitHubCopilotChatClient.EnsureBundledCopilotCliPlatformSupported);
		Assert.Contains("Windows x64", ex.Message);
	}

	[Fact]
	public void ProjectFileBundlesDocumentedWindowsX64CopilotCliOnly()
	{
		var project = LoadProjectFile();
		var toolProperties = ProjectProperties(project);
		var target = project.Descendants("Target").Single(element => (string?)element.Attribute("Name") == "_AddWindowsCopilotCliToToolPackage");
		var targetProperties = target.Descendants("PropertyGroup")
			.Elements()
			.GroupBy(element => element.Name.LocalName)
			.ToDictionary(group => group.Key, group => group.Last().Value.Trim());

		Assert.Equal("repo-ai-ready", toolProperties["ToolCommandName"]);
		Assert.Equal("README.md", toolProperties["PackageReadmeFile"]);
		Assert.Equal("win32-x64", targetProperties["_RepoAIReadyCopilotWinPlatform"]);
		Assert.Equal("win-x64", targetProperties["_RepoAIReadyCopilotWinRid"]);
		Assert.Equal("copilot.exe", targetProperties["_RepoAIReadyCopilotWinBinary"]);
		Assert.Contains("registry.npmjs.org/@github/copilot-$(_RepoAIReadyCopilotWinPlatform)", targetProperties["_RepoAIReadyCopilotWinDownloadUrl"]);
		Assert.Contains(@"runtimes\$(_RepoAIReadyCopilotWinRid)\native", targetProperties["_RepoAIReadyCopilotWinOutputDir"]);
		Assert.Contains(@"runtimes\$(_RepoAIReadyCopilotWinRid)\native", targetProperties["_RepoAIReadyCopilotWinPublishDir"]);
		Assert.Contains(target.Descendants("DownloadFile"), element => (string?)element.Attribute("SourceUrl") == "$(_RepoAIReadyCopilotWinDownloadUrl)");
	}

	[Fact]
	public void ReadmeDocumentsCopilotPlatformAndPackTimeDownloadLimitations()
	{
		var readme = File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "README.md"));

		Assert.Contains("Windows x64", readme);
		Assert.Contains("win-x64", readme);
		Assert.Contains("registry.npmjs.org", readme);
		Assert.Contains("--backend deterministic", readme);
	}

	private static XDocument LoadProjectFile() =>
		XDocument.Load(Path.Combine(RepositoryRoot().FullName, "src", "RepoAIReady", "RepoAIReady.csproj"));

	private static Dictionary<string, string> ProjectProperties(XDocument project) =>
		project.Root!
			.Elements("PropertyGroup")
			.Elements()
			.ToDictionary(element => element.Name.LocalName, element => element.Value.Trim());

	private static DirectoryInfo RepositoryRoot()
	{
		var current = new DirectoryInfo(AppContext.BaseDirectory);
		while (current is not null && !File.Exists(Path.Combine(current.FullName, "RepoAIReady.sln")))
		{
			current = current.Parent;
		}

		return current ?? throw new InvalidOperationException("Could not locate repository root.");
	}
}
