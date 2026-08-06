# Repository Guidelines — API

Fonte canônica na skill `$nero`: `references/guidelines/api-guidelines.md`.
Também aplica a domínio `integracoes` (herança: hubs/barramentos/consumers tipicamente API).

## Project Structure & Module Organization

For backend APIs, preserve the structure already used by the repository before introducing a new architecture. Typical .NET APIs should keep clear boundaries between `Api` or controllers, application services, domain/business rules, DTOs/contracts, repositories, infrastructure, configuration, middleware, and tests. Typical NestJS APIs should keep modules focused by domain, with controllers thin, providers/services holding use cases, DTOs and validators at the API boundary, repositories or data providers isolated, and shared concerns placed in explicit common modules.

Always use the skill `$dotnet-backend-patterns` for .NET backend work when available. This reference also applies to NestJS APIs; translate the same boundaries to Nest modules, controllers, providers, DTOs, pipes, guards, interceptors, repositories, and e2e tests.

## Backend Domain Rules

- Keep controllers thin. They should translate HTTP input/output, not hold business rules.
- Keep business behavior in services, use cases, handlers, or the existing domain layer.
- Keep data access isolated in repositories, query services, ORM adapters, or the persistence layer already used by the project.
- Preserve public API contracts unless the task explicitly includes a compatibility plan.
- For API changes, validate routes, status codes, JSON payloads, headers, pagination, filtering, and error contracts.
- Do not invent business rules. If behavior is unclear, mark the assumption explicitly and keep the implementation narrow.
- Do not expose persistence entities directly through public APIs unless that is already the established contract.
- Prefer DTOs/contracts for request and response models, with validation at the API boundary.
- Propagate cancellation/timeouts where the stack supports it, especially for database and HTTP calls.
- Use exceptions for exceptional failures, not expected business flow; prefer a result/error contract for recoverable domain outcomes when the project already supports it.
- Preserve authentication, authorization, audit, logging, correlation IDs, and observability behavior when refactoring.
- Treat migrations, schema changes, background jobs, integrations, and message consumers as production-sensitive surfaces.
- Do not change migrations, CI/CD, infrastructure, or package dependencies without explicit technical and operational justification.
- Keep changes small, local, reversible, and verifiable.
- Prefer root-cause analysis before changing production code or tests.

## .NET API Defaults

- Preserve Controller, Service, Repository, DTO, and domain boundaries when they already exist.
- Use dependency injection through constructors and register services with the narrowest correct lifetime.
- Use `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` for typed configuration instead of hardcoded values.
- Use `async`/`await` through the call stack; do not block with `.Result`, `.Wait()`, or `GetAwaiter().GetResult()`.
- Accept and pass `CancellationToken` in async API, service, repository, database, and HTTP client calls.
- Use `IHttpClientFactory` or typed clients instead of manually creating `new HttpClient()`.
- For EF Core, use projections and `AsNoTracking()` for read-only queries; watch for N+1 queries and missing indexes.
- For Dapper, use parameterized queries and keep SQL close to the repository/query object that owns it.
- Prefer unit tests for business rules and integration tests for API contracts, persistence, and middleware behavior.

## NestJS API Defaults

- Keep modules domain-oriented; avoid turning `common` or `shared` into a dumping ground.
- Keep controllers responsible for route shape, decorators, guards, pipes, and response mapping only.
- Put business rules in injectable services/providers, not decorators, controllers, or ORM entities.
- Use DTO classes with validation decorators and pipes for request validation.
- Use guards for authentication/authorization, interceptors for cross-cutting response/telemetry behavior, and filters for centralized exception mapping.
- Keep Prisma, TypeORM, Sequelize, raw SQL, or external SDK access behind providers/repositories when the project already follows that pattern.
- Prefer explicit environment/config modules and typed config access over direct scattered `process.env` reads.
- Use Jest unit tests for providers and e2e tests for routes, guards, validation pipes, and serialized HTTP contracts.
- Preserve OpenAPI/Swagger annotations when the project exposes API docs.

## Build, Test, and Development Commands

- `.NET`: `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet format` when applicable.
- `NestJS`: prefer the package manager implied by the lockfile; usually `pnpm install`, `pnpm lint`, `pnpm test`, `pnpm test:e2e`, and `pnpm build`.
- `rg`: inspect endpoints, DTOs, services, repositories, migrations, tests, and config quickly.
- API clients, focused scripts, Swagger/OpenAPI, Postman collections, or curl may be used for targeted endpoint validation when present.

## Coding Style & Naming Conventions

Follow the repository formatter and naming conventions before applying generic preferences. Keep names domain-driven and explicit. Avoid generic service names when a more precise use-case or domain term exists. Keep comments short and useful, focused on non-obvious decisions or constraints. When adding a public function, method, DTO, endpoint, or provider, add auxiliary documentation such as XML docs, JSDoc, Swagger decorators, README notes, or examples when it fits the existing ecosystem.

For .NET, prefer PascalCase for public types and members, interfaces with the established local convention, async methods ending in `Async` when the project follows that style, and records for immutable DTOs when already compatible with the codebase. For NestJS, prefer TypeScript strictness, explicit DTOs, injectable providers, and filenames aligned with Nest conventions such as `*.controller.ts`, `*.service.ts`, `*.module.ts`, and `*.dto.ts`.

## Testing Guidelines

Validation should be proportional to risk. Business rules need focused unit tests. Public API behavior needs integration or e2e tests that cover route, status code, payload, validation, authorization, and error mapping. Persistence changes need migration/schema verification and queries exercised against a realistic provider when possible. Integration changes need success, failure, timeout, and retry/error-path coverage when practical.

Do not weaken tests to make failures pass. If a test is obsolete because a contract changed, record the contract decision and update the test to the new explicit behavior. If tests cannot be run, state the reason and provide the strongest inspection evidence available.

## Commit & Pull Request Guidelines

Keep commits focused and imperative, using conventional subjects such as `fix:`, `feat:`, `test:`, `docs:`, or `chore:` when the repository follows that pattern. Pull requests should include the technical objective, affected endpoints/jobs/contracts, files changed, validation commands and results, migration/deploy impact, rollback considerations, and residual risks.

For backend changes, always call out public API compatibility, data impact, security impact, observability impact, and QA recommendations when applicable. After updating anything in a backend project, suggest a commit message for the resulting change.

## Security & Configuration Tips

Review configuration, authentication, authorization, secrets handling, logging, Datadog/Sentry/OpenTelemetry, database access, and external integrations before changing backend behavior. Do not commit secrets, tokens, cookies, private keys, `.env` values, or machine-specific configuration. Avoid logging sensitive payloads or identifiers unless the project already has an approved masking strategy. Treat migrations, background processing, retries, idempotency, and integration calls as operational-risk areas that need explicit evidence before release.
