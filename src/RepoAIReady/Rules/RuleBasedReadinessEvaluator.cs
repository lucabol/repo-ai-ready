using System.Text.Json;
using System.Text.RegularExpressions;
using RepoAIReady.GitHub;

namespace RepoAIReady.Rules;

public sealed class RuleBasedReadinessEvaluator
{
	public AiReadinessReport Evaluate(string rubric, CollectedRepositoryEvidence evidence)
	{
		var documentation = ScoreDocumentation(evidence);
		var style = ScoreStyleAndValidation(evidence);
		var testing = ScoreTesting(evidence);
		var build = ScoreBuildInfrastructure(evidence);
		var aiContext = ScoreAiContext(evidence);
		var fundamentals = new FundamentalsBlock(documentation, style, testing, build, aiContext);
		var total = documentation.Score + style.Score + testing.Score + build.Score + aiContext.Score;

		return new AiReadinessReport(
			evidence.FullName,
			ClassifyRepository(evidence),
			total,
			fundamentals,
			TopStrengths(fundamentals),
			TopImprovements(fundamentals),
			Uncertainties(evidence));
	}

	private static FundamentalScore ScoreDocumentation(CollectedRepositoryEvidence evidence)
	{
		var score = 0;
		var found = new List<string>();
		var gaps = new List<string>();

		AddIf(evidence.Exists("README.md"), 8, "README.md exists and provides a root documentation entry point.", "Add a root README.md that explains project purpose and structure.");
		AddIf(evidence.Exists("CONTRIBUTING.md"), 4, "CONTRIBUTING.md documents contribution workflow.", "Add CONTRIBUTING.md with local setup, contribution, and PR guidance.");
		AddIf(evidence.DirectoryExists("docs"), 3, "docs/ exists for additional documentation.", "Add repo-local docs/ for architecture, setup, and operations.");
		AddIf(evidence.DirectoryExists("architecture"), 2, "architecture/ exists for design documentation.", "Add architecture docs describing major components and boundaries.");
		AddIf(HasAreaReadme(evidence), 3, "Area-level README files were found.", "Add area-level READMEs for major projects or subsystems.");

		return Build(score, found, gaps);

		void AddIf(bool condition, int points, string evidenceText, string gap)
		{
			if (condition)
			{
				score += points;
				found.Add(evidenceText);
			}
			else
			{
				gaps.Add(gap);
			}
		}
	}

	private static FundamentalScore ScoreStyleAndValidation(CollectedRepositoryEvidence evidence)
	{
		var score = 0;
		var found = new List<string>();
		var gaps = new List<string>();
		var packageScripts = ReadPackageScripts(evidence.Content("package.json"));

		AddIf(evidence.Exists(".editorconfig"), 4, ".editorconfig defines baseline formatting.", "Add .editorconfig or equivalent formatter configuration.");
		AddIf(evidence.Exists("tsconfig.json") || evidence.Files.Any(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) || evidence.Exists("pyproject.toml"), 4, "Static typing or language-level validation configuration is present.", "Add language type-check/static-analysis configuration.");
		AddIf(evidence.Exists("eslint.config.js") || evidence.Exists(".eslintrc.json") || evidence.Exists("biome.json") || packageScripts.Any(s => s.Contains("lint", StringComparison.OrdinalIgnoreCase)), 5, "Linting configuration or scripts are present.", "Add linting rules and a documented lint command.");
		AddIf(evidence.Exists(".prettierrc") || evidence.Exists("biome.json") || packageScripts.Any(s => s.Contains("format", StringComparison.OrdinalIgnoreCase)), 3, "Formatting tooling is present.", "Add an automated formatting command.");
		AddIf(HasWorkflowMention(evidence, "lint") || HasWorkflowMention(evidence, "typecheck") || HasWorkflowMention(evidence, "type check"), 4, "CI appears to run lint/type validation.", "Run linting and type checking in CI.");

		return Build(score, found, gaps);

		void AddIf(bool condition, int points, string evidenceText, string gap)
		{
			if (condition)
			{
				score += points;
				found.Add(evidenceText);
			}
			else
			{
				gaps.Add(gap);
			}
		}
	}

