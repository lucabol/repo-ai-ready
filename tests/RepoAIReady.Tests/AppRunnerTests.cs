using RepoAIReady;
using RepoAIReady.Agent;
using RepoAIReady.Cli;
using RepoAIReady.GitHub;
using RepoAIReady.Reporting;
using RepoAIReady.Rules;

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

	[Fact]
	public async Task EvaluateRepositoriesAsync_RunsWithBoundedParallelismAndPreservesOrder()
	{
		var repositories = new[]
		{
			new RepositorySlug("example", "one"),
			new RepositorySlug("example", "two"),
			new RepositorySlug("example", "three"),
			new RepositorySlug("example", "four")
		};
		var evidenceSource = new DelayedEvidenceSource();
		var judge = new AgentFrameworkRepoJudge(
			"# AI Readiness Repository Judge",
			new RuleBasedReadinessChatClient(new RuleBasedReadinessEvaluator()));

		var reports = await AppRunner.EvaluateRepositoriesAsync(
			repositories,
			evidenceSource,
			judge,
			maxParallelism: 2,
			updateStatus: null,
			CancellationToken.None);

		Assert.Equal(["example/one", "example/two", "example/three", "example/four"], reports.Select(report => report.Repo).ToArray());
		Assert.Equal(2, evidenceSource.MaxObservedConcurrency);
	}

	[Fact]
	public async Task EvaluateRepositoriesAsync_ReportsPerRepositoryStages()
	{
		var repositories = new[] { new RepositorySlug("example", "one") };
		var stages = new List<RepositoryEvaluationStage>();
		var judge = new AgentFrameworkRepoJudge(
			"# AI Readiness Repository Judge",
			new RuleBasedReadinessChatClient(new RuleBasedReadinessEvaluator()));

		await AppRunner.EvaluateRepositoriesAsync(
			repositories,
			new DelayedEvidenceSource(),
			judge,
			maxParallelism: 1,
			(index, stage) => stages.Add(stage),
			CancellationToken.None);

		Assert.Equal(
			[
				RepositoryEvaluationStage.CollectingEvidence,
				RepositoryEvaluationStage.Judging,
				RepositoryEvaluationStage.Done
			],
			stages);
	}

	[Fact]
	public async Task EvaluateRepositoriesBatchAsync_PreservesSuccessfulReportsWhenOneRepositoryFails()
	{
		var repositories = new[]
		{
			new RepositorySlug("example", "one"),
			new RepositorySlug("example", "broken"),
			new RepositorySlug("example", "two")
		};
		var stages = new List<(int Index, RepositoryEvaluationStage Stage)>();
		var judge = new AgentFrameworkRepoJudge(
			"# AI Readiness Repository Judge",
			new RuleBasedReadinessChatClient(new RuleBasedReadinessEvaluator()));

		var batch = await AppRunner.EvaluateRepositoriesBatchAsync(
			repositories,
			new FailingEvidenceSource("example/broken"),
			judge,
			maxParallelism: 1,
			(index, stage) => stages.Add((index, stage)),
			CancellationToken.None);

		Assert.Equal(["example/one", "example/two"], batch.Reports.Select(report => report.Repo).ToArray());
		var failure = Assert.Single(batch.Failures);
		Assert.Equal("example/broken", failure.Repo);
		Assert.Equal("Processing evidence...", failure.Stage);
		Assert.Equal(5, failure.ExitCode);
		Assert.Equal(nameof(InvalidOperationException), failure.ErrorType);
		Assert.Contains("boom", failure.Message, StringComparison.Ordinal);

		Assert.Equal(
			[
				RepositoryEvaluationStage.CollectingEvidence,
				RepositoryEvaluationStage.Judging,
				RepositoryEvaluationStage.Done
			],
			stages.Where(item => item.Index == 0).Select(item => item.Stage).ToArray());
		Assert.Equal(
			[
				RepositoryEvaluationStage.CollectingEvidence,
				RepositoryEvaluationStage.Failed
			],
			stages.Where(item => item.Index == 1).Select(item => item.Stage).ToArray());
		Assert.Equal(
			[
				RepositoryEvaluationStage.CollectingEvidence,
				RepositoryEvaluationStage.Judging,
				RepositoryEvaluationStage.Done
			],
			stages.Where(item => item.Index == 2).Select(item => item.Stage).ToArray());
	}

	[Fact]
	public void DetermineExitCode_ReturnsFailureCodeWhenAnyRepositoryFails()
	{
		var batch = new RepositoryEvaluationBatch(
			[ReportFor("example/ready", overallScore: 100)],
			[new RepositoryEvaluationFailure("example/missing", "Processing evidence...", 4, "NotFoundException", "not found")]);

		var exitCode = AppRunner.DetermineExitCode(batch, minScore: 100);

		Assert.Equal(4, exitCode);
	}

	[Fact]
	public void StageMarkup_UsesDistinctLabelsForWorkStates()
	{
		Assert.Contains("Processing evidence", AppRunner.StageMarkup(RepositoryEvaluationStage.CollectingEvidence));
		Assert.Contains("Judging", AppRunner.StageMarkup(RepositoryEvaluationStage.Judging));
		Assert.Contains("Done", AppRunner.StageMarkup(RepositoryEvaluationStage.Done));
	}

	[Fact]
	public void StageText_UsesPlainTextLabelsForRedirectedOutputFallback()
	{
		Assert.Equal("Processing evidence...", AppRunner.StageText(RepositoryEvaluationStage.CollectingEvidence));
		Assert.Equal("Judging...", AppRunner.StageText(RepositoryEvaluationStage.Judging));
		Assert.Equal("Done", AppRunner.StageText(RepositoryEvaluationStage.Done));
	}

	private sealed class DelayedEvidenceSource : IRepositoryEvidenceSource
	{
		private int _currentConcurrency;
		private int _maxObservedConcurrency;

		public int MaxObservedConcurrency => _maxObservedConcurrency;

		public async Task<CollectedRepositoryEvidence> CollectAsync(RepositorySlug repository, CancellationToken cancellationToken)
		{
			var current = Interlocked.Increment(ref _currentConcurrency);
			UpdateMaxObserved(current);

			try
			{
				await Task.Delay(100, cancellationToken);
				return EvidenceFor(repository);
			}
			finally
			{
				Interlocked.Decrement(ref _currentConcurrency);
			}
		}

		private void UpdateMaxObserved(int current)
		{
			while (true)
			{
				var observed = _maxObservedConcurrency;
				if (current <= observed || Interlocked.CompareExchange(ref _maxObservedConcurrency, current, observed) == observed)
				{
					return;
				}
			}
		}

		private static CollectedRepositoryEvidence EvidenceFor(RepositorySlug repository)
		{
			var metadata = new RepositoryMetadata(
				repository.FullName,
				"Test repository",
				"main",
				$"https://github.com/{repository.FullName}",
				"C#",
				IsPrivate: false,
				DateTimeOffset.UtcNow);
			var files = new[]
			{
				new EvidenceFile("README.md", "file", "# Test", $"https://github.com/{repository.FullName}/blob/main/README.md", "sha", Truncated: false)
			};

			return new CollectedRepositoryEvidence(repository, metadata, files, []);
		}
	}

	private sealed class FailingEvidenceSource(string failingRepository) : IRepositoryEvidenceSource
	{
		public Task<CollectedRepositoryEvidence> CollectAsync(RepositorySlug repository, CancellationToken cancellationToken)
		{
			if (repository.FullName.Equals(failingRepository, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("boom while collecting evidence");
			}

			return Task.FromResult(EvidenceFor(repository));
		}
	}

	private static CollectedRepositoryEvidence EvidenceFor(RepositorySlug repository)
	{
		var metadata = new RepositoryMetadata(
			repository.FullName,
			"Test repository",
			"main",
			$"https://github.com/{repository.FullName}",
			"C#",
			IsPrivate: false,
			DateTimeOffset.UtcNow);
		var files = new[]
		{
			new EvidenceFile("README.md", "file", "# Test", $"https://github.com/{repository.FullName}/blob/main/README.md", "sha", Truncated: false)
		};

		return new CollectedRepositoryEvidence(repository, metadata, files, []);
	}

	private static AiReadinessReport ReportFor(string repo, int overallScore) =>
		new(
			repo,
			"test",
			overallScore,
			new(
				Score(20),
				Score(20),
				Score(20),
				Score(20),
				Score(20)),
			[],
			[],
			[]);

	private static FundamentalScore Score(int score) =>
		new(score, [], []);
}
