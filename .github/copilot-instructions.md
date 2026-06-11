# RepoAIReady instructions

RepoAIReady is a .NET CLI tool. Keep changes small, type-safe, and covered by tests when behavior changes.

Use these validation commands before finishing code changes:

```powershell
dotnet restore RepoAIReady.sln
dotnet build RepoAIReady.sln --configuration Release --no-restore
dotnet test RepoAIReady.sln --configuration Release --no-build
```

When changing packaging or CLI startup behavior, also validate:

```powershell
dotnet pack src\RepoAIReady\RepoAIReady.csproj --configuration Release --output artifacts\nupkg
dotnet tool install RepoAIReady --tool-path <temp-dir> --add-source artifacts\nupkg --no-cache
```
