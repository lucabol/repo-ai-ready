# AI Readiness Repository Judge

## Purpose

Use this document to evaluate whether a repository has the baseline configuration needed for AI coding agents to work effectively. Judge only baseline AI readiness: documentation, validation, tests, build infrastructure, and AI context. Do not score advanced topics such as custom observability, enterprise governance, or deep AI extensibility unless they directly support the baseline.

## Core Definition

A repository is AI-ready when an AI coding agent can:

1. Understand the project purpose, structure, conventions, and contribution workflow.
2. Identify the correct scope for a requested change.
3. Make changes that follow local coding standards.
4. Run validation commands and interpret failures.
5. Run relevant tests without guessing.
6. Build the changed project reliably in a reproducible environment.
7. Use repository-specific AI instructions and context rather than generic assumptions.

## Required Evaluation Process

1. Classify the repository type.
2. Inspect evidence for each readiness fundamental.
3. Adjust expectations based on repository type.
4. Score each fundamental from 0 to 20.
5. Produce an overall score from 0 to 100.
6. Provide concrete, prioritized improvement suggestions.

Never award points for files that merely exist by name. The evidence must be usable by an AI agent.

## Repository Type Classification

Classify the repository as exactly one of the following:

| Type | Definition | Readiness expectation |
|---|---|---|
| `single_repo` | One cohesive project or application. | Repo-wide docs, tests, build, validation, and instructions may be sufficient. |
| `monorepo` | Multiple explicit sub-projects in one repo, usually indicated by workspace, solution, project, or package files. | Each important sub-project should have scoped docs, tests, build commands, validation commands, and instructions. |
| `large_repo` | A large codebase where boundaries are inferred from directories, naming, or architectural layers rather than explicit project files. | Each major area should have scoped docs, tests, validation guidance, and path-specific AI context. |

### Signals for Classification

Use these signals:

- Top-level directories and their names.
- Workspace or solution files, for example `.sln`, `.csproj`, `package.json` workspaces, `pnpm-workspace.yaml`, `go.work`, `Cargo.toml`.
- Multiple apps, packages, services, extensions, or libraries.
- Area-specific test folders, build files, or README files.
- Size and complexity of source layout.

## Scoring Summary

| Fundamental | Weight |
|---|---:|
| Documentation | 20 |
| Style and Validation | 20 |
| Testing | 20 |
| Build Infrastructure | 20 |
| AI Context | 20 |
| **Total** | **100** |

Use integer scores. If evidence is unavailable or inaccessible, score conservatively and state the uncertainty.

## Maturity Levels

Apply these levels independently to each fundamental:

| Level | Score range | Meaning |
|---|---:|---|
| Missing | 0-4 | Little or no usable evidence. Agents must guess. |
| Minimal | 5-9 | Some evidence exists, but it is incomplete, stale, ambiguous, or not scoped. |
| Functional | 10-14 | Baseline exists and works for common changes, but gaps remain for scope, speed, or consistency. |
| Strong | 15-18 | Good coverage, automated feedback loops, and clear scoped guidance for most changes. |
| Excellent | 19-20 | Comprehensive, current, scoped, reproducible, and directly actionable by agents. |

## Fundamental 1: Documentation

### What to judge

Documentation gives agents context about project purpose, architecture, design decisions, contribution workflows, configuration, and local conventions.

### Strong evidence

- Root `README.md` explains what the project is and how it is organized.
- Contributing guide explains development workflow, pull request expectations, and local setup.
- Architecture or design docs explain important layers, services, modules, boundaries, or data flows.
- Configuration docs explain required environment variables, tools, secrets handling, or local dependencies.
- Area-level READMEs exist for monorepos and large repos.
- Docs are versioned in the repo or clearly linked from stable repo files.

### Weak evidence

- README only describes product marketing, not development.
- Important docs exist only in external sites with no repo-local summary.
- Docs are stale, incomplete, or conflict with build/test scripts.
- Large repos have only a root README and no area-level context.

### Scoring guidance

- `0-4`: No useful development documentation.
- `5-9`: Basic README exists but lacks architecture, setup, or contribution details.
- `10-14`: Root documentation is useful, but scoped docs are missing or inconsistent.
- `15-18`: Strong root docs plus meaningful area or project docs.
- `19-20`: Comprehensive, current, scoped, and directly actionable for agents.

## Fundamental 2: Style and Validation

### What to judge

Style and validation tools automatically catch formatting issues, lint violations, type errors, and coding standard deviations.

### Strong evidence

- Formatting configuration, for example `.editorconfig`, Prettier, Biome, dotnet format, Black, rustfmt, gofmt.
- Linting configuration and scripts, for example ESLint, Ruff, golangci-lint, Clippy, Stylelint, analyzers.
- Type checking or static analysis, for example TypeScript `tsconfig.json`, C# compiler/analyzers, mypy, Pyright.
- Validation commands are documented and runnable locally.
- CI runs the same validation on pull requests.
- Different sub-projects or areas have appropriately scoped validation.

