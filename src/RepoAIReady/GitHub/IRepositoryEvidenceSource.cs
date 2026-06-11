using RepoAIReady.Cli;

namespace RepoAIReady.GitHub;

public interface IRepositoryEvidenceSource
{
	Task<CollectedRepositoryEvidence> CollectAsync(RepositorySlug repository, CancellationToken cancellationToken);
}
