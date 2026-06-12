# RepoAIReady

RepoAIReady is a .NET command-line tool that evaluates GitHub repositories for AI coding-agent readiness. It collects repository evidence through the GitHub API, judges it against a Markdown rubric, and writes console, Markdown, and JSON reports.

## Features

- Evaluates one or more GitHub repositories for AI coding-agent readiness using a Markdown judge rubric.
- Scores repositories across documentation, style and validation, testing, build infrastructure, and AI context.
- Collects targeted GitHub evidence from README files, docs, workflows, dependency files, tests, source structure, dev containers, and Copilot-specific guidance.
- Supports GitHub Copilot, OpenAI-compatible, and deterministic offline judging backends.
- Runs repository evaluations in parallel with configurable worker count and live progress reporting.
- Produces console dashboards plus Markdown and JSON reports, including per-repository details and aggregate summaries.
- Highlights top strengths, highest-impact improvements, and uncertainties for each repository.
- Supports .env configuration, explicit tokens, model overrides, minimum-score exit codes, and private-repository evidence collection.

## Prerequisites

- .NET SDK installed so you can install and run the tool with `dotnet tool`.
- Access to a judge backend:
  - Default `copilot` backend: a logged-in GitHub Copilot CLI/SDK account, or `COPILOT_TOKEN`/`GITHUB_COPILOT_TOKEN` set; the packaged tool currently supports this backend only on Windows x64.
  - `openai` backend: `OPENAI_API_KEY` set, with `OPENAI_ENDPOINT` if you use an OpenAI-compatible service such as Azure OpenAI.
  - `deterministic` backend: no LLM credentials required; useful for offline smoke tests and CI.
- Optional `GITHUB_TOKEN` or `GH_TOKEN` for collecting repository evidence from private repositories or to avoid unauthenticated GitHub API rate limits.

## Installation

Install the latest preview package from NuGet.org:

```powershell
dotnet tool install --global RepoAIReady --prerelease
```

If RepoAIReady is already installed, update it with:

```powershell
dotnet tool update --global RepoAIReady --prerelease
```

Then run it with the `repo-ai-ready` command:

```powershell
repo-ai-ready microsoft/vscode
```

Until the package is published, install from a local package build:

```powershell
dotnet pack src\RepoAIReady\RepoAIReady.csproj --configuration Release
dotnet tool install RepoAIReady --global --add-source artifacts\nupkg
```

## Usage

```text
repo-ai-ready <org/repo> [org/repo ...] [--judge-file <path>] [--output <dir>] [--format console|markdown|json|all] [--backend copilot|openai|deterministic] [--env-file <path>] [--github-token <token>] [--copilot-token <token>] [--openai-key <key>] [--openai-endpoint <uri>] [--model <model>] [--min-score <0-100>]
```

RepoAIReady uses its bundled `ai-readiness-llm-judge.md` by default. Use `--judge-file <path>` to load a custom rubric.

Examples:

```powershell
repo-ai-ready microsoft/vscode
repo-ai-ready microsoft/vscode dotnet/runtime --format all --output reports
repo-ai-ready microsoft/vscode --judge-file custom-judge.md --backend deterministic
```

## Authentication and backends

RepoAIReady loads `.env` from the current directory by default. Copy `.env.example` and fill in the values you need:

```powershell
Copy-Item .env.example .env
```

Supported environment variables:

| Variable | Purpose |
|---|---|
| `GITHUB_TOKEN` or `GH_TOKEN` | Optional GitHub token for collecting repository evidence. |
| `COPILOT_TOKEN` or `GITHUB_COPILOT_TOKEN` | Optional Copilot token. Leave unset to use your logged-in Copilot CLI/SDK account. |
| `OPENAI_API_KEY` | Required when using `--backend openai`. |
| `OPENAI_ENDPOINT` | Optional OpenAI-compatible endpoint, for example Azure OpenAI. |
| `REPOAI_MODEL` | Optional model override. |

Backends:

| Backend | Description |
|---|---|
| `copilot` | Default. Uses the GitHub Copilot SDK with the bundled Windows x64 Copilot CLI. |
| `openai` | Uses OpenAI-compatible chat completions. |
| `deterministic` | Offline heuristic evaluator for local smoke tests and CI. |

### Copilot packaged-tool platform support

The NuGet package currently bundles the GitHub Copilot CLI native binary only for Windows x64 (`runtimes/win-x64/native/copilot.exe`). The default `copilot` backend is therefore supported by the packaged tool only on Windows x64; on Linux, macOS, or Windows ARM64, use `--backend deterministic` for offline heuristic judging or `--backend openai` for OpenAI-compatible judging. Unsupported Copilot platforms fail with an explicit error instead of silently implying cross-platform Copilot startup.

During `dotnet pack`, the project downloads `@github/copilot-win32-x64` from `registry.npmjs.org` using the `CopilotCliVersion` supplied by the GitHub Copilot SDK package. The current pack target does not pin an additional SHA-256 checksum, so pack and release should run only in trusted CI; the workflows verify the expected package entries and run an installed-tool deterministic smoke evaluation before publishing.

## Development

Build and test:

```powershell
dotnet restore RepoAIReady.sln
dotnet build RepoAIReady.sln --configuration Release --no-restore
dotnet test RepoAIReady.sln --configuration Release --no-build
```

Package and smoke-test the tool:

```powershell
dotnet pack src\RepoAIReady\RepoAIReady.csproj --configuration Release --output artifacts\nupkg
$toolPath = Join-Path (Resolve-Path artifacts).Path "tool-smoke"
$smokeOutput = Join-Path (Resolve-Path artifacts).Path "smoke-output"
Remove-Item -Recurse -Force $toolPath, $smokeOutput -ErrorAction SilentlyContinue
dotnet tool install RepoAIReady --tool-path $toolPath --add-source artifacts\nupkg --no-cache
& (Join-Path $toolPath "repo-ai-ready") lucabol/repo-ai-ready --backend deterministic --format json --output $smokeOutput --parallelism 1
Get-ChildItem $smokeOutput -Recurse -Filter aggregate-report.json
```

## Releases

Releases are driven by version tags:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The release workflow builds, tests, packs, publishes to NuGet.org with Trusted Publishing, and creates or updates a GitHub Release.

Configure the trusted publisher on nuget.org with these values:

| Field | Value |
|---|---|
| Repository Owner | `lucabol` |
| Repository | `repo-ai-ready` |
| Workflow File | `release.yml` |
| Environment | Leave empty |

Trusted Publishing uses OIDC, so the workflow does not require a NuGet API key secret.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