### Weak evidence

- Tools exist but no script or documentation tells agents how to run them.
- Validation is only manual or editor-specific.
- Linting is present for one language but missing for other important languages.
- Formatting is not enforced.
- Validation commands are too broad, slow, or flaky without scoped alternatives.

### Scoring guidance

- `0-4`: No automated style, lint, format, or type checks.
- `5-9`: Some tools exist but are incomplete or not wired into scripts/CI.
- `10-14`: Baseline validation exists for major languages but has coverage or scoping gaps.
- `15-18`: Strong validation across major areas with documented commands.
- `19-20`: Comprehensive, scoped, fast enough for agents, and enforced in CI.

## Fundamental 3: Testing

### What to judge

Tests give agents a feedback loop to verify behavior after changes.

### Strong evidence

- Test suites exist for important application, library, service, or package areas.
- Root or project-level test scripts are defined.
- Tests are documented with commands and filtering options.
- Unit tests, integration tests, and end-to-end tests exist where appropriate.
- CI runs relevant tests on pull requests.
- Monorepos and large repos provide per-project or per-area tests.
- Test placement and naming conventions are clear.

### Weak evidence

- Test files exist but there is no clear way to run them.
- Only a full test suite is available and it is too slow for targeted agent feedback.
- Tests cover only a small or unrelated portion of the repo.
- CI tests exist but local commands are undocumented.
- Test guidance does not explain which tests correspond to which changed paths.

### Scoring guidance

- `0-4`: No meaningful tests or no runnable test command.
- `5-9`: Limited tests or unclear commands.
- `10-14`: Useful tests exist, but scoped execution or coverage is incomplete.
- `15-18`: Good test coverage and clear local/CI commands for most changes.
- `19-20`: Comprehensive, scoped, reliable, and well-documented tests.

## Fundamental 4: Build Infrastructure

### What to judge

Build infrastructure lets agents compile, package, validate dependencies, and rely on consistent automation.

### Strong evidence

- Build scripts compile the project and resolve imports.
- CI pipelines run build, lint, typecheck, test, and security-related checks on pull requests.
- Dependency versions are pinned or locked, for example `package-lock.json`, `pnpm-lock.yaml`, `yarn.lock`, `packages.lock.json`, `go.sum`, `Cargo.lock`.
- Runtime/tool versions are pinned, for example `.nvmrc`, `global.json`, `.tool-versions`, Dockerfile, devcontainer.
- Setup commands are documented and reproducible.
- Monorepos and large repos have per-project or per-area build scripts.
- Build artifacts and generated files are clearly identified.

### Weak evidence

- Build command is missing, unclear, or only implied by framework conventions.
- CI does not run on pull requests.
- Dependencies are floating or lockfiles are missing.
- Setup depends on undocumented local state.
- Full build is too expensive and no scoped build is documented.

### Scoring guidance

- `0-4`: No reliable build or dependency setup.
- `5-9`: Basic build exists but is poorly documented or not reproducible.
- `10-14`: Build and dependency management work for common paths but have CI/scoping gaps.
- `15-18`: Strong build, CI, and reproducible setup for most areas.
- `19-20`: Comprehensive, reproducible, automated, and scoped build infrastructure.

## Fundamental 5: AI Context

### What to judge

AI context gives coding agents repository-specific guidance so they can work without relying on generic assumptions.

### Strong evidence

- Repository-wide `.github/copilot-instructions.md` exists.
- Instructions include architecture, naming, coding standards, preferred libraries, and validation commands.
- Path-specific instructions exist under `.github/instructions/*.instructions.md` for monorepo or large-repo areas.
- Instructions are scoped using clear `applyTo` patterns when supported.
- Instructions tell agents how to build, test, lint, typecheck, and format changes.
- Instructions identify local pitfalls, generated files, localization rules, security-sensitive areas, or ownership boundaries.
- Additional context exists where useful, such as prompts, skills, custom agents, MCP configuration, or setup steps.

### Weak evidence

- No Copilot or agent instruction file.
- Instructions are generic and could apply to any repo.
- Instructions omit validation commands.
- One root instruction file tries to cover a large repo without path-specific guidance.
- AI context conflicts with actual scripts or docs.
- Instructions are too long, noisy, or unscoped for agents to use effectively.

### Scoring guidance

- `0-4`: No AI-specific context.
- `5-9`: Basic repo-wide instructions exist but are generic or incomplete.
- `10-14`: Useful instructions exist, but scope or validation guidance is incomplete.
- `15-18`: Strong repo-wide and scoped instructions for most important areas.
- `19-20`: Comprehensive, current, scoped, command-rich, and optimized for agent workflows.

## Repository-Type Adjustments

### Single repo

Do not penalize for lack of area-level docs or instructions if the project is genuinely small and cohesive. A single root README, test command, build command, validation command, and instruction file can be enough.

### Monorepo

Penalize if sub-projects lack their own:

- Purpose and setup documentation.
- Build scripts.
- Test scripts.
- Validation commands.
- Path-specific AI instructions.

