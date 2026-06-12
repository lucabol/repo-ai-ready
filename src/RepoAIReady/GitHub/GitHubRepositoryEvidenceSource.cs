using Octokit;
using RepoAIReady.Cli;
using System.Text.RegularExpressions;

namespace RepoAIReady.GitHub;

public sealed class GitHubRepositoryEvidenceSource : IRepositoryEvidenceSource
{
	private const int MaxContentChars = 16_000;
	private const int MaxInspectedDirectoryEntries = 40;
	private const int MaxTreeEntriesToInspect = 5_000;
	private const int MaxTreeDirectoriesToTrack = 300;
	private const int MaxTreeFilesToFetch = 80;

	internal const string TreeTruncatedMissingPath = "__repo_ai_ready:git-tree-truncated";
	internal const string TreeInspectionLimitedMissingPath = "__repo_ai_ready:git-tree-inspection-limited";
	internal const string TreeContentFetchLimitedMissingPath = "__repo_ai_ready:git-tree-content-fetch-limited";

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

		await AddRepositoryTreeEvidenceAsync(repository, repo.DefaultBranch, files, missing, cancellationToken);

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

				foreach (var content in contents.Take(MaxInspectedDirectoryEntries))
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

		await AddSkillReferencedFilesAsync(repository, files, cancellationToken);

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

