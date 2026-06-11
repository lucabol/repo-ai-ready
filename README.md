# RepoAIReady

RepoAIReady is a .NET command-line tool that evaluates GitHub repositories for AI coding-agent readiness. It collects repository evidence through the GitHub API, judges it against a Markdown rubric, and writes console, Markdown, and JSON reports.

## Prerequisites

- .NET SDK installed so you can install and run the tool with `dotnet tool`.
- Access to a judge backend:
  - Default `copilot` backend: a logged-in GitHub Copilot CLI/SDK account, or `COPILOT_TOKEN`/`GITHUB_COPILOT_TOKEN` set.
  - `openai` backend: `OPENAI_API_KEY` set, with `OPENAI_ENDPOINT` if you use an OpenAI-compatible service such as Azure OpenAI.
  - `deterministic` backend: no LLM credentials required; useful for offline smoke tests and CI.
- Optional `GITHUB_TOKEN` or `GH_TOKEN` for collecting repository evidence from private repositories or to avoid unauthenticated GitHub API rate limits.

## Installation

Install the public tool package from NuGet.org:

```powershell
dotnet tool install --global RepoAIReady
```

Then run it with the `repo-ai-ready` command:

```powershell
repo-ai-ready ai-readiness-llm-judge.md microsoft/vscode
```

Until the package is published, install from a local package build:

```powershell
dotnet pack src\RepoAIReady\RepoAIReady.csproj --configuration Release
dotnet tool install RepoAIReady --global --add-source artifacts\nupkg
```

## Usage

```text
repo-ai-ready <judge.md> <org/repo> [org/repo ...] [--output <dir>] [--format console|markdown|json|all] [--backend copilot|openai|deterministic] [--env-file <path>] [--github-token <token>] [--copilot-token <token>] [--openai-key <key>] [--openai-endpoint <uri>] [--model <model>] [--min-score <0-100>]
```

Examples:

```powershell
repo-ai-ready ai-readiness-llm-judge.md microsoft/vscode
repo-ai-ready --judge ai-readiness-llm-judge.md microsoft/vscode dotnet/runtime --format all --output reports
repo-ai-ready ai-readiness-llm-judge.md microsoft/vscode --backend deterministic
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
| `copilot` | Default. Uses the GitHub Copilot SDK. |
| `openai` | Uses OpenAI-compatible chat completions. |
| `deterministic` | Offline heuristic evaluator for local smoke tests and CI. |

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
$toolPath = Join-Path $env:TEMP "repo-ai-ready-tool-test"
dotnet tool install RepoAIReady --tool-path $toolPath --add-source artifacts\nupkg --no-cache
& (Join-Path $toolPath "repo-ai-ready") --help
Remove-Item -Recurse -Force $toolPath
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

The workflow uses the GitHub repository variable `NUGET_USER` as the nuget.org profile name, defaulting to `lucabol`.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
