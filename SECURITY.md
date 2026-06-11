# Security policy

## Reporting a vulnerability

Please report security issues privately by opening a GitHub security advisory in this repository or by contacting the repository owner directly.

Do not include tokens, private repository contents, or other secrets in public issues.

## Token handling

RepoAIReady reads GitHub, Copilot, and OpenAI-compatible tokens from command-line options or environment variables. Tokens are used for outbound API calls only and should not be committed to source control. Use `.env.example` as a template and keep local `.env` files private.
