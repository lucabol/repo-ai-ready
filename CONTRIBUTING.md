# Contributing

Thanks for helping improve RepoAIReady.

## Development workflow

1. Fork or branch from the default branch.
2. Keep changes focused and include tests when behavior changes.
3. Run the validation commands before opening a pull request:

```powershell
dotnet restore RepoAIReady.sln
dotnet build RepoAIReady.sln --configuration Release --no-restore
dotnet test RepoAIReady.sln --configuration Release --no-build
```

## Packaging checks

When changing CLI behavior, packaging metadata, or release configuration, also run:

```powershell
dotnet pack src\RepoAIReady\RepoAIReady.csproj --configuration Release --output artifacts\nupkg
$toolPath = Join-Path $env:TEMP "repo-ai-ready-tool-test"
dotnet tool install RepoAIReady --tool-path $toolPath --add-source artifacts\nupkg --no-cache
& (Join-Path $toolPath "repo-ai-ready") --help
Remove-Item -Recurse -Force $toolPath
```

## Pull requests

Describe the user-visible change, include validation results, and call out any changes to authentication, repository evidence collection, or generated report formats.
