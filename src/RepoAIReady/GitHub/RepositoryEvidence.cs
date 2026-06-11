using RepoAIReady.Cli;

namespace RepoAIReady.GitHub;

public sealed record RepositoryMetadata(
	string FullName,
	string Description,
	string DefaultBranch,
	string HtmlUrl,
	string PrimaryLanguage,
	bool IsPrivate,
	DateTimeOffset? PushedAt);

public sealed record EvidenceFile(
	string Path,
	string Kind,
	string? Content,
	string? HtmlUrl,
	string? Sha,
	bool Truncated)
{
	public bool Exists => Kind is "file" or "dir";
	public bool IsFile => Kind == "file";
	public bool IsDirectory => Kind == "dir";
}

public sealed record CollectedRepositoryEvidence(
	RepositorySlug Repository,
	RepositoryMetadata Metadata,
	IReadOnlyList<EvidenceFile> Files,
	IReadOnlyList<string> MissingPaths)
{
	public string FullName => Repository.FullName;

	public EvidenceFile? Find(string path) =>
		Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));

	public bool Exists(string path) => Find(path)?.Exists == true;

	public bool DirectoryExists(string path) => Find(path)?.IsDirectory == true;

	public string? Content(string path) => Find(path)?.Content;
}
