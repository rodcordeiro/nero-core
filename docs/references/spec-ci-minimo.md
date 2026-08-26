# Spec: CI mínimo (GitHub Actions)

Issue: [#10](https://github.com/rodcordeiro/nero-core/issues/10)  
Roadmap: pós Clean Genesis (ADR 0003)  
Seam: workflow GitHub Actions → `dotnet restore` / `test` na solution MCP (exit code / logs)

## Problem Statement

After Clean Genesis, Nero has a private remote but no automated gate that proves Core still builds and tests on every change. Contributors and agents can merge or push regressions without a shared, feed-free CI signal.

## Solution

Add a minimal GitHub Actions workflow on pull requests and `main` that restores and tests `mcp/Nero.Knowledge.Base.sln` with public NuGet only—no corporate feeds, secrets, or Azure Pipelines—so the Core stays verifiable for friends sharing the private repo.

## User Stories

1. As a Nero maintainer, I want CI to run on every pull request, so that broken builds do not land on `main` unnoticed.
2. As a Nero maintainer, I want CI to run on pushes to `main`, so that direct commits are still verified.
3. As a contributor, I want a clear red/green check on the PR, so that I know whether Core still builds before merge.
4. As an AFK agent, I want a documented CI contract, so that I can treat a green check as evidence before claiming Done.
5. As a friend cloning the private repo, I want CI that uses only public NuGet, so that I am never prompted for employer feed credentials.
6. As a maintainer, I want restore and test of the MCP solution, so that the Core executable surface is covered.
7. As a maintainer, I want the workflow to fail the job when tests fail, so that a red check means “do not ship.”
8. As a maintainer, I want the workflow to fail when restore fails, so that missing packages are visible.
9. As a maintainer, I want CI logs readable in the Actions UI, so that I can diagnose failures without local reproduction first.
10. As a Windows-oriented Nero developer, I want CI on a runner that matches supported `dotnet` SDK expectations for .NET 8, so that results match local `dotnet test`.
11. As a maintainer, I want no required repository secrets for the minimal workflow, so that onboarding stays simple.
12. As a Clean Genesis steward, I want zero Azure DevOps or private feed references in the workflow, so that Upstream Divergence and hygiene stay intact.
13. As a maintainer, I want the workflow file versioned in this repo, so that CI is reviewable like any other change.
14. As a contributor, I want CI scoped to the MCP solution path, so that unrelated future folders do not silently expand the gate without intent.
15. As a maintainer, I want optional Release build later without blocking this slice, so that v0 CI stays small.
16. As an agent working the hub board, I want this ticket to remain `ready-for-agent` until green CI exists, so that triage stays accurate.
17. As a pack author (e.g. code-graph), I want Core CI independent of pack repos, so that pack boards do not own Nero Core health.
18. As a maintainer, I want failed CI to block merge only when branch protection is enabled later, so that this slice can ship workflow-first without forcing protection in the same change.
19. As a reviewer, I want the workflow to avoid uploading Knowledge Repo or personal Corpus artifacts, so that CI never treats Corpus as product input.
20. As a maintainer, I want publish/DLL staging out of this minimal CI, so that Windows file locks and publish paths stay a local concern.
21. As a friend, I want documentation in README pointing at Actions as the health signal, so that I know where to look after clone.
22. As a maintainer, I want the workflow name and job name stable enough to cite in issues, so that Gauntlet evidence can link a run URL.
23. As an agent, I want not to invent Azure Pipelines for this ticket, so that Out of Scope stays respected.
24. As a maintainer, I want caching of NuGet only if it does not pull private sources, so that speed does not reintroduce feed risk.
25. As a Clean Genesis steward, I want the workflow free of employer brand tokens, so that hygiene scans stay green.

## Implementation Decisions

- Add a single GitHub Actions workflow under the repo’s standard Actions path; trigger on `pull_request` and `push` to `main`.
- Use the official .NET setup action (or equivalent) pinned to SDK compatible with .NET 8 as used by Core.
- Commands: `dotnet restore` then `dotnet test` on `mcp/Nero.Knowledge.Base.sln` (Release or Debug consistent with local docs; prefer matching README’s test invocation).
- No `NuGet.config` private sources; no `NUGET_*` secrets; no Azure Pipelines YAML.
- Do not run MCP host over stdio in CI for this slice; tests are the gate.
- Do not publish to `mcp/publish` in CI.
- Branch protection / required checks are optional follow-up; not required to close this spec’s DoD.
- Document the workflow briefly in README (CI section or TL;DR pointer).

## Testing Decisions

- Good tests for this slice are observational: a workflow run that restores and tests successfully on a clean runner is the acceptance evidence—not new unit tests about YAML.
- Validate by opening a PR or pushing a no-op and confirming the Actions run is green.
- Negative check (manual or follow-up): temporarily breaking a known test should fail the job (optional smoke after green path).
- Prior art: local validation already uses `dotnet test .\mcp\Nero.Knowledge.Base.sln` (README / AGENTS).

## Out of Scope

- Azure DevOps pipelines
- Private NuGet feeds or credential providers
- Multi-OS matrix beyond one supported runner
- Deployment, Docker, release tagging
- Publishing the MCP DLL from CI
- Branch protection rules (may come later)
- Pack / Domain Skill CI
- Knowledge Repo validation as a required CI job (scaffold validate may be a later additive job)

## Further Notes

- ADR 0003 (Clean Genesis) and hygiene rules forbid corporate pipeline/feed residue.
- Ticket remains on Nero Scrum board (#14); default iteration **next** unless pulled to current.
- Spec path in repo: `docs/references/spec-ci-minimo.md`.
