# Repository Guidelines — Mobile

Fonte canônica na skill `$nero`: `references/guidelines/mobile-guidelines.md`.

Paths e scripts abaixo são um **layout Expo típico / esperado**. Documente o checkout real em `structure.md` / `conventions.md`; se divergir deste alvo, registre débito de adequação em `tech-debt.md` — não reestruture código só por este playbook.

## Project Structure & Module Organization

For Expo React Native apps (padrão esperado):

- Main app routes in `src/app/` with Expo Router layouts and screens.
- Shared UI in `src/components/`.
- Business screens in `src/screens/` (feature folders).
- App-wide helpers in `src/common/`, stores in `src/stores/`, hooks in `src/hooks/`.
- Static assets in `assets/` and/or `src/assets/`.
- Type declarations in `src/@types/` when the project uses that layout.

Always use the skills `$vercel-react-native-skills` and `$react-native-best-practices` when available. Acione também `$nero` para knowledge/contexto. Libs de UI/auth específicas da organização vivem em Domain Skills externas — ver `references/domain-skills.md`.

## Mobile Domain Rules

- Keep route files thin. They should only compose and forward to the screen component.
- Keep business behavior in feature hooks under `src/screens/<Feature>/hooks/` (or the project's equivalent feature-hook location).
- Keep screen entrypoints focused on UI composition.
- Split complex screens into smaller components when UI states are clearly different, especially for camera, permission, review, and finalization flows.
- Prefer Expo Router public APIs. Avoid internal package imports or deep framework paths.
- Treat camera, permission, barcode, and upload flows as sensitive UI states that should preserve behavior when refactoring.
- Do not move feature logic into shared components unless the behavior is truly cross-cutting.
- Maintain the existing mobile navigation and shell structure; do not flatten routes into generic screens.
- When changing screen structure, validate that loading, empty, denied-permission, and success states still render correctly on device.

## Build, Test, and Development Commands

Prefer the package manager implied by the lockfile (`pnpm` when `pnpm-lock.yaml` exists). Scripts abaixo são o padrão esperado; use os nomes reais do `package.json` do checkout:

- `pnpm start`: Expo dev server.
- `pnpm start:client` (ou equivalente): Expo com dev client.
- `pnpm ios` / `pnpm android`: simulador ou device.
- `pnpm web`: browser quando aplicável.
- `pnpm build:dev` / `build:preview` / `build:prod` (ou perfis EAS do projeto): builds.
- `pnpm lint` / formatação: conforme scripts locais.

## Coding Style & Naming Conventions

Use TypeScript and React function components. Follow the repository formatter and lint rules before generic preferences. Prefer the established path structure over ad hoc folders. Name React components and screens in `PascalCase`, hooks as `useSomething`, and route files to match Expo Router conventions. If NativeWind/Tailwind is present, keep class ordering consistent with the project's Prettier plugin when configured.

## Testing Guidelines

Many mobile apps have limited automated suites. Validate with the project's lint/format scripts and smoke-test in Expo (`pnpm start` or platform commands). If tests exist or are added later, keep them close to the feature and name them after the unit under test. If tests cannot be run, state the reason and provide the strongest inspection evidence available.

## Commit & Pull Request Guidelines

Keep commits focused and imperative (conventional subjects such as `fix:`, `chore:`, `feat:` when the repo follows that pattern). Pull requests should include a concise summary, linked issue when available, screenshots or screen recordings for UI work, and notes about Expo, EAS, or environment impact. After updating anything in a mobile project, suggest a commit message for the resulting change.

## Security & Configuration Tips

Review the project's config/env modules before changing environment, logging, Datadog, or Sentry behavior. Avoid committing secrets or machine-specific values. Treat mobile build and release settings as production-sensitive changes.