	private static FundamentalScore ScoreTesting(CollectedRepositoryEvidence evidence)
	{
		var score = 0;
		var found = new List<string>();
		var gaps = new List<string>();
		var packageScripts = ReadPackageScripts(evidence.Content("package.json"));

		AddIf(evidence.DirectoryExists("test") || evidence.DirectoryExists("tests") || evidence.DirectoryExists("__tests__") || evidence.Files.Any(f => f.Path.Contains("/test/", StringComparison.OrdinalIgnoreCase)), 6, "Test directories or test files are present.", "Add test suites under test/, tests/, __tests__, or area-specific test folders.");
		AddIf(packageScripts.Any(s => s.Contains("test", StringComparison.OrdinalIgnoreCase)), 6, "package.json includes test scripts.", "Expose a local test command.");
		AddIf(HasWorkflowMention(evidence, "test"), 5, "CI appears to run tests.", "Run tests in pull request CI.");
		AddIf(evidence.Files.Any(f => f.Path.Contains("integration", StringComparison.OrdinalIgnoreCase) || f.Path.Contains("e2e", StringComparison.OrdinalIgnoreCase)), 3, "Integration or end-to-end test signals were found.", "Add integration/end-to-end tests where behavior crosses boundaries.");

		return Build(score, found, gaps);

		void AddIf(bool condition, int points, string evidenceText, string gap)
		{
			if (condition)
			{
				score += points;
				found.Add(evidenceText);
			}
			else
			{
				gaps.Add(gap);
			}
		}
	}

	private static FundamentalScore ScoreBuildInfrastructure(CollectedRepositoryEvidence evidence)
	{
		var score = 0;
		var found = new List<string>();
		var gaps = new List<string>();
		var packageScripts = ReadPackageScripts(evidence.Content("package.json"));

		AddIf(packageScripts.Any(s => s.Contains("build", StringComparison.OrdinalIgnoreCase)) || evidence.Files.Any(f => f.Path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) || evidence.Exists("go.mod") || evidence.Exists("Cargo.toml"), 5, "Build scripts or project files are present.", "Add a documented build command or project file.");
		AddIf(evidence.Exists("package-lock.json") || evidence.Exists("pnpm-lock.yaml") || evidence.Exists("yarn.lock") || evidence.Exists("go.sum") || evidence.Exists("Cargo.lock"), 5, "Dependency lockfiles are present.", "Commit dependency lockfiles for reproducible installs.");
		AddIf(evidence.DirectoryExists(".github/workflows"), 5, "GitHub Actions workflows are present.", "Add pull request CI workflows that build, validate, and test changes.");
		AddIf(evidence.DirectoryExists(".devcontainer") || evidence.Exists("Dockerfile") || evidence.Exists("global.json"), 3, "Environment pinning/setup files are present.", "Add devcontainer, Dockerfile, global.json, or equivalent tool version pinning.");
		AddIf(evidence.Exists(".github/dependabot.yml"), 2, "Dependabot configuration is present.", "Add Dependabot or equivalent dependency maintenance automation.");

		return Build(score, found, gaps);

		void AddIf(bool condition, int points, string evidenceText, string gap)
		{
			if (condition)
			{
				score += points;
				found.Add(evidenceText);
			}
			else
			{
				gaps.Add(gap);
			}
		}
	}

