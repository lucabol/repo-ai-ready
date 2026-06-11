using RepoAIReady.Cli;

namespace RepoAIReady.Tests;

public sealed class EnvFileParserTests
{
	[Fact]
	public void Parse_ReadsDotEnvSyntax()
	{
		var path = Path.Combine(Path.GetTempPath(), $"repoaiready-{Guid.NewGuid():N}.env");
		File.WriteAllLines(path,
		[
			"# comment",
			"GITHUB_TOKEN=from-env-file",
			"export REPOAI_MODEL=\"gpt-test\"",
			"OPENAI_ENDPOINT=https://example.test # inline comment"
		]);

		try
		{
			var values = EnvFileParser.Parse(new FileInfo(path));

			Assert.Equal("from-env-file", values["GITHUB_TOKEN"]);
			Assert.Equal("gpt-test", values["REPOAI_MODEL"]);
			Assert.Equal("https://example.test", values["OPENAI_ENDPOINT"]);
		}
		finally
		{
			File.Delete(path);
		}
	}
}
