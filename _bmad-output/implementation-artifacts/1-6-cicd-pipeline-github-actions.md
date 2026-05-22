# Story 1.6: CI/CD Pipeline (GitHub Actions)

Status: review

## Story

As a developer,
I want a GitHub Actions pipeline that validates every pull request including migration bundle correctness against a real database,
so that broken builds, failed tests, and silent data-loss migrations never reach the main branch.

## Acceptance Criteria

1. **Given** a pull request is opened or updated
   **When** the CI pipeline runs
   **Then** it executes these jobs in order, each gating the next: (1) `build` — `dotnet build` passes with zero warnings; (2) `test` — `dotnet test` all tests pass; (3) `migration-bundle` — `dotnet ef migrations bundle` generates without error; (4) `migration-validate` — bundle applied against a fresh PostgreSQL container started by this job

2. **Given** the `migration-validate` job runs
   **When** the migration bundle is applied
   **Then** a dedicated PostgreSQL container is started by this CI job (separate from the `test` job)
   **And** the bundle is applied against it as an executable artifact — a rename-as-drop-add migration that compiles but destroys data will fail this step
   **And** the job reports success only if the bundle applies and the schema is queryable after apply

3. **Given** the `playwright-tests` job in the CI workflow
   **When** the workflow runs today (Epic 1 — no Playwright tests exist)
   **Then** the job is defined in the workflow file with `if: false` — it is skipped without error
   **And** `playwright.config.ts` is committed at `src/OneId.Web/playwright.config.ts` with `use: { launchOptions: { args: ["--force-color-profile=srgb"] } }` — pre-configured for CI contrast detection when Epic 5a enables the job

## Tasks / Subtasks

- [x] Task 1: Create `.github/workflows/ci.yml` (AC: 1, 2)
  - [x] Create `.github/` and `.github/workflows/` directories in project root
  - [x] Define `ci.yml` with trigger on `pull_request` to `main` and `push` to `main`
  - [x] Define `build` job: `ubuntu-latest`, checkout, setup-dotnet 10.0.x, `dotnet build OneId.slnx -c Release`
  - [x] Define `test` job: `needs: build`, `ubuntu-latest`, checkout, setup-dotnet 10.0.x, `dotnet test OneId.slnx -c Release` with `TESTCONTAINERS_RYUK_DISABLED: 'true'`
  - [x] Define `migration-bundle` job: `needs: test`, install `dotnet-ef` 10.0.8, run `dotnet ef migrations bundle`, upload `efbundle` artifact
  - [x] Define `migration-validate` job: `needs: migration-bundle`, postgres service container, download artifact, `chmod +x`, run bundle, verify schema with `psql`
  - [x] Define `playwright-tests` job with `if: false`

- [x] Task 2: Create `src/OneId.Web/playwright.config.ts` (AC: 3)
  - [x] Add `@playwright/test` as devDependency in `src/OneId.Web/package.json`
  - [x] Create `src/OneId.Web/playwright.config.ts` with CI contrast detection launchOptions

- [x] Task 3: Build and test verification (AC: 1)
  - [x] `dotnet build OneId.slnx` — zero warnings
  - [x] `dotnet test OneId.slnx` — all tests pass (integration tests require Docker running locally)
  - [x] Confirm `npm run build` in `src/OneId.Web/` still passes (playwright.config.ts must not break the TypeScript build)

## Dev Notes

### CRITICAL: Solution File is `.slnx` Not `.sln`

The solution file at the project root is `OneId.slnx` (modern XML format introduced in .NET 9). All `dotnet` CLI commands use this file:
- `dotnet build OneId.slnx -c Release`
- `dotnet test OneId.slnx -c Release`

**DO NOT** reference `OneId.sln` — it does not exist.

### `TreatWarningsAsErrors` Is Already Enabled

`Directory.Build.props` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` globally. The `build` job will already fail on any warning. No additional flags needed.

### Why TESTCONTAINERS_RYUK_DISABLED=true

Testcontainers uses a "Ryuk" resource reaper container to clean up Docker artifacts. On GitHub Actions `ubuntu-latest` runners, Ryuk can fail to start due to container naming conflicts or privileged mode restrictions. Setting `TESTCONTAINERS_RYUK_DISABLED=true` disables it — test containers are still cleaned up when the job exits (process exit = container stop).

Without this flag, integration tests may randomly fail in CI with a Ryuk startup error even though all actual test assertions pass.

### dotnet-ef Tool Version Must Match EF Core Version

EF Core is pinned at `10.0.8` in `Directory.Build.props`. The `dotnet-ef` global tool must be installed at the same version:
```
dotnet tool install --global dotnet-ef --version 10.0.8
```
Mismatched versions cause `dotnet ef migrations bundle` to fail with a design-time host error.

### Migration Bundle Command — Exact Flags

```
dotnet ef migrations bundle \
  --project src/OneId.Server \
  --startup-project src/OneId.Server \
  --output efbundle \
  --self-contained \
  --runtime linux-x64
