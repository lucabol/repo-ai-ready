using RepoAIReady.Cli;

namespace RepoAIReady.Tests;

public sealed class EnvFileParserTests
{
	[Fact]
	public void Parse_ReadsDotEnvSyntax()
	{
		var path = TestEnvPath();
		File.WriteAllLines(path,
		[
			"# comment",
			"GITHUB_TOKEN=from-env-file",
			"export REPOAI_MODEL=\"gpt-test\"",
			"OPENAI_ENDPOINT=https://example.test # inline comment",
			"OPENAI_FRAGMENT=https://example.test/#fragment"
		]);

		try
		{
			var values = EnvFileParser.Parse(new FileInfo(path));

			Assert.Equal("from-env-file", values["GITHUB_TOKEN"]);
			Assert.Equal("gpt-test", values["REPOAI_MODEL"]);
			Assert.Equal("https://example.test", values["OPENAI_ENDPOINT"]);
			Assert.Equal("https://example.test/#fragment", values["OPENAI_FRAGMENT"]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void Parse_StripsInlineCommentsAfterQuotedValues()
	{
		var path = TestEnvPath();
		File.WriteAllLines(path,
		[
			"DOUBLE_QUOTED=\"value\" # inline comment",
			"SINGLE_QUOTED='single value' # inline comment",
			"DOUBLE_QUOTED_WITH_HASH=\"value # not a comment\" # inline comment",
			"SINGLE_QUOTED_WITH_HASH='single # not a comment' # inline comment",
			"ESCAPED_DOUBLE_QUOTE=\"value \\\"#\\\"\" # inline comment"
		]);

		try
		{
			var values = EnvFileParser.Parse(new FileInfo(path));

			Assert.Equal("value", values["DOUBLE_QUOTED"]);
			Assert.Equal("single value", values["SINGLE_QUOTED"]);
			Assert.Equal("value # not a comment", values["DOUBLE_QUOTED_WITH_HASH"]);
			Assert.Equal("single # not a comment", values["SINGLE_QUOTED_WITH_HASH"]);
			Assert.Equal("value \"#\"", values["ESCAPED_DOUBLE_QUOTE"]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static string TestEnvPath()
	{
		var directory = Path.Combine(Directory.GetCurrentDirectory(), "repoaiready-env-tests");
		Directory.CreateDirectory(directory);
		return Path.Combine(directory, $"repoaiready-{Guid.NewGuid():N}.env");
	}
}
