# Repository Guidelines - PowerShell

Fonte canonica na skill `$nero`: `references/guidelines/powershell-guidelines.md`.

Use for PowerShell modules, standalone script collections, Windows automation repositories, and mixed `.ps1` / `.psm1` / `.psd1` workspaces.

## Project Structure & Module Organization

Preserve the checkout structure before introducing a new layout. In module repositories, treat the module manifest (`*.psd1`) and root module (`*.psm1`) as the public packaging boundary. Public commands should live in explicit public folders such as `Public/Functions`, `Public/Authoral`, `Public/Imported`, or the repository's established equivalent. Private helpers, constants, classes, API wrappers, credential access, and shared implementation details should stay under `Private/`.

For standalone script repositories, distinguish active scripts from references, examples, generated exports, logs, and legacy snippets. A folder such as `scripts/` should be treated as the operational surface; a folder such as `references/` or `referencies/` should be treated as source material unless the checkout documents otherwise.

Do not move a function between module, public, private, script, or reference areas just to match this guideline. Document the real layout in `.agents/references/structure.md`; record divergence as debt only when it blocks safe maintenance.

## PowerShell Domain Rules

- Keep public functions small and command-shaped: parameter binding, validation, pipeline behavior, help, and calls into private helpers.
- Put reusable implementation in private helpers instead of duplicating it across public commands.
- Prefer approved verb-noun command names and keep filenames aligned with exported function names.
- Do not export everything by wildcard unless the repository already depends on that behavior; when changing exports, preserve compatibility or document the breaking change.
- Treat `.psd1` metadata as release-sensitive: version, `RootModule`, `RequiredModules`, `NestedModules`, `FunctionsToExport`, `HelpInfoURI`, `ProjectUri`, and tags affect install, publish, and discovery.
- Treat scripts that modify registry, services, scheduled tasks, VPN, network shares, disk cleanup, package installation, GitHub settings, or user profiles as machine-affecting operations.
- Use `SupportsShouldProcess`, `-WhatIf`, `-Confirm`, and clear `ConfirmImpact` for destructive or broad file/system changes.
- Resolve paths with `-LiteralPath` when user input or spaces are possible. Validate recursive delete/move/copy roots before acting.
- Never log or commit tokens, credentials, API keys, bearer values, vault secrets, secure strings, personal IDs, or machine-specific connection data.
- Use `SecretManagement`, `SecretStore`, Windows Credential Manager, environment variables, or CI secrets for credentials; never hardcode real values.
- Do not make network/API calls at module import time. Keep external calls inside commands or explicit initialization functions.
- Preserve comment-based help for public commands and update generated docs/help XML when the repo uses them.

## Module Defaults

- Keep `*.psm1` import code deterministic: dot-source public/private files, load optional assemblies with explicit error handling, and fail loudly when import leaves the module in a broken state.
- Export only public functions from the public command folders when practical.
- Keep private helpers non-exported; call them from public commands through the module scope.
- Validate `RequiredModules` before relying on local modules in CI or release workflows.
- For API modules, keep URI construction, headers, authentication, retries, error mapping, and JSON conversion in a private client/helper layer.
- For secret-backed API modules, keep secret names and vault names stable, but never include real secret values in examples or tests.

## Script Collection Defaults

- Put runnable scripts in an active folder such as `scripts/`; keep examples and copied references separate.
- Add comment-based help to runnable scripts so `Get-Help <script>` explains purpose, parameters, examples, side effects, and rollback considerations.
- Prefer functions with `CmdletBinding()` over top-level imperative scripts when the script may be reused.
- For machine-affecting scripts, require an explicit dry-run path (`-WhatIf`, `-WhatIf`-compatible commands, or documented inspection mode).
- Avoid hidden assumptions about current directory, administrator rights, execution policy, shell profile, UI availability, installed modules, or interactive prompts.
- Mark Windows-only, admin-only, GUI-only, or network-dependent scripts clearly in docs or help.

## Build, Test, and Development Commands

Prefer the repository's existing workflow. Common validation commands:

- `Invoke-ScriptAnalyzer -Path . -Recurse` for linting.
- `Invoke-ScriptAnalyzer -Path . -Recurse -OutVariable issues` with failures on `Severity -eq 'Error'` when matching the observed GitHub workflows.
- `Import-Module .\ModuleName.psd1 -Force -Verbose` for module import smoke tests.
- `Get-Command -Module ModuleName` to confirm exported commands.
- `Get-Help <Command> -Full` to verify public command help.
- `Test-ModuleManifest .\ModuleName.psd1` for manifest validation.
- `Invoke-Pester` when a Pester suite exists.
- `Publish-Module -Name .\ModuleName.psd1` only in release automation and only with CI secrets.

Do not install modules globally or trust PSGallery as part of ordinary inspection unless validation explicitly requires it. If dependency installation is needed, call it out as environment-affecting.

## Coding Style & Naming Conventions

Follow the local style first. Prefer PascalCase for function names, parameters, and type-like names. Use approved verbs when adding public commands. Prefer singular responsibility per public command. Avoid aliases in scripts that may run in CI or on another machine. Use named parameters for clarity in machine-affecting operations.

Use structured output objects for automation-friendly commands. Use `Write-Verbose`, `Write-Warning`, `Write-Error`, and `throw` intentionally; avoid `Write-Host` except for intentionally interactive UX. Return objects with `-PassThru` when the command mutates state and callers need a summary.

## Testing Guidelines

Validation should match risk:

- Public command behavior: Pester tests or targeted import/help/export smoke tests.
- Manifest changes: `Test-ModuleManifest` and module import.
- API wrappers: mock external calls where possible; sanitize examples and avoid real credentials.
- File cleanup, registry, service, task scheduler, VPN, installer, or profile scripts: dry-run validation first, then explicit scoped smoke tests only on a safe target.
- Docs/help changes: verify command help and generated docs stay aligned.

If tests cannot be run because dependencies, secrets, admin rights, Windows APIs, or external services are unavailable, state the blocker and provide inspection evidence.

## Commit & Pull Request Guidelines

Call out module version impact, exported command changes, required module changes, release/publish impact, machine-affecting behavior, and rollback. For script collections, list affected scripts and whether each script is active, reference-only, generated, or legacy.

After updating a PowerShell repository, suggest a focused commit message and the strongest validation command that was or should be run.

## Security & Configuration Tips

Review secrets, vault usage, local profile assumptions, CI secrets, publishing tokens, network endpoints, generated logs, and examples before committing. Do not store real credentials in docs, tests, examples, comments, `.ps1`, `.psm1`, `.psd1`, workflow YAML, logs, or generated help.

For recursive filesystem operations, validate the resolved absolute target and block system roots, user profile roots, program folders, Windows folders, and broad drive roots unless the command's explicit purpose and confirmation model covers them.
