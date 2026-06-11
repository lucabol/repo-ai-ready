namespace RepoAIReady.Agent;

public sealed class CopilotBackendException(string message, Exception? innerException = null)
	: Exception(message, innerException);
