namespace RepoAIReady.Tests;

public sealed class JudgeRubricTests
{
	[Fact]
	public void Rubric_InstructsConciseMachineReadableStrings()
	{
		var rubric = File.ReadAllText(FindRubricPath());

		Assert.Contains("### Concise machine-readable strings", rubric, StringComparison.Ordinal);
		Assert.Contains("Do not insert manual line breaks", rubric, StringComparison.Ordinal);
		Assert.Contains("Never truncate with ellipses", rubric, StringComparison.Ordinal);
		Assert.Contains("top_strengths", rubric, StringComparison.Ordinal);
		Assert.Contains("highest_impact_improvements", rubric, StringComparison.Ordinal);
		Assert.Contains("fundamentals.*.evidence", rubric, StringComparison.Ordinal);
		Assert.Contains("fundamentals.*.gaps", rubric, StringComparison.Ordinal);
		Assert.Contains("uncertainties", rubric, StringComparison.Ordinal);
	}

	private static string FindRubricPath()
	{
		foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
		{
			var directory = new DirectoryInfo(start);
			while (directory is not null)
			{
				var path = Path.Combine(directory.FullName, "ai-readiness-llm-judge.md");
				if (File.Exists(path))
				{
					return path;
				}

				directory = directory.Parent;
			}
		}

		throw new FileNotFoundException("Could not find ai-readiness-llm-judge.md in the repository tree.");
	}
}