	private static FundamentalScore ScoreAiContext(CollectedRepositoryEvidence evidence)
	{
		var score = 0;
		var found = new List<string>();
		var gaps = new List<string>();
		var repositoryType = ClassifyRepository(evidence);
		var copilotInstructions = evidence.Content(".github/copilot-instructions.md");

		if (!string.IsNullOrWhiteSpace(copilotInstructions))
		{
			score += 8;
			found.Add(".github/copilot-instructions.md provides repo-wide AI instructions.");
			if (ContainsValidationGuidance(copilotInstructions))
			{
				score += 4;
				found.Add("Repo-wide AI instructions include build, test, or validation guidance.");
			}
			else
			{
				gaps.Add("Expand .github/copilot-instructions.md with concrete build, test, lint, or validation commands.");
			}
		}
		else
		{
			gaps.Add("Add .github/copilot-instructions.md with architecture, conventions, and validation commands.");
		}

		if (evidence.DirectoryExists(".github/instructions"))
		{
			score += 4;
			found.Add(".github/instructions contains path-specific AI guidance.");
		}
		else if (repositoryType == "single_repo" && !string.IsNullOrWhiteSpace(copilotInstructions))
		{
			score += 4;
			found.Add("Single cohesive repository can rely on usable repo-wide AI instructions.");
		}
		else if (repositoryType is "monorepo" or "large_repo")
		{
			gaps.Add("Add path-specific .github/instructions/*.instructions.md files for major areas.");
		}

		var advancedContextScore = 0;
		if (HasPromptOrAgentCustomization(evidence))
		{
			advancedContextScore += 1;
			found.Add("Reusable prompts or custom agents are present for agent workflows.");
		}

		var skillReview = ReviewSkills(evidence);
		if (skillReview.ValidCount > 0)
		{
			advancedContextScore += 2;
			found.Add($"Valid Agent Skills were found ({skillReview.ValidCount}).");
		}

		gaps.AddRange(skillReview.Gaps);

		var mcpReview = ReviewMcpConfigurations(evidence);
		if (mcpReview.ValidCount > 0)
		{
			advancedContextScore += 2;
			found.Add($"Valid MCP configuration was found ({mcpReview.ValidCount}).");
		}

		gaps.AddRange(mcpReview.Gaps);

		score += Math.Min(advancedContextScore, 2);

		if (evidence.Files.Any(f => f.Path.Contains("copilot-setup-steps", StringComparison.OrdinalIgnoreCase)))
		{
			score += 2;
			found.Add("Copilot setup steps are present for cloud-agent environment customization.");
		}

		return Build(score, found, gaps);
	}

	private sealed record SignalReview(int ValidCount, IReadOnlyList<string> Gaps);

	private sealed record FrontmatterBlock(IReadOnlyDictionary<string, string> Values, string Body);

	private static bool ContainsValidationGuidance(string content)
	{
		var normalized = content.ToLowerInvariant();
		return normalized.Contains("build", StringComparison.Ordinal) ||
			normalized.Contains("test", StringComparison.Ordinal) ||
			normalized.Contains("lint", StringComparison.Ordinal) ||
			normalized.Contains("format", StringComparison.Ordinal) ||
			normalized.Contains("typecheck", StringComparison.Ordinal) ||
			normalized.Contains("type check", StringComparison.Ordinal) ||
			normalized.Contains("dotnet", StringComparison.Ordinal) ||
			normalized.Contains("npm", StringComparison.Ordinal) ||
			normalized.Contains("pnpm", StringComparison.Ordinal) ||
			normalized.Contains("yarn", StringComparison.Ordinal) ||
			normalized.Contains("cargo", StringComparison.Ordinal) ||
			normalized.Contains("pytest", StringComparison.Ordinal);
	}

