namespace RepoAIReady.Cli;

public static class AppEnvironment
{
	private const string DefaultEnvFileName = ".env";

	private static readonly string[] SupportedNames =
	[
		"GITHUB_TOKEN",
		"GH_TOKEN",
		"COPILOT_TOKEN",
		"GITHUB_COPILOT_TOKEN",
		"OPENAI_API_KEY",
		"OPENAI_ENDPOINT",
		"REPOAI_MODEL"
	];

	public static IReadOnlyDictionary<string, string> Load(IReadOnlyList<string> args, DirectoryInfo currentDirectory)
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var envFile = ResolveEnvFile(args, currentDirectory, out var explicitEnvFile);

		if (envFile.Exists)
		{
			foreach (var pair in EnvFileParser.Parse(envFile))
			{
				values[pair.Key] = pair.Value;
			}
		}
		else if (explicitEnvFile)
		{
			throw new UsageException($"Environment file not found: {envFile.FullName}");
		}

		foreach (var name in SupportedNames)
		{
			var value = Environment.GetEnvironmentVariable(name);
			if (value is not null)
			{
				values[name] = value;
			}
		}

		return values;
	}

	public static string? GetValue(this IReadOnlyDictionary<string, string> values, string name) =>
		values.TryGetValue(name, out var value) ? value : null;

	private static FileInfo ResolveEnvFile(IReadOnlyList<string> args, DirectoryInfo currentDirectory, out bool explicitEnvFile)
	{
		explicitEnvFile = false;
		for (var i = 0; i < args.Count; i++)
		{
			if (args[i] is not "--env-file")
			{
				continue;
			}

			explicitEnvFile = true;
			var path = ReadValue(args, i, "--env-file");
			return new FileInfo(Path.IsPathRooted(path) ? path : Path.Combine(currentDirectory.FullName, path));
		}

		return new FileInfo(Path.Combine(currentDirectory.FullName, DefaultEnvFileName));
	}

	private static string ReadValue(IReadOnlyList<string> args, int index, string option)
	{
		if (index + 1 >= args.Count)
		{
			throw new UsageException($"{option} requires a value.");
		}

		return args[index + 1];
	}
}