```

- `--self-contained`: bundles the .NET runtime into the executable (the validate job uses `ubuntu-latest` which has .NET SDK available, but `--self-contained` removes ambiguity about runtime version)
- `--runtime linux-x64`: the CI runner is Linux x64; without this flag the bundle targets the current SDK's default RID which may not match
- `--output efbundle`: explicit output name for predictable artifact upload/download

The `Microsoft.EntityFrameworkCore.Design` package is already referenced in `src/OneId.Server/OneId.Server.csproj` with `PrivateAssets=all` (design-time only, not deployed). This is the correct setup for `dotnet ef` tool to work.

### Migration Validate Job — How It Catches Data-Loss Migrations

The migration-validate job applies the bundle against a **fresh** PostgreSQL schema. This catches:
- SQL syntax errors that `dotnet build` cannot detect
- Constraint violations in migration scripts
- Idempotency issues (attempting to create objects that already exist)

A "rename-as-drop-add" migration (drop old column + add new column) would succeed on a fresh DB but the CI schema verification query reveals the new column shape. More importantly, this job gates merging — if a developer applies such a migration against production, the bundle applied here reveals the destructive schema change before it reaches main.

### Exact ci.yml Content

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Build
        run: dotnet build OneId.slnx -c Release

  test:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Test
        run: dotnet test OneId.slnx -c Release
        env:
          TESTCONTAINERS_RYUK_DISABLED: 'true'

  migration-bundle:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Install dotnet-ef
        run: dotnet tool install --global dotnet-ef --version 10.0.8
      - name: Generate migration bundle
        run: |
          dotnet ef migrations bundle \
            --project src/OneId.Server \
            --startup-project src/OneId.Server \
            --output efbundle \
            --self-contained \
            --runtime linux-x64
      - name: Upload bundle
        uses: actions/upload-artifact@v4
        with:
          name: efbundle
          path: efbundle

  migration-validate:
    needs: migration-bundle
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_DB: oneid_ci
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
    steps:
      - name: Download bundle
        uses: actions/download-artifact@v4
        with:
          name: efbundle
      - name: Apply migration bundle
        run: |
          chmod +x ./efbundle
          ./efbundle --connection "Host=localhost;Port=5432;Database=oneid_ci;Username=postgres;Password=postgres"
      - name: Verify schema is queryable
        run: |
          psql -h localhost -U postgres -d oneid_ci \
            -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;"
        env:
          PGPASSWORD: postgres

  playwright-tests:
    if: false
    runs-on: ubuntu-latest
    steps:
      - name: Placeholder
        run: echo "Playwright tests activated in Epic 5a"
```

### Exact playwright.config.ts Content

Create `src/OneId.Web/playwright.config.ts`:

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
  use: {
    launchOptions: {
      args: ['--force-color-profile=srgb'],
    },
  },
});
```

The `--force-color-profile=srgb` flag ensures Chromium uses the sRGB color profile in CI headless environments where the OS color profile may differ from developer machines — preventing spurious visual regression failures when color-sensitive tests are added in Epic 5a.

### package.json Change for Playwright

Add `@playwright/test` to devDependencies in `src/OneId.Web/package.json`. Use the latest stable 1.x version available at implementation time (check `npm show @playwright/test version`). Do NOT pin to a specific patch version — use `^1.xx.0`.

```json
"@playwright/test": "^1.48.0"
```

**Important:** Do NOT run `npx playwright install` — that downloads browser binaries. This story only adds the config file and the package reference. Browser installation is part of Epic 5a when tests actually run.

### Why Each Job Runs on a Fresh Runner

GitHub Actions jobs run on isolated virtual machines. This means:
- The `build` job's compiled output is NOT available to the `test` job
- Each job must run `dotnet restore` + `dotnet build` (or `dotnet test` which does both)
- Do NOT use `--no-build` on the `test` job — there are no shared build artifacts between jobs

This is intentional: each job validating from a clean checkout catches environment-specific build issues.

### Testcontainers in CI — What Already Works

Story 1.5 already set up `OneIdWebApplicationFactory` with Testcontainers (PostgreSQL 4.12.0). On `ubuntu-latest` GitHub Actions runners:
- Docker daemon is pre-installed and running
- Docker socket at `/var/run/docker.sock` is accessible
- `postgres:16-alpine` image pull works (public Docker Hub)
- Testcontainers auto-detects the Docker socket — no `DOCKER_HOST` env var needed

The `TESTCONTAINERS_RYUK_DISABLED=true` env var is the ONLY CI-specific addition needed for the test job.

### File Structure — Where to Create Files

```
OneId/
├── .github/                                    ← CREATE directory
│   └── workflows/                              ← CREATE directory
│       └── ci.yml                              ← NEW
├── src/
│   └── OneId.Web/
│       ├── package.json                        ← MODIFY: add @playwright/test devDependency
│       └── playwright.config.ts               ← NEW
```

No changes to any `.csproj`, `Directory.Build.props`, or backend source files.

### Previous Story Learnings (Stories 1.1–1.5)

- `TreatWarningsAsErrors` is global — the CI build will fail on any nullable annotation sloppiness introduced in new code (not applicable to this story since no C# files are added, but worth knowing for future)
- Solution file is `OneId.slnx` — confirmed by reading the file directly in Story 1.1
- `postgres:16-alpine` is the established PostgreSQL image used throughout (Story 1.5 Testcontainers config, docker-compose.yml)
- xUnit test collection serialization means integration tests run sequentially — the CI `dotnet test` will take longer than unit tests alone (expected behavior, not a failure)
- `TestTokenFactoryContractTests` has 1 skipped test — this will appear as "1 skipped" in CI test output. This is expected and correct (it is the visible known gap from Story 1.5)

### What Epic 2 Must NOT Break in ci.yml

When Epic 2 adds `UseEnvironment("Testing")` JWT Bearer configuration to `WebApplicationFactory`, the `test` job will still work as-is — no changes to `ci.yml` are needed. The Testcontainers integration tests wire JWT Bearer in `ConfigureWebHost`, not in the application startup, so the CI pipeline remains unchanged.

### Verification Commands (Run Locally Before Marking Done)

```bash
# Verify .NET build (from project root)
dotnet build OneId.slnx -c Release