	private async Task AddRepositoryTreeEvidenceAsync(
		RepositorySlug repository,
		string defaultBranch,
		IDictionary<string, EvidenceFile> files,
		ICollection<string> missing,
		CancellationToken cancellationToken)
	{
		try
		{
			var tree = await _client.Git.Tree.GetRecursive(repository.Owner, repository.Name, defaultBranch);
			if (tree.Truncated)
			{
				missing.Add(TreeTruncatedMissingPath);
			}

			if (tree.Tree.Count > MaxTreeEntriesToInspect)
			{
				missing.Add(TreeInspectionLimitedMissingPath);
			}

			var selectedFiles = new Dictionary<string, TreeFileCandidate>(StringComparer.OrdinalIgnoreCase);
			var trackedDirectories = 0;
			var directoryTrackingLimited = false;
			foreach (var item in tree.Tree.Take(MaxTreeEntriesToInspect))
			{
				cancellationToken.ThrowIfCancellationRequested();
				var path = NormalizeRepositoryPath(item.Path);
				if (string.IsNullOrWhiteSpace(path))
				{
					continue;
				}

				var kind = ToEvidenceKind(item.Type.Value.ToString());
				if (kind is null)
				{
					continue;
				}

				if (kind == "dir")
				{
					directoryTrackingLimited |= TrackTreeDirectory(path, item.Sha, item.Url, files, ref trackedDirectories);
					continue;
				}

				if (!ShouldTrackTreeFile(path))
				{
					continue;
				}

				AddOrReplaceWithRicherEvidence(files, new EvidenceFile(path, "file", null, item.Url, item.Sha, Truncated: false));
				foreach (var parent in ParentDirectories(path))
				{
					directoryTrackingLimited |= TrackTreeDirectory(parent, null, null, files, ref trackedDirectories);
				}

				if (ShouldFetchTreeFileContent(path) && !selectedFiles.ContainsKey(path))
				{
					selectedFiles[path] = new TreeFileCandidate(path, TreeContentPriority(path));
				}
			}

			var filesToFetch = selectedFiles.Values
				.OrderBy(file => file.Priority)
				.ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
				.Take(MaxTreeFilesToFetch)
				.ToList();

			if (selectedFiles.Count > MaxTreeFilesToFetch)
			{
				missing.Add(TreeContentFetchLimitedMissingPath);
			}

			if (directoryTrackingLimited)
			{
				missing.Add(TreeInspectionLimitedMissingPath);
			}

			foreach (var candidate in filesToFetch)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (files.TryGetValue(candidate.Path, out var existing) && existing.IsFile && !string.IsNullOrWhiteSpace(existing.Content))
				{
					continue;
				}

				try
				{
					var contents = await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name, candidate.Path);
					if (contents.Count == 1)
					{
						AddOrReplaceWithRicherEvidence(files, ToEvidenceFile(contents[0], candidate.Path));
					}
				}
				catch (NotFoundException)
				{
				}
			}
		}
		catch (NotFoundException)
		{
		}
		catch (ApiException)
		{
			missing.Add(TreeInspectionLimitedMissingPath);
		}
	}

	private static string NormalizeRepositoryPath(string? path) =>
		(path ?? string.Empty).Replace('\\', '/').Trim('/');

	private static string? ToEvidenceKind(string type) =>
		type.Equals("tree", StringComparison.OrdinalIgnoreCase)
			? "dir"
			: type.Equals("blob", StringComparison.OrdinalIgnoreCase)
				? "file"
				: null;

	private static bool TrackTreeDirectory(
		string path,
		string? sha,
		string? url,
		IDictionary<string, EvidenceFile> files,
		ref int trackedDirectories)
	{
		if (!ShouldTrackTreeDirectory(path) || files.ContainsKey(path))
		{
			return false;
		}

		if (trackedDirectories >= MaxTreeDirectoriesToTrack)
		{
			return true;
		}

		trackedDirectories++;
		AddOrReplaceWithRicherEvidence(files, new EvidenceFile(path, "dir", null, url, sha, Truncated: false));
		return false;
	}

	internal static bool ShouldTrackTreeFile(string path) =>
		IsKnownRootFile(path) ||
		IsDotNetManifest(path) ||
		IsPackageManifest(path) ||
		IsWorkflowPath(path) ||
		IsAiGuidancePath(path) ||
		IsAreaReadmeOrDoc(path);

	internal static bool ShouldFetchTreeFileContent(string path) =>
		IsKnownRootFile(path) ||
		IsDotNetManifest(path) ||
		IsWorkflowPath(path) ||
		IsAiGuidancePath(path) ||
		IsAreaReadmeOrDoc(path) ||
		path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith(".mcp.json", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("mcp.json", StringComparison.OrdinalIgnoreCase);

	private static bool ShouldTrackTreeDirectory(string path)
	{
		var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
		return path.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
			path.Equals(".devcontainer", StringComparison.OrdinalIgnoreCase) ||
			path.Equals(".vscode", StringComparison.OrdinalIgnoreCase) ||
			path.Equals(".claude", StringComparison.OrdinalIgnoreCase) ||
			path.Equals(".agents", StringComparison.OrdinalIgnoreCase) ||
			path.Equals("docs", StringComparison.OrdinalIgnoreCase) ||
			path.Equals("architecture", StringComparison.OrdinalIgnoreCase) ||
			path.Equals("src", StringComparison.OrdinalIgnoreCase) ||
			segments.Any(segment =>
				segment.Equals("test", StringComparison.OrdinalIgnoreCase) ||
				segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
				segment.Equals("__tests__", StringComparison.OrdinalIgnoreCase)) ||
			path.StartsWith(".github/workflows", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".github/instructions", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".github/prompts", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".github/skills", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".github/agents", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".claude/skills", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".agents/skills", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith(".devcontainer", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsKnownRootFile(string path) =>
		path.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
		path.Equals("CONTRIBUTING.md", StringComparison.OrdinalIgnoreCase) ||
		path.Equals("SECURITY.md", StringComparison.OrdinalIgnoreCase) ||
		path.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase) ||
		path.Equals("global.json", StringComparison.OrdinalIgnoreCase) ||
		path.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
		path.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
		path.Equals(".mcp.json", StringComparison.OrdinalIgnoreCase);

	private static bool IsDotNetManifest(string path) =>
		path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("Directory.Build.targets", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("Directory.Packages.props", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("NuGet.config", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("packages.lock.json", StringComparison.OrdinalIgnoreCase);

	private static bool IsPackageManifest(string path) =>
		path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("package-lock.json", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("yarn.lock", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("go.sum", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith("Cargo.lock", StringComparison.OrdinalIgnoreCase);

	private static bool IsWorkflowPath(string path) =>
		path.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase);

	private static bool IsAiGuidancePath(string path) =>
		path.Equals(".github/copilot-instructions.md", StringComparison.OrdinalIgnoreCase) ||
		path.Equals(".github/mcp.json", StringComparison.OrdinalIgnoreCase) ||
		path.Equals(".vscode/mcp.json", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/instructions/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/prompts/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/agents/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".github/skills/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".claude/skills/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".agents/skills/", StringComparison.OrdinalIgnoreCase) ||
		path.StartsWith(".devcontainer/", StringComparison.OrdinalIgnoreCase);

	private static bool IsAreaReadmeOrDoc(string path) =>
		(path.Count(ch => ch == '/') >= 1 && Path.GetFileName(path).Equals("README.md", StringComparison.OrdinalIgnoreCase)) ||
		(path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) && IsMarkdownPath(path)) ||
		(path.StartsWith("architecture/", StringComparison.OrdinalIgnoreCase) && IsMarkdownPath(path));

	private static bool IsMarkdownPath(string path) =>
		path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
		path.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase);

	private static int TreeContentPriority(string path)
	{
		if (path.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
			path.Equals("CONTRIBUTING.md", StringComparison.OrdinalIgnoreCase) ||
			path.Equals(".github/copilot-instructions.md", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}

		if (IsWorkflowPath(path) || IsDotNetManifest(path))
		{
			return 1;
		}

		if (IsAiGuidancePath(path))
		{
			return 2;
		}

		return 3;
	}

	private static IEnumerable<string> ParentDirectories(string path)
	{
		var separator = path.LastIndexOf('/');
		while (separator > 0)
		{
			path = path[..separator];
			yield return path;
			separator = path.LastIndexOf('/');
		}
	}

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

	private sealed record TreeFileCandidate(string Path, int Priority);

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
			foreach (var content in (await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name, path)).Take(MaxInspectedDirectoryEntries))
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

	private async Task AddSkillReferencedFilesAsync(
		RepositorySlug repository,
		IDictionary<string, EvidenceFile> files,
		CancellationToken cancellationToken)
	{
		var paths = files.Values
			.Where(IsSkillFile)
			.SelectMany(SkillReferencedPaths)
			.Where(path => !files.ContainsKey(path))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(200)
			.ToList();

		foreach (var path in paths)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				var contents = await _client.Repository.Content.GetAllContents(repository.Owner, repository.Name, path);
				if (contents.Count == 1 && contents[0].Path.Equals(path, StringComparison.OrdinalIgnoreCase))
				{
					AddOrReplaceWithRicherEvidence(files, ToEvidenceFile(contents[0], path));
				}
				else if (contents.Count > 0)
				{
					AddOrReplaceWithRicherEvidence(files, new EvidenceFile(path, "dir", null, contents[0].HtmlUrl, contents[0].Sha, Truncated: false));
					foreach (var child in contents.Take(40))
					{
						AddOrReplaceWithRicherEvidence(files, await ToEvidenceFileAsync(repository, child, path));
					}
				}
			}
			catch (NotFoundException)
			{
			}
		}
	}

	internal static IReadOnlyList<string> SkillReferencedPaths(EvidenceFile skill)
	{
		if (!IsSkillFile(skill) || string.IsNullOrWhiteSpace(skill.Content))
		{
			return [];
		}

		var skillDirectory = DirectoryName(skill.Path);
		return FindRelativeMarkdownLinks(skill.Content)
			.Select(path => CombineRepositoryPath(skillDirectory, path))
			.Where(path => path is not null)
			.Cast<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static bool IsSkillFile(EvidenceFile file) =>
		file.IsFile &&
		file.Path.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase) &&
		(file.Path.StartsWith(".github/skills/", StringComparison.OrdinalIgnoreCase) ||
		 file.Path.StartsWith(".claude/skills/", StringComparison.OrdinalIgnoreCase) ||
		 file.Path.StartsWith(".agents/skills/", StringComparison.OrdinalIgnoreCase));

	private static IEnumerable<string> FindRelativeMarkdownLinks(string body) =>
		Regex.Matches(body, @"\[[^\]]+\]\((?<path>[^)#]+)(?:#[^)]+)?\)", RegexOptions.CultureInvariant)
			.Select(match => match.Groups["path"].Value.Trim())
			.Select(path => path.Split(' ', 2)[0].Trim('\'', '"'))
			.Where(path =>
				path.Length > 0 &&
				!path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
				!path.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
				!path.StartsWith("#", StringComparison.Ordinal));

	private static string? CombineRepositoryPath(string baseDirectory, string relativePath)
	{
		var path = relativePath.Replace('\\', '/');
		if (path.StartsWith("/", StringComparison.Ordinal))
		{
			return null;
		}

		var segments = new List<string>();
		foreach (var segment in (baseDirectory + "/" + path).Split('/', StringSplitOptions.RemoveEmptyEntries))
		{
			if (segment == ".")
			{
				continue;
			}

			if (segment == "..")
			{
				if (segments.Count == 0)
				{
					return null;
				}

				segments.RemoveAt(segments.Count - 1);
				continue;
			}

			segments.Add(segment);
		}

		return string.Join('/', segments);
	}

	private static string DirectoryName(string path)
	{
		var separator = path.LastIndexOf('/');
		return separator <= 0 ? string.Empty : path[..separator];
	}
}
