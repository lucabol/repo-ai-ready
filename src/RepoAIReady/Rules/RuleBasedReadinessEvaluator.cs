using System.Text.Json;
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

		AddIf(evidence.Exists(".github/copilot-instructions.md"), 8, ".github/copilot-instructions.md provides repo-wide AI instructions.", "Add .github/copilot-instructions.md with architecture, conventions, and validation commands.");
		AddIf(evidence.DirectoryExists(".github/instructions"), 6, ".github/instructions contains path-specific AI guidance.", "Add path-specific .github/instructions/*.instructions.md files for major areas.");
		AddIf(evidence.DirectoryExists(".github/prompts") || evidence.DirectoryExists(".github/skills") || evidence.DirectoryExists(".github/agents"), 4, "Additional AI prompts, skills, or agents are present.", "Add reusable prompts, skills, or agents only after baseline instructions exist.");
		AddIf(evidence.Files.Any(f => f.Path.Contains("copilot-setup-steps", StringComparison.OrdinalIgnoreCase)), 2, "Copilot setup steps are present.", "Add copilot-setup-steps.yml when cloud agent setup needs custom dependencies.");

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

	private static IReadOnlyList<string> TopStrengths(FundamentalsBlock fundamentals) =>
		FundamentalPairs(fundamentals)
			.OrderByDescending(p => p.Score.Score)
			.Take(3)
			.Select(p => $"{p.Name}: {p.Score.Evidence.FirstOrDefault() ?? "No specific evidence."}")
			.ToList();

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

		if (evidence.MissingPaths.Count > 0)
		{
			uncertainties.Add("Some checklist paths were absent or inaccessible; missing paths were treated as gaps.");
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
