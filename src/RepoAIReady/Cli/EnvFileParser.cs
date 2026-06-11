namespace RepoAIReady.Cli;

public static class EnvFileParser
{
	public static IReadOnlyDictionary<string, string> Parse(FileInfo file)
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var lineNumber = 0;

		foreach (var rawLine in File.ReadLines(file.FullName))
		{
			lineNumber++;
			var line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
			{
				continue;
			}

			if (line.StartsWith("export ", StringComparison.Ordinal))
			{
				line = line["export ".Length..].TrimStart();
			}

			var equalsIndex = line.IndexOf('=');
			if (equalsIndex <= 0)
			{
				throw new UsageException($"Invalid .env entry at {file.FullName}:{lineNumber}. Expected KEY=value.");
			}

			var key = line[..equalsIndex].Trim();
			if (key.Length == 0 || key.Any(char.IsWhiteSpace))
			{
				throw new UsageException($"Invalid .env key at {file.FullName}:{lineNumber}.");
			}

			values[key] = ParseValue(line[(equalsIndex + 1)..]);
		}

		return values;
	}

	private static string ParseValue(string value)
	{
		value = value.TrimStart();
		if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
		{
			return value[1..^1]
				.Replace("\\n", "\n", StringComparison.Ordinal)
				.Replace("\\r", "\r", StringComparison.Ordinal)
				.Replace("\\t", "\t", StringComparison.Ordinal)
				.Replace("\\\"", "\"", StringComparison.Ordinal)
				.Replace("\\\\", "\\", StringComparison.Ordinal);
		}

		if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
		{
			return value[1..^1];
		}

		return StripInlineComment(value).Trim();
	}

	private static string StripInlineComment(string value)
	{
		for (var i = 0; i < value.Length; i++)
		{
			if (value[i] == '#' && (i == 0 || char.IsWhiteSpace(value[i - 1])))
			{
				return value[..i];
			}
		}

		return value;
	}
}
