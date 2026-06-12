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
		if (TryReadQuotedValue(value, '"', out var doubleQuoted))
		{
			return doubleQuoted
				.Replace("\\n", "\n", StringComparison.Ordinal)
				.Replace("\\r", "\r", StringComparison.Ordinal)
				.Replace("\\t", "\t", StringComparison.Ordinal)
				.Replace("\\\"", "\"", StringComparison.Ordinal)
				.Replace("\\\\", "\\", StringComparison.Ordinal);
		}

		if (TryReadQuotedValue(value, '\'', out var singleQuoted))
		{
			return singleQuoted;
		}

		return StripInlineComment(value).Trim();
	}

	private static bool TryReadQuotedValue(string value, char quote, out string parsed)
	{
		parsed = string.Empty;
		if (value.Length < 2 || value[0] != quote)
		{
			return false;
		}

		var closingQuoteIndex = FindClosingQuote(value, quote);
		if (closingQuoteIndex <= 0)
		{
			return false;
		}

		var trailing = value[(closingQuoteIndex + 1)..].TrimStart();
		if (trailing.Length > 0 && trailing[0] != '#')
		{
			return false;
		}

		parsed = value[1..closingQuoteIndex];
		return true;
	}

	private static int FindClosingQuote(string value, char quote)
	{
		for (var i = 1; i < value.Length; i++)
		{
			if (value[i] == quote && (quote != '"' || !IsEscaped(value, i)))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsEscaped(string value, int index)
	{
		var backslashCount = 0;
		for (var i = index - 1; i >= 0 && value[i] == '\\'; i--)
		{
			backslashCount++;
		}

		return backslashCount % 2 == 1;
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
