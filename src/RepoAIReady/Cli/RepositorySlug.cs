namespace RepoAIReady.Cli;

public sealed record RepositorySlug(string Owner, string Name)
{
	public string FullName => $"{Owner}/{Name}";
	public string Slug => $"{Sanitize(Owner)}-{Sanitize(Name)}";

	public static RepositorySlug Parse(string value)
	{
		var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length != 2 || parts.Any(static p => p.Length == 0))
		{
			throw new UsageException($"Repository must be in org/repo form: {value}");
		}

		return new RepositorySlug(parts[0], parts[1]);
	}

	private static string Sanitize(string value)
	{
		var invalid = Path.GetInvalidFileNameChars().ToHashSet();
		return string.Concat(value.Select(ch => invalid.Contains(ch) ? '-' : char.ToLowerInvariant(ch)));
	}
}