	private static bool HasPromptOrAgentCustomization(CollectedRepositoryEvidence evidence) =>
		evidence.Files.Any(f => f.IsFile && f.Path.StartsWith(".github/prompts/", StringComparison.OrdinalIgnoreCase) && f.Path.EndsWith(".prompt.md", StringComparison.OrdinalIgnoreCase)) ||
		evidence.Files.Any(f => f.IsFile && f.Path.StartsWith(".github/agents/", StringComparison.OrdinalIgnoreCase) && f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));

	private static SignalReview ReviewSkills(CollectedRepositoryEvidence evidence)
	{
		var valid = 0;
		var gaps = new List<string>();
		foreach (var skill in evidence.Files.Where(IsSkillFile))
		{
			var skillGaps = ValidateSkill(evidence, skill);
			if (skillGaps.Count == 0)
			{
				valid++;
			}
			else
			{
				gaps.AddRange(skillGaps);
			}
		}

		return new SignalReview(valid, gaps);
	}

	private static IReadOnlyList<string> ValidateSkill(CollectedRepositoryEvidence evidence, EvidenceFile skill)
	{
		var gaps = new List<string>();
		var skillDirectory = GetDirectoryName(skill.Path);
		var expectedName = GetFileName(skillDirectory);

		if (string.IsNullOrWhiteSpace(skill.Content))
		{
			return [$"{skill.Path} exists but its SKILL.md content was not available for validation."];
		}

		if (!TryReadFrontmatter(skill.Content, out var frontmatter))
		{
			return [$"{skill.Path} must start with YAML frontmatter containing name and description."];
		}

		var name = GetFrontmatterValue(frontmatter, "name");
		if (string.IsNullOrWhiteSpace(name))
		{
			gaps.Add($"{skill.Path} is missing required skill frontmatter field 'name'.");
		}
		else
		{
			if (!IsValidSkillName(name))
			{
				gaps.Add($"{skill.Path} has invalid skill name '{name}'; use lowercase letters, numbers, and hyphens.");
			}

			if (!string.Equals(name, expectedName, StringComparison.Ordinal))
			{
				gaps.Add($"{skill.Path} skill name must match parent directory '{expectedName}'.");
			}
		}

		var description = GetFrontmatterValue(frontmatter, "description");
		if (string.IsNullOrWhiteSpace(description))
		{
			gaps.Add($"{skill.Path} is missing required skill frontmatter field 'description'.");
		}
		else if (!DescriptionExplainsUseCase(description))
		{
			gaps.Add($"{skill.Path} description should explain what the skill does and when Copilot should use it.");
		}

		foreach (var linkedPath in FindRelativeMarkdownLinks(frontmatter.Body))
		{
			var repositoryPath = CombineRepositoryPath(skillDirectory, linkedPath);
			if (repositoryPath is not null && !evidence.Exists(repositoryPath))
			{
				gaps.Add($"{skill.Path} references missing skill resource '{linkedPath}'.");
			}
		}

		var allowedTools = GetFrontmatterValue(frontmatter, "allowed-tools");
		if (allowedTools.Contains("shell", StringComparison.OrdinalIgnoreCase) || allowedTools.Contains("bash", StringComparison.OrdinalIgnoreCase))
		{
			gaps.Add($"{skill.Path} pre-approves shell tools; review scripts and source trust before relying on this skill.");
		}

		return gaps;
	}

	private static SignalReview ReviewMcpConfigurations(CollectedRepositoryEvidence evidence)
	{
		var valid = 0;
		var gaps = new List<string>();
		foreach (var mcpConfig in evidence.Files.Where(IsMcpConfigFile))
		{
			var review = ValidateMcpJson(mcpConfig);
			if (review.ValidCount > 0)
			{
				valid += review.ValidCount;
			}

			gaps.AddRange(review.Gaps);
		}

		foreach (var agent in evidence.Files.Where(IsCustomAgentWithMcpServers))
		{
			var securityGaps = FindMcpSecurityGaps(agent.Path, agent.Content ?? string.Empty, string.Empty);
			if (securityGaps.Count == 0)
			{
				valid++;
			}
			else
			{
				gaps.AddRange(securityGaps);
			}
		}

		return new SignalReview(valid, gaps);
	}

	private static SignalReview ValidateMcpJson(EvidenceFile mcpConfig)
	{
		if (string.IsNullOrWhiteSpace(mcpConfig.Content))
		{
			return new SignalReview(0, [$"{mcpConfig.Path} exists but content was not available for MCP validation."]);
		}

		var gaps = new List<string>();
		try
		{
			using var document = JsonDocument.Parse(
				mcpConfig.Content,
				new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
			if (!TryGetMcpServers(document.RootElement, out var servers, out var serverProperty))
			{
				return new SignalReview(0, [$"{mcpConfig.Path} must define a top-level 'servers' or 'mcpServers' object."]);
			}

			if (mcpConfig.Path.EndsWith(".vscode/mcp.json", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(serverProperty, "mcpServers", StringComparison.Ordinal))
			{
				gaps.Add($"{mcpConfig.Path} uses 'mcpServers'; VS Code workspace MCP config should use top-level 'servers'.");
			}

			var serverCount = 0;
			foreach (var server in servers.EnumerateObject())
			{
				serverCount++;
				if (server.Value.ValueKind != JsonValueKind.Object)
				{
					gaps.Add($"{mcpConfig.Path} MCP server '{server.Name}' must be a JSON object.");
					continue;
				}

				gaps.AddRange(ValidateMcpServer(mcpConfig.Path, server.Name, server.Value));
			}

			if (serverCount == 0)
			{
				gaps.Add($"{mcpConfig.Path} defines no MCP servers.");
			}

			gaps.AddRange(FindMcpSecurityGaps(mcpConfig.Path, mcpConfig.Content, ExtractMcpArgsText(servers)));
			return new SignalReview(gaps.Any(IsBlockingMcpGap) || serverCount == 0 ? 0 : 1, gaps);
		}
		catch (JsonException ex)
		{
			return new SignalReview(0, [$"{mcpConfig.Path} is not valid JSON for MCP configuration: {ex.Message}"]);
		}
	}

	private static IReadOnlyList<string> ValidateMcpServer(string path, string name, JsonElement server)
	{
		var gaps = new List<string>();
		var type = GetJsonString(server, "type");
		var hasCommand = HasNonEmptyJsonString(server, "command");
		var hasUrl = HasNonEmptyJsonString(server, "url");
		var isRemote = type is not null && (type.Equals("http", StringComparison.OrdinalIgnoreCase) || type.Equals("sse", StringComparison.OrdinalIgnoreCase));
		var isLocal = type is null || type.Equals("stdio", StringComparison.OrdinalIgnoreCase) || type.Equals("local", StringComparison.OrdinalIgnoreCase);

		if (isRemote || (type is null && hasUrl))
		{
			if (!hasUrl)
			{
				gaps.Add($"{path} MCP server '{name}' is remote but does not define a url.");
			}
		}
		else if (isLocal)
		{
			if (!hasCommand)
			{
				gaps.Add($"{path} MCP server '{name}' is local/stdio but does not define a command.");
			}
		}
		else
		{
			gaps.Add($"{path} MCP server '{name}' has unsupported type '{type}'.");
		}

		if (path.EndsWith(".github/mcp.json", StringComparison.OrdinalIgnoreCase) && !server.TryGetProperty("tools", out _))
		{
			gaps.Add($"{path} MCP server '{name}' should allowlist tools for cloud-agent/code-review use.");
		}

		if (server.TryGetProperty("tools", out var tools) &&
			tools.ValueKind == JsonValueKind.Array &&
			tools.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && t.GetString() == "*"))
		{
			gaps.Add($"{path} MCP server '{name}' enables all tools; prefer a least-privilege tool allowlist.");
		}

		return gaps;
	}

	private static IReadOnlyList<string> FindMcpSecurityGaps(string path, string content, string argsText)
	{
		var gaps = new List<string>();
		if (Regex.IsMatch(content, @"(?i)""(?:api[_-]?key|token|secret|password|credential)""\s*:\s*""(?!\$|\$\{)[^""]{8,}"""))
		{
			gaps.Add($"{path} appears to contain hardcoded MCP credentials; use inputs, environment variables, or COPILOT_MCP_* secrets.");
		}

		if (Regex.IsMatch(content, @"(?i)(ghp_|gho_|ghu_|ghs_|ghr_)[A-Za-z0-9]{30,}|sk-[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----"))
		{
			gaps.Add($"{path} appears to contain a concrete secret value in MCP configuration.");
		}

		if (Regex.IsMatch(argsText, @"(?i)(\$\(|`[^`]+`|;\s*\w|\|\s*\w|&&\s*\w|\|\|\s*\w|eval\s|bash\s+-c|sh\s+-c|curl\s+.*\|\s*(ba)?sh)"))
		{
			gaps.Add($"{path} MCP args contain shell-injection-prone command patterns.");
		}

		if (content.Contains("@latest", StringComparison.OrdinalIgnoreCase))
		{
			gaps.Add($"{path} uses @latest for an MCP package; pin versions for reproducible and auditable agent tooling.");
		}

		return gaps;
	}

	private static bool IsSkillFile(EvidenceFile file) =>
		file.IsFile &&
		file.Path.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase) &&
		(file.Path.StartsWith(".github/skills/", StringComparison.OrdinalIgnoreCase) ||
		 file.Path.StartsWith(".claude/skills/", StringComparison.OrdinalIgnoreCase) ||
		 file.Path.StartsWith(".agents/skills/", StringComparison.OrdinalIgnoreCase));

	private static bool IsMcpConfigFile(EvidenceFile file) =>
		file.IsFile && IsMcpConfigPath(file.Path);

	private static bool IsMcpConfigPath(string path) =>
		path.Equals(".vscode/mcp.json", StringComparison.OrdinalIgnoreCase) ||
		path.Equals(".mcp.json", StringComparison.OrdinalIgnoreCase) ||
		path.Equals(".github/mcp.json", StringComparison.OrdinalIgnoreCase);

	private static bool IsCustomAgentWithMcpServers(EvidenceFile file) =>
		file.IsFile &&
		file.Path.StartsWith(".github/agents/", StringComparison.OrdinalIgnoreCase) &&
		file.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
		file.Content?.Contains("mcp-servers:", StringComparison.OrdinalIgnoreCase) == true;

	private static bool TryReadFrontmatter(string content, out FrontmatterBlock frontmatter)
	{
		frontmatter = new FrontmatterBlock(new Dictionary<string, string>(), string.Empty);
		var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
		var lines = normalized.Split('\n');
		if (lines.Length < 3 || lines[0].Trim() != "---")
		{
			return false;
		}

		var end = Array.FindIndex(lines, 1, line => line.Trim() == "---");
		if (end < 0)
		{
			return false;
		}

		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		for (var i = 1; i < end; i++)
		{
			var line = lines[i];
			if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}

			var separator = line.IndexOf(':', StringComparison.Ordinal);
			if (separator <= 0)
			{
				continue;
			}

			var key = line[..separator].Trim();
			var value = line[(separator + 1)..].Trim();
			if (value is "|" or ">")
			{
				var block = new List<string>();
				while (i + 1 < end && (string.IsNullOrWhiteSpace(lines[i + 1]) || char.IsWhiteSpace(lines[i + 1][0])))
				{
					i++;
					if (!string.IsNullOrWhiteSpace(lines[i]))
					{
						block.Add(lines[i].Trim());
					}
				}

				value = string.Join(' ', block);
			}

			values[key] = TrimYamlScalar(value);
		}

		frontmatter = new FrontmatterBlock(values, string.Join('\n', lines.Skip(end + 1)));
		return true;
	}

	private static string GetFrontmatterValue(FrontmatterBlock frontmatter, string name) =>
		frontmatter.Values.TryGetValue(name, out var value) ? value : string.Empty;

	private static string TrimYamlScalar(string value) =>
		value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"'))
			? value[1..^1]
			: value;

	private static bool IsValidSkillName(string name) =>
		Regex.IsMatch(name, "^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant) &&
		!name.Contains("--", StringComparison.Ordinal);

	private static bool DescriptionExplainsUseCase(string description)
	{
		var normalized = description.ToLowerInvariant();
		return normalized.Contains("use when", StringComparison.Ordinal) ||
			normalized.Contains("when asked", StringComparison.Ordinal) ||
			normalized.Contains("when working", StringComparison.Ordinal) ||
			normalized.Contains("for ", StringComparison.Ordinal);
	}

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

	private static bool TryGetMcpServers(JsonElement root, out JsonElement servers, out string propertyName)
	{
		if (root.ValueKind == JsonValueKind.Object &&
			root.TryGetProperty("servers", out servers) &&
			servers.ValueKind == JsonValueKind.Object)
		{
			propertyName = "servers";
			return true;
		}

		if (root.ValueKind == JsonValueKind.Object &&
			root.TryGetProperty("mcpServers", out servers) &&
			servers.ValueKind == JsonValueKind.Object)
		{
			propertyName = "mcpServers";
			return true;
		}

		servers = default;
		propertyName = string.Empty;
		return false;
	}

	private static string ExtractMcpArgsText(JsonElement servers)
	{
		var args = new List<string>();
		foreach (var server in servers.EnumerateObject())
		{
			if (server.Value.ValueKind != JsonValueKind.Object ||
				!server.Value.TryGetProperty("args", out var argsElement) ||
				argsElement.ValueKind != JsonValueKind.Array)
			{
				continue;
			}

			args.AddRange(argsElement.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString() ?? string.Empty));
		}

		return string.Join(' ', args);
	}

	private static string? GetJsonString(JsonElement element, string propertyName) =>
		element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
			? property.GetString()
			: null;

	private static bool HasNonEmptyJsonString(JsonElement element, string propertyName) =>
		!string.IsNullOrWhiteSpace(GetJsonString(element, propertyName));

	private static bool IsBlockingMcpGap(string gap) =>
		gap.Contains("hardcoded", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("concrete secret", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("shell-injection", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("does not define", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("unsupported type", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("must be", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("not valid JSON", StringComparison.OrdinalIgnoreCase) ||
		gap.Contains("defines no MCP servers", StringComparison.OrdinalIgnoreCase);

	private static string GetDirectoryName(string path)
	{
		var separator = path.LastIndexOf('/');
		return separator <= 0 ? string.Empty : path[..separator];
	}

	private static string GetFileName(string path)
	{
		var separator = path.LastIndexOf('/');
		return separator < 0 ? path : path[(separator + 1)..];
	}

	private static string ClassifyRepository(CollectedRepositoryEvidence evidence)
	{
		var topLevelDirectories = evidence.Files.Count(f => f.IsDirectory && !f.Path.Contains('/'));
		var packageLikeFiles = evidence.Files.Count(f =>
			f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
			f.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
			f.Path.EndsWith("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
			f.Path.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase));

		if (packageLikeFiles > 3)
		{
			return "monorepo";
		}

		return topLevelDirectories >= 20 ? "large_repo" : "single_repo";
	}

	private static IReadOnlyList<string> TopStrengths(FundamentalsBlock fundamentals)
	{
		var strengths = FundamentalPairs(fundamentals)
			.OrderByDescending(p => p.Score.Score)
			.Take(3)
			.Select(p => StrengthText(p.Name, p.Score))
			.ToList();
		var aiContextStrength = AiContextCustomizationStrength(fundamentals.AiContext);
		if (aiContextStrength is null)
		{
			return strengths;
		}

		var existingAiContext = strengths.FindIndex(s => s.StartsWith("AI Context:", StringComparison.Ordinal));
		if (existingAiContext >= 0)
		{
			strengths[existingAiContext] = aiContextStrength;
		}
		else if (strengths.Count >= 3)
		{
			strengths[^1] = aiContextStrength;
		}
		else
		{
			strengths.Add(aiContextStrength);
		}

		return strengths;
	}

	private static string StrengthText(string name, FundamentalScore score) =>
		$"{name}: {score.Evidence.FirstOrDefault() ?? "No specific evidence."}";

	private static string? AiContextCustomizationStrength(FundamentalScore aiContext)
	{
		var hasSkills = aiContext.Evidence.Any(e => e.Contains("Agent Skills", StringComparison.OrdinalIgnoreCase));
		var hasMcp = aiContext.Evidence.Any(e => e.Contains("MCP", StringComparison.OrdinalIgnoreCase));
		return (hasSkills, hasMcp) switch
		{
			(true, true) => "AI Context: valid Agent Skills and MCP server configuration are present alongside repo guidance.",
			(true, false) => "AI Context: valid Agent Skills are present alongside repo guidance.",
			(false, true) => "AI Context: valid MCP server configuration is present alongside repo guidance.",
			_ => null
		};
	}

	private static IReadOnlyList<string> TopImprovements(FundamentalsBlock fundamentals) =>
		FundamentalPairs(fundamentals)
			.OrderBy(p => p.Score.Score)
			.Take(3)
			.Select(p => $"{p.Name}: {p.Score.Gaps.FirstOrDefault() ?? "No major gap identified."}")
			.ToList();

	private static IReadOnlyList<string> Uncertainties(CollectedRepositoryEvidence evidence)
	{
		var uncertainties = new List<string>();
		if (string.IsNullOrWhiteSpace(evidence.Metadata.PrimaryLanguage))
		{
			uncertainties.Add("Primary language was not reported by GitHub metadata.");
		}

		if (evidence.Files.Any(f => f.Truncated))
		{
			uncertainties.Add("Some large files were truncated before judging.");
		}

		if (!evidence.Files.Any(IsMcpConfigFile) && evidence.MissingPaths.Any(IsMcpConfigPath))
		{
			uncertainties.Add("Repository-level GitHub.com MCP settings are not exposed as repository files and were not assessed.");
		}

		if (evidence.MissingPaths.Count > 0)
		{
			uncertainties.Add("Some checklist paths were absent or inaccessible; missing paths were judged according to rubric expectations.");
		}

		return uncertainties;
	}

	private static IEnumerable<(string Name, FundamentalScore Score)> FundamentalPairs(FundamentalsBlock fundamentals)
	{
		yield return ("Documentation", fundamentals.Documentation);
		yield return ("Style and Validation", fundamentals.StyleAndValidation);
		yield return ("Testing", fundamentals.Testing);
		yield return ("Build Infrastructure", fundamentals.BuildInfrastructure);
		yield return ("AI Context", fundamentals.AiContext);
	}

	private static FundamentalScore Build(int score, List<string> evidence, List<string> gaps) =>
		new(Math.Clamp(score, 0, 20), evidence, gaps);

	private static bool HasAreaReadme(CollectedRepositoryEvidence evidence) =>
		evidence.Files.Any(f =>
			f.Path.Count(ch => ch == '/') >= 1 &&
			Path.GetFileName(f.Path).Equals("README.md", StringComparison.OrdinalIgnoreCase));

	private static bool HasWorkflowMention(CollectedRepositoryEvidence evidence, string text) =>
		evidence.Files.Any(f =>
			f.Path.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) &&
			f.Content?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);

	private static IReadOnlyList<string> ReadPackageScripts(string? packageJson)
	{
		if (string.IsNullOrWhiteSpace(packageJson))
		{
			return [];
		}

		try
		{
			using var doc = JsonDocument.Parse(packageJson);
			if (!doc.RootElement.TryGetProperty("scripts", out var scripts) || scripts.ValueKind != JsonValueKind.Object)
			{
				return [];
			}

			return scripts.EnumerateObject().Select(p => $"{p.Name}:{p.Value.GetString()}").ToList();
		}
		catch (JsonException)
		{
			return [];
		}
	}
}
