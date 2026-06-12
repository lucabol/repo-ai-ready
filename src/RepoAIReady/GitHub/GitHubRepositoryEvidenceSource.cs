using Octokit;
using RepoAIReady.Cli;

namespace RepoAIReady.GitHub;

public sealed class GitHubRepositoryEvidenceSource : IRepositoryEvidenceSource
{
	private const int MaxContentChars = 16_000;

	private static readonly string[] PathsToInspect =
	[
		"README.md",
		"CONTRIBUTING.md",
		"SECURITY.md",
		"docs",
		"architecture",
		".github",
		".github/copilot-instructions.md",
		".github/instructions",
		".github/prompts",
		".github/skills",
		".github/agents",
		".github/mcp.json",
		".github/workflows",
		".github/dependabot.yml",
		".vscode",
		".vscode/mcp.json",
		".mcp.json",
		".claude",
		".claude/skills",
		".agents",
		".agents/skills",
		".devcontainer",
		"Dockerfile",
		"docker-compose.yml",
		"package.json",
		"package-lock.json",
		"pnpm-lock.yaml",
		"yarn.lock",
		"tsconfig.json",
		".editorconfig",
		"eslint.config.js",
		".eslintrc.json",
		".prettierrc",
		"biome.json",
		"pyproject.toml",
		"requirements.txt",
		"go.mod",
		"go.sum",
		"Cargo.toml",
		"Cargo.lock",
		"global.json",
		"test",
		"tests",
		"__tests__",
		"src"
	];

	private readonly GitHubClient _client;

	public GitHubRepositoryEvidenceSource(string? token)
	{
		_client = new GitHubClient(new ProductHeaderValue("RepoAIReady"));
		if (!string.IsNullOrWhiteSpace(token))
		{
			_client.Credentials = new Credentials(token);
		}
	}

	public async Task<CollectedRepositoryEvidence> CollectAsync(RepositorySlug repository, CancellationToken cancellationToken)
	{
		var repo = await _client.Repository.Get(repository.Owner, repository.Name);
		cancellationToken.ThrowIfCancellationRequested();

		var files = new Dictionary<string, EvidenceFile>(StringComparer.OrdinalIgnoreCase);
		var missing = new List<string>();

		foreach (var item in await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name))
		{
			AddOrReplaceWithRicherEvidence(files, ToEvidenceFile(item));
		}

		foreach (var path in PathsToInspect)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (files.TryGetValue(path, out var existing) && !ShouldRefreshInspectedPath(existing))
			{
				continue;
			}

			try
			{
				var contents = await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name, path);
				if (contents.Count == 0)
				{
					missing.Add(path);
					continue;
				}

				foreach (var content in contents.Take(40))
				{
					var evidenceFile = await ToEvidenceFileAsync(repository, content, path);
					AddOrReplaceWithRicherEvidence(files, evidenceFile);
					if (ShouldFetchDirectoryTree(evidenceFile))
					{
						await AddDirectoryTreeAsync(repository, evidenceFile.Path, files, remainingDepth: 2, cancellationToken);
					}
				}
			}
			catch (NotFoundException)
			{
				if (!files.ContainsKey(path))
				{
					missing.Add(path);
				}
			}
		}

		var metadata = new RepositoryMetadata(
			repo.FullName,
			repo.Description ?? string.Empty,
			repo.DefaultBranch,
			repo.HtmlUrl,
			repo.Language ?? string.Empty,
			repo.Private,
			repo.PushedAt);

		return new CollectedRepositoryEvidence(repository, metadata, files.Values.ToList(), missing);
	}

	internal static bool ShouldRefreshInspectedPath(EvidenceFile evidence) =>
		evidence.IsDirectory || (evidence.IsFile && string.IsNullOrWhiteSpace(evidence.Content));

	internal static void AddOrReplaceWithRicherEvidence(IDictionary<string, EvidenceFile> files, EvidenceFile evidence)
	{
		if (!files.TryGetValue(evidence.Path, out var existing) || IsRicherThan(evidence, existing))
		{
			files[evidence.Path] = evidence;
		}
	}

	private static bool IsRicherThan(EvidenceFile candidate, EvidenceFile existing)
	{
		if (candidate.IsFile && existing.IsFile)
		{
			return string.IsNullOrWhiteSpace(existing.Content) && !string.IsNullOrWhiteSpace(candidate.Content);
		}

		return candidate.Exists && !existing.Exists;
	}

	private static EvidenceFile ToEvidenceFile(RepositoryContent content, string? requestedPath = null)
	{
		var kind = content.Type.Value.ToString().ToLowerInvariant();
		var text = content.Type == ContentType.File ? content.Content : null;
		var truncated = text is not null && text.Length > MaxContentChars;
		if (truncated)
		{
			text = text![..MaxContentChars] + "\n...(truncated)";
		}

		var path = content.Path;
		if (string.IsNullOrWhiteSpace(path) && requestedPath is not null)
		{
			path = requestedPath;
		}

		return new EvidenceFile(path, kind, text, content.HtmlUrl, content.Sha, truncated);
	}

	private async Task<EvidenceFile> ToEvidenceFileAsync(RepositorySlug repository, RepositoryContent content, string? requestedPath = null)
	{
		var evidence = ToEvidenceFile(content, requestedPath);
		if (!evidence.IsFile || evidence.Content is not null || !ShouldFetchDirectoryChild(evidence.Path))
		{
			return evidence;
		}

		try
		{
			var file = await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name, evidence.Path);
			return ToEvidenceFile(file.First(), requestedPath);
		}
		catch (NotFoundException)
		{
			return evidence;
		}
	}

	private static bool ShouldFetchDirectoryChild(string path) =>
		path.StartsWith(".github/agents/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/instructions/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/prompts/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/skills/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".vscode/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".claude/skills/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".agents/skills/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".devcontainer/", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase);

	private async Task AddDirectoryTreeAsync(
		RepositorySlug repository,
		string path,
		IDictionary<string, EvidenceFile> files,
		int remainingDepth,
		CancellationToken cancellationToken)
	{
		if (remainingDepth <= 0)
		{
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			foreach (var content in (await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name, path)).Take(40))
			{
				cancellationToken.ThrowIfCancellationRequested();
				var evidenceFile = await ToEvidenceFileAsync(repository, content, path);
				AddOrReplaceWithRicherEvidence(files, evidenceFile);
				if (ShouldFetchDirectoryTree(evidenceFile))
				{
					await AddDirectoryTreeAsync(repository, evidenceFile.Path, files, remainingDepth - 1, cancellationToken);
				}
			}
		}
		catch (NotFoundException)
		{
		}
	}

	internal static bool ShouldFetchDirectoryTree(EvidenceFile evidence) =>
		evidence.IsDirectory &&
		(evidence.Path.StartsWith(".github/skills/", StringComparison.OrdinalIgnoreCase) ||
		 evidence.Path.StartsWith(".claude/skills/", StringComparison.OrdinalIgnoreCase) ||
		 evidence.Path.StartsWith(".agents/skills/", StringComparison.OrdinalIgnoreCase));
}
