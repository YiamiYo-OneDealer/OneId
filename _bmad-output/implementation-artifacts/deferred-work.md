# Deferred Work Log

## Deferred from: code review of 1-1-initialize-backend-and-frontend-projects (2026-05-22)

- **Tailwind v4 / missing `tailwind.config.ts`** (`src/OneId.Web/components.json`) — Tailwind v4 deprecates `tailwind.config.ts`; `components.json` references a file that doesn't exist; dark mode uses v4 CSS-only approach instead of spec-required `darkMode: ['class']` in TS config. Reason: ramifications of v4 approach not yet clear. Revisit in Story 5a.1.

- **Auto-migration is dev-only; no production migration strategy** (`src/OneId.Server/Program.cs:29-33`) — `db.Database.MigrateAsync()` runs only in Development. Production migration strategy (CLI entrypoint, deployment pipeline step) is owned by Story 1.6.
- **CORS policy not configured** (`src/OneId.Server/Program.cs`) — No `AddCors()`/`UseCors()` calls. No frontend API calls exist yet; add when routing and auth are wired in Epic 2/5a.
- **No frontend test infrastructure** (`src/OneId.Web/package.json`) — No `vitest`/`jest`, no `test` script. Frontend testing is not in Story 1.1 scope; add alongside first testable frontend feature.
- **`MigrateAsync` crashes if PostgreSQL unreachable at dev startup** (`src/OneId.Server/Program.cs:31`) — No retry policy or graceful degradation around startup migration. Acceptable for local dev bootstrap; revisit if Docker Compose health checks in Story 1.2 don't cover it.
- **`#nullable disable` in generated migration file** (`Migrations/20260522064503_InitialCreate.cs`) — EF Core scaffolds migrations with this pragma by default. Removing it may break future `dotnet ef migrations add` output consistency.
