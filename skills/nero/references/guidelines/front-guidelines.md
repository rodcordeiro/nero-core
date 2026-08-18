# Repository Guidelines — Front

Aplique cada regra da secao que a tarefa tocar.

## Project Structure & Module Organization

For frontend web applications, preserve the structure already used by the repository before introducing a new architecture. Typical React, Next.js, Vite, or SPA projects should keep route/page entrypoints thin, shared UI components isolated, feature-specific components close to their feature, business-facing hooks or services separated from visual components, API clients centralized, and app-wide helpers, types, styles, assets, and state kept in explicit folders.

Use the local framework conventions first. For Next.js, respect the existing `app/` or `pages/` router model. For Vite/SPA React, respect the current router, layout, state, and feature folder patterns. Reshape into a generic structure only when the task explicitly requires that migration.

## Frontend Domain Rules

- Start from the user flow. Components, state, API calls, and layout exist to support a concrete task.
- Keep route/page files thin. They should compose layout, load required context, and forward behavior to feature components or hooks.
- Keep business behavior in feature hooks, services, state stores, or use-case helpers, not buried inside presentational components.
- Keep shared components generic. Move behavior into shared UI only when it is truly cross-cutting.
- Preserve existing design-system, routing, state, API, styling, and component patterns unless there is a concrete reason to diverge.
- If the app depends on an organization-specific UI or auth library, document that dependency in the app's `AGENTS.md` / Domain Skill with checkout evidence.
- Preserve API contracts unless the task explicitly includes backend compatibility and rollout impact.
- Validate loading, empty, error, disabled, optimistic, and success states when relevant.
- Preserve accessibility semantics, labels, focus behavior, keyboard navigation, color contrast, and responsive constraints.
- Avoid layout shifts caused by dynamic labels, loading states, long text, hover styles, or conditional controls.
- Encode only proven business rules and copy. If behavior, permission, validation, or microcopy is unclear, mark the assumption explicitly.
- Add packages only with explicit technical and operational justification.
- Keep changes small, local, reversible, and verifiable.

## React and TypeScript Defaults

- Prefer TypeScript and function components when the project already uses them.
- Name components in `PascalCase`, hooks as `useSomething`, and files according to the repository convention.
- Keep component props explicit and narrow; avoid broad `any`, untyped objects, or leaking API response shapes directly into UI components.
- Prefer derived state over duplicated state. Avoid effects for values that can be computed during render.
- Keep side effects in hooks or services with clear dependency boundaries.
- Memoize only when there is a measured or obvious render-cost reason; do not add `useMemo` or `useCallback` mechanically.
- Treat forms, permissions, destructive actions, uploads, payments, and authentication flows as sensitive user states.
- Use existing data-fetching patterns before adding a new client pattern. Respect current React Query, SWR, loader, server action, or custom fetch conventions.
- Keep server/client boundaries explicit in Next.js. Browser-only code stays in client components; server-only code stays off the client bundle.

## API and State Defaults

- Centralize API calls in the existing client/service layer when one exists.
- Keep request and response mapping explicit at the boundary between API and UI.
- Handle cancellation, stale responses, retries, and race conditions when a screen can trigger overlapping requests.
- Surface backend errors with the product's existing error UX; invent a generic toast only when that contract already exists.
- Preserve auth headers, tenant/context headers, correlation IDs, and telemetry behavior.
- Keep global state for genuinely shared app state. Prefer local or feature state for screen-local behavior.
- Avoid storing sensitive data in browser storage unless the project already has an approved pattern.

## Build, Test, and Development Commands

- Prefer the package manager implied by the lockfile: `pnpm` when `pnpm-lock.yaml` exists, `npm` for `package-lock.json`, and `yarn` for `yarn.lock`.
- Common commands: `pnpm lint`, `pnpm test`, `pnpm typecheck`, `pnpm build`, and the equivalent `npm run` or `yarn` scripts when applicable.
- `pnpm dev`, `npm run dev`, or `yarn dev`: start the local frontend server when visual or browser validation is needed.
- `rg`: inspect routes, components, hooks, stores, API clients, schemas, tests, and design-system usage quickly.
- Use Playwright, browser DevTools, screenshots, or documented browser inspection for visual, responsive, and interaction-sensitive changes.

## Coding Style & Naming Conventions

Follow the repository formatter and lint rules before applying generic preferences. Keep imports organized according to the local toolchain. Prefer explicit names from the product domain over generic names like `DataCard`, `ModalWrapper`, or `handleClick` when intent is clearer.

Keep CSS, Tailwind, CSS Modules, styled-components, or design-system usage consistent with the project. Do not mix styling strategies casually. Use existing spacing, typography, icons, colors, tokens, and component variants. Comments should be short and explain non-obvious constraints, browser workarounds, or domain decisions.

## Testing Guidelines

Validation should be proportional to risk. Use unit tests for pure UI logic, hooks, formatters, and reducers. Use component tests for conditional rendering, forms, and state transitions. Use e2e or browser validation for navigation, authentication, critical flows, accessibility behavior, responsive layout, and API integration.

For user-facing changes, check the main path plus relevant loading, empty, error, disabled, and success states. For responsive changes, inspect at least a narrow mobile viewport and a desktop viewport. If tests cannot be run, state the reason and provide the strongest inspection evidence available.

Keep tests faithful to the contract. If behavior changed intentionally, update tests to the explicit new contract and record the affected user flow.

## Commit & Pull Request Guidelines

Keep commits focused and imperative, using conventional subjects such as `fix:`, `feat:`, `test:`, `docs:`, or `chore:` when the repository follows that pattern. Pull requests should include the technical objective, screens or flows affected, files changed, validation commands and results, screenshots or recordings for visual changes, API/backend compatibility notes, accessibility/responsiveness notes, and residual risks.

After updating anything in a frontend project, suggest a commit message for the resulting change.

## Security & Configuration Tips

Review configuration, environment variables, auth/session handling, API clients, logging, analytics, Sentry/Datadog/OpenTelemetry, and browser storage before changing frontend behavior. Do not commit secrets, tokens, cookies, private keys, `.env` values, or machine-specific configuration. Avoid logging sensitive payloads, identifiers, headers, or form values unless the project already has an approved masking strategy. Treat authentication, authorization visibility, payments, uploads, destructive actions, and personal data screens as production-sensitive UI surfaces.