Reward clear boundaries and workspace metadata that help agents scope work.

### Large repo

Penalize if agents must infer everything from folder names. Reward:

- Area-level READMEs.
- Architecture maps.
- Scoped tests and validation commands.
- Path-specific instructions for major areas.
- Clear ownership, layering, dependency, or contribution rules.

## Evidence Checklist

When judging a repo, inspect these common paths and equivalents:

```text
README.md
CONTRIBUTING.md
docs/
architecture/
.github/copilot-instructions.md
.github/instructions/*.instructions.md
.github/workflows/
.github/dependabot.yml
.devcontainer/
Dockerfile
package.json
package-lock.json
pnpm-lock.yaml
yarn.lock
tsconfig.json
.editorconfig
eslint.config.*
.prettierrc*
biome.json
pyproject.toml
requirements*.txt
Pipfile.lock
poetry.lock
go.mod
go.sum
Cargo.toml
Cargo.lock
*.sln
*.csproj
global.json
src/**/README.md
test/
tests/
__tests__/
```

This checklist is not exhaustive. Judge by function, not file names.

## Common Penalties

Apply these penalties within the relevant fundamental:

- File exists but content is generic: low or no credit.
- Commands exist but are undocumented: partial credit only.
- CI exists but does not run validation relevant to code changes: partial credit only.
- Large repo has only root-level guidance: penalize documentation, testing, validation, and AI context.
- Dependency lockfiles are missing for package-managed projects: penalize build infrastructure.
- Tests cannot be run locally or are too slow without filters: penalize testing.
- Instructions omit validation commands: penalize AI context.
- Repo relies on external docs with no local summary: penalize documentation.

## Output Format

Return the evaluation in both human-readable and machine-readable form.

### Human-readable format

```markdown
# AI Readiness Evaluation: <repo>

Overall score: <0-100>/100
Repository type: <single_repo | monorepo | large_repo>

| Fundamental | Score | Assessment |
|---|---:|---|
| Documentation | <0-20> | <short evidence-based assessment> |
| Style and Validation | <0-20> | <short evidence-based assessment> |
| Testing | <0-20> | <short evidence-based assessment> |
| Build Infrastructure | <0-20> | <short evidence-based assessment> |
| AI Context | <0-20> | <short evidence-based assessment> |

## Top strengths

1. <strength with evidence>
2. <strength with evidence>
3. <strength with evidence>

## Highest-impact improvements

1. <specific improvement>
2. <specific improvement>
3. <specific improvement>

## Evidence inspected

- `<path>`: <why it matters>
- `<path>`: <why it matters>
```

### Machine-readable format

```json
{
  "repo": "<owner/name or path>",
  "repository_type": "single_repo | monorepo | large_repo",
  "overall_score": 0,
  "fundamentals": {
    "documentation": {
      "score": 0,
      "evidence": ["<path or finding>"],
      "gaps": ["<gap>"]
    },
    "style_and_validation": {
      "score": 0,
      "evidence": ["<path or finding>"],
      "gaps": ["<gap>"]
    },
    "testing": {
      "score": 0,
      "evidence": ["<path or finding>"],
      "gaps": ["<gap>"]
    },
    "build_infrastructure": {
      "score": 0,
      "evidence": ["<path or finding>"],
      "gaps": ["<gap>"]
    },
    "ai_context": {
      "score": 0,
      "evidence": ["<path or finding>"],
      "gaps": ["<gap>"]
    }
  },
  "top_strengths": ["<strength>"],
  "highest_impact_improvements": ["<improvement>"],
  "uncertainties": ["<uncertainty>"]
}
```

### Concise machine-readable strings

The JSON report is rendered in console tables and lists, so keep each string concise and self-contained. Do not insert manual line breaks for terminal width; return plain single-line strings and let renderers wrap them. Never truncate with ellipses.

Use these target lengths unless a slightly longer item is needed to preserve important evidence:

- `top_strengths`: one evidence-backed sentence per item, target 100-120 characters.
- `highest_impact_improvements`: one concrete action per item, target 100-140 characters.
- `fundamentals.*.evidence`: one short fact or cited path per item, target 80-120 characters.
- `fundamentals.*.gaps`: one short missing capability or action per item, target 80-120 characters.
- `uncertainties`: one specific uncertainty per item, target 100-140 characters.

Prefer splitting unrelated facts into separate items over creating a long compound sentence.

## Judge Behavior Rules

- Be evidence-based and cite paths.
- Prefer concrete commands, files, and workflows over general impressions.
- Score conservatively when evidence is absent.
- Do not assume a framework default is present unless the repo exposes it through files or scripts.
- Distinguish "has a tool" from "agents can use the tool effectively."
- For monorepos and large repos, always assess scoped readiness, not just root readiness.
- Suggestions must be actionable and tied to the lowest-scoring gaps.
- Avoid recommending advanced AI tooling before baseline docs, validation, tests, build, and instructions are in place.
