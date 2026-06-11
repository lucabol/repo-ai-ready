using System.ComponentModel;
using RepoAIReady.GitHub;

namespace RepoAIReady.Agent;

public sealed class EvidenceTools(CollectedRepositoryEvidence evidence)
{
	[Description("Get the text content of a repository file by path.")]
	public string GetFileContent([Description("Repository-relative file path.")] string path) =>
		evidence.Content(path) ?? $"[File not found or not text: {path}]";

	[Description("List known files and directories below a repository-relative path.")]
	public string ListDirectory([Description("Repository-relative directory path. Use empty string for root.")] string path)
	{
		var prefix = string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimEnd('/') + "/";
		var entries = evidence.Files
			.Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			.Select(f => f.Path[prefix.Length..])
			.Where(rest => rest.Length > 0 && !rest.Contains('/'))
			.OrderBy(static p => p)
			.ToList();

		return entries.Count == 0 ? "[No known entries]" : string.Join('\n', entries);
	}

	[Description("Check whether a repository path exists in the collected evidence.")]
	public bool PathExists([Description("Repository-relative path.")] string path) => evidence.Exists(path);
}
