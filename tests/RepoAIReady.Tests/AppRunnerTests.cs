using RepoAIReady;

namespace RepoAIReady.Tests;

public sealed class AppRunnerTests
{
	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	[InlineData("-?")]
	public async Task RunAsync_PrintsHelpAndReturnsSuccess(string argument)
	{
		var output = new StringWriter();
		var originalOut = Console.Out;
		try
		{
			Console.SetOut(output);

			var exitCode = await AppRunner.RunAsync([argument], CancellationToken.None);

			Assert.Equal(0, exitCode);
			Assert.Contains("repo-ai-ready", output.ToString());
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}

	[Fact]
	public async Task RunAsync_PrintsVersionAndReturnsSuccess()
	{
		var output = new StringWriter();
		var originalOut = Console.Out;
		try
		{
			Console.SetOut(output);

			var exitCode = await AppRunner.RunAsync(["--version"], CancellationToken.None);

			Assert.Equal(0, exitCode);
			Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}
}