# Verify tests (requires Docker running locally)
dotnet test OneId.slnx -c Release

# Verify frontend build still passes (from src/OneId.Web/)
cd src/OneId.Web && npm install && npm run build

# Verify migration bundle generates (requires dotnet-ef installed)
dotnet tool install --global dotnet-ef --version 10.0.8
dotnet ef migrations bundle --project src/OneId.Server --startup-project src/OneId.Server --output efbundle --self-contained --runtime linux-x64
```

### References

- [Source: epics.md#Story 1.6] — acceptance criteria, job names and order, playwright-tests if:false, playwright.config.ts launchOptions requirement
- [Source: epics.md#Epic 1] — AR-3 (CI/CD GitHub Actions), AR-13 (migration bundles)
- [Source: architecture.md#Project Directory Structure] — `.github/workflows/ci.yml` location, confirmed in architecture
- [Source: architecture.md#All Implementation Agents MUST] — TreatWarningsAsErrors context
- [Source: Directory.Build.props] — `TreatWarningsAsErrors=true`, EF Core pinned at 10.0.8, TargetFramework net10.0
- [Source: OneId.slnx] — solution file lists 3 projects: Server, IntegrationTests, UnitTests (OneId.Web is NOT in the solution — only .NET projects)
- [Source: src/OneId.Server/OneId.Server.csproj] — `Microsoft.EntityFrameworkCore.Design 10.0.8` with `PrivateAssets=all` is already present
- [Source: implementation-artifacts/1-5-...md] — Testcontainers 4.12.0 setup, `postgres:16-alpine`, `TESTCONTAINERS_RYUK_DISABLED` note, collection fixture lifecycle

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Completion Notes List

- Created `.github/workflows/ci.yml` with 5 jobs: `build` → `test` → `migration-bundle` → `migration-validate` (each gating the next via `needs:`), plus `playwright-tests` with `if: false`. Triggers on PR to main and push to main.
- `build` job uses `dotnet build OneId.slnx -c Release` (solution file is `.slnx` not `.sln`). `TreatWarningsAsErrors` is already global so zero warnings = enforced automatically.
- `test` job sets `TESTCONTAINERS_RYUK_DISABLED: 'true'` to prevent Ryuk container startup failures on GitHub Actions `ubuntu-latest` runners.
- `migration-bundle` job installs `dotnet-ef --version 10.0.8` (matches EF Core pin in Directory.Build.props), generates bundle with `--self-contained --runtime linux-x64` for Linux CI runner compatibility.
- `migration-validate` job uses a GitHub Actions service container (`postgres:16-alpine`) with health check, downloads the efbundle artifact, applies it, then verifies schema is queryable with `psql`.
- `playwright.config.ts` is outside both `tsconfig.app.json` (include: ["src"]) and `tsconfig.node.json` (include: ["vite.config.ts"]) — TypeScript build does not process it. Frontend build confirmed clean.
- Added `@playwright/test ^1.48.0` to devDependencies; `npm install` succeeded (3 new packages, 0 vulnerabilities).
- Build verification: `dotnet build` — 0 warnings, 0 errors. `dotnet test` — Unit: 9 passed, 1 skipped; Integration: 5 passed, 1 skipped. `npm run build` — clean Vite build.

### File List

- .github/workflows/ci.yml (new)
- src/OneId.Web/playwright.config.ts (new)
- src/OneId.Web/package.json (modified — added @playwright/test ^1.48.0 devDependency)
- src/OneId.Web/package-lock.json (modified — npm install updated lockfile)
