using RepoAIReady.GitHub;

namespace RepoAIReady.Agent;

public sealed record AgentPromptPayload(string Rubric, CollectedRepositoryEvidence Evidence);
