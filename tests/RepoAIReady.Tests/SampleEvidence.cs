using RepoAIReady.Cli;
using RepoAIReady.GitHub;

namespace RepoAIReady.Tests;

internal static class SampleEvidence
{
	public static CollectedRepositoryEvidence VscodeLike()
	{
		var repo = new RepositorySlug("microsoft", "vscode");
		var metadata = new RepositoryMetadata(
			"microsoft/vscode",
			"Visual Studio Code",
			"main",
			"https://github.com/microsoft/vscode",
			"TypeScript",
			IsPrivate: false,
			DateTimeOffset.UtcNow);

		var files = new List<EvidenceFile>
		{
			Dir("src"),
			Dir("test"),
			Dir("docs"),
			Dir(".github"),
			Dir(".github/workflows"),
			Dir(".github/instructions"),
			Dir(".github/prompts"),
			Dir(".github/skills"),
			Dir(".devcontainer"),
			File("README.md", "# Visual Studio Code\nArchitecture and contribution entry point."),
			File("CONTRIBUTING.md", "# Contributing\nDevelopment workflow and pull requests."),
			File(".editorconfig", "root = true\n[*]\nindent_style = tab"),
			File("package.json", """
				{
				  "scripts": {
				    "build": "npm run compile",
				    "test": "npm run test-node",
				    "eslint": "node build/eslint.ts",
				    "format": "prettier --check ."
				  }
				}
				"""),
			File("package-lock.json", "{}"),
			File("tsconfig.json", "{}"),
			File(".github/workflows/pr.yml", "npm run compile\nnpm run eslint\nnpm run test-node\nnpm run valid-layers-check"),
			File(".github/copilot-instructions.md", "# Copilot Instructions\nRun compile-check and tests."),
			File(".github/instructions/source.instructions.md", "---\napplyTo: src/**\n---\nLayering rules."),
			File(".github/prompts/implement.prompt.md", "Implement prompt"),
			File(".github/skills/unit-tests/SKILL.md", """
				---
				name: unit-tests
				description: Guide for creating and running unit tests. Use when asked to add or debug unit tests.
				---

				Follow the repository test conventions.
				"""),
			File(".github/workflows/copilot-setup-steps.yml", "name: Copilot Setup Steps"),
			File(".devcontainer/README.md", "Dev container setup")
		};

		return new CollectedRepositoryEvidence(repo, metadata, files, []);
	}

	private static EvidenceFile File(string path, string content) => new(path, "file", content, $"https://github.com/microsoft/vscode/blob/main/{path}", "sha", Truncated: false);

	private static EvidenceFile Dir(string path) => new(path, "dir", null, $"https://github.com/microsoft/vscode/tree/main/{path}", "sha", Truncated: false);
}
