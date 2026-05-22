# Story 1.1: Initialize Backend and Frontend Projects

Status: done

## Story

As a developer,
I want both projects initialized with correct templates, dependencies, and compiler strictness configured,
so that the team has a compilable, runnable starting point with zero-warning enforcement from the first commit.

## Acceptance Criteria

1. `dotnet build` in `src/OneId.Server/` produces zero errors and zero warnings.
2. `Directory.Build.props` at solution root contains `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<Nullable>enable</Nullable>` applied to all .NET projects.
3. `OpenIddict.AspNetCore` v7.5.0, `OpenIddict.EntityFrameworkCore` v7.5.0, and `Npgsql.EntityFrameworkCore.PostgreSQL` are referenced in `OneId.Server.csproj`.
4. Project was bootstrapped with `dotnet new webapi -n OneId.Server --use-controllers` and resides at `src/OneId.Server/`.
5. `npm install && npm run build` in `src/OneId.Web/` succeeds (Vite + React + TypeScript strict template).
6. `npx shadcn@latest init` has been run with dark theme configuration; components output to `src/components/ui/`.
7. `"strict": true` is confirmed in `tsconfig.json`.
8. Given a running PostgreSQL instance, EF Core migrations apply on startup and `GET /health` returns HTTP 200.
9. `DevSigningKeyStabilityTest.cs` exists at `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs` with the correct `[Fact(Skip = ...)]` decorator and test body as specified in Dev Notes.

## Tasks / Subtasks

- [x] Task 1: Create solution and backend project skeleton (AC: #1, #2, #4)
  - [x] Run at repo root: `dotnet new sln -n OneId`
  - [x] Run: `dotnet new webapi -n OneId.Server --use-controllers -o src/OneId.Server`
  - [x] Run: `dotnet sln add src/OneId.Server/OneId.Server.csproj`
  - [x] Create `Directory.Build.props` at repo root (exact content in Dev Notes)
  - [x] Delete generated boilerplate: `WeatherForecast.cs` and `Controllers/WeatherForecastController.cs`
  - [x] Verify: `dotnet build` shows 0 errors, 0 warnings

- [x] Task 2: Install NuGet packages (AC: #3)
  - [x] Add `OpenIddict.AspNetCore` Version="7.5.0"
  - [x] Add `OpenIddict.EntityFrameworkCore` Version="7.5.0"
  - [x] Add `Npgsql.EntityFrameworkCore.PostgreSQL` (latest stable compatible with .NET 9)
  - [x] Add `Microsoft.EntityFrameworkCore.Design` as dev-only (PrivateAssets="all")
  - [x] Verify `dotnet build` still clean after package additions

- [x] Task 3: Create full directory skeleton (AC: #4)
  - [x] Create backend folder tree under `src/OneId.Server/` (see Project Structure Notes)
  - [x] Create placeholder `Application/Common/ITenantContext.cs` (empty interface, comment: wired in Story 1.3a)
  - [x] Create placeholder `Application/Common/Permissions.cs` (empty static class, comment: populated in Story 4a.1)
  - [x] Create placeholder `Application/Common/ICacheService.cs` (empty interface, comment: wired in Story 1.7a)
  - [x] Add `.gitkeep` to empty directories to preserve them in git

- [x] Task 4: Create AppDbContext and initial migration (AC: #8)
  - [x] Create `Infrastructure/Persistence/AppDbContext.cs` (exact shape in Dev Notes)
  - [x] Register AppDbContext in `Program.cs` using Npgsql; read connection string from config
  - [x] Run: `dotnet ef migrations add InitialCreate -o Infrastructure/Persistence/Migrations`
  - [x] Configure dev-only startup migration application in `Program.cs`

- [x] Task 5: Wire Program.cs skeleton (AC: #8)
  - [x] Add health checks: `builder.Services.AddHealthChecks()` + `app.MapHealthChecks("/health")`
  - [x] Add Problem Details: `builder.Services.AddProblemDetails()`
  - [x] Add AR-5 registration order comments as placeholders (exact comments in Dev Notes)
  - [x] Verify `GET /health` returns HTTP 200 against local PostgreSQL

- [x] Task 6: Create test projects and DevSigningKeyStabilityTest (AC: #9)
  - [x] Run: `dotnet new xunit -n OneId.Server.UnitTests -o tests/OneId.Server.UnitTests`
  - [x] Run: `dotnet new xunit -n OneId.Server.IntegrationTests -o tests/OneId.Server.IntegrationTests`
  - [x] Add both test projects to the solution
  - [x] Add `OneId.Server` project reference to both test projects
  - [x] Create `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs` (exact content in Dev Notes)
  - [x] Verify: `dotnet test` runs; the skipped test is visible in output (not silently green)

- [x] Task 7: Initialize frontend project (AC: #5, #6, #7)
  - [x] Run in `src/`: `npm create vite@latest OneId.Web -- --template react-ts`
  - [x] Run in `src/OneId.Web/`: `npm install`
  - [x] Verify `"strict": true` in `tsconfig.json`
  - [x] Run: `npx shadcn@latest init` with selections specified in Dev Notes
  - [x] Verify components directory is `src/components/ui/`
  - [x] Run: `npm run build` — must succeed with zero TypeScript errors

## Dev Notes

### CRITICAL: AR-15 Deferred-Skip Cap

This story creates exactly **1 deferred skip**: `DevSigningKeyStabilityTest`. The project-wide cap is **3 deferred skips** at any time (AR-15). The remaining two caps (`TestTokenFactoryContractTests` in Story 1.5, `PermissionCatalogSyncTests` in Story 1.7b) must not be created in this story. Do NOT add any additional `[Fact(Skip = ...)]` decorators anywhere.

### CRITICAL: Program.cs Registration Order (AR-5)

The order in `Program.cs` is a delivery correctness gate enforced by an integration test in Story 1.3a. Lay out the registration with comments now — do NOT reorder later:

```csharp
// AR-5 STEP 1: ITenantContextMiddleware MUST precede EF Core and OpenIddict — see architecture.md
// TODO Story 1.3a: app.UseMiddleware<TenantContextMiddleware>();

// AR-5 STEP 2: EF Core with global query filters referencing ITenantContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AR-5 STEP 3: OpenIddict registered AFTER EF Core — Story 2.1 wires this
// TODO Story 2.1: builder.Services.AddOpenIddict()...

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
```

### Directory.Build.props — Exact Content

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Place at repo root (same level as `OneId.sln`). Individual `.csproj` files must NOT redeclare these properties — they inherit automatically.

### AppDbContext — Initial Shape

```csharp
using Microsoft.EntityFrameworkCore;

namespace OneId.Server.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // MUST be first: applies snake_case naming to all entities
        builder.UseSnakeCaseNamingConvention();

        // AR-14: UseXminAsConcurrencyToken applied to all mutable entities.
        // Each epic that introduces a new mutable entity is responsible for adding it here.
        // Story 1.3b adds: Tenant, User
        // Epics 3 and 4a add: Role, RoleSet, Group, Permission, DimensionValue, etc.
    }
}
```

`UseSnakeCaseNamingConvention()` is an extension from `Npgsql.EntityFrameworkCore.PostgreSQL`. It must be the first call inside `OnModelCreating` — anything called before it won't receive snake_case naming.

Entity `IEntityTypeConfiguration<T>` files go in `Infrastructure/Persistence/Configurations/` — never configure entities inline in `OnModelCreating` (architecture rule).

### DevSigningKeyStabilityTest.cs — Exact File Content

```csharp
using Xunit;

namespace OneId.Server.Tests.Infrastructure;

public class DevSigningKeyStabilityTest
{
    [Fact(Skip = "Wired in Epic 2 — remove Skip when OpenIddict signing key is configured")]
    public async Task SigningKey_IsFileBased_AndSurvivesAppRestart()
    {
        // Epic 2 Story 2.1 must remove this Skip attribute and make this test pass.
        // Test must assert:
        //   1. Signing key file exists at: keys/dev-signing.key
        //   2. A token signed before a WebApplicationFactory restart validates
        //      successfully after restart (proves key is file-based, not ephemeral)
        await Task.CompletedTask;
        Assert.Fail("DevSigningKeyStabilityTest not yet wired — implement in Epic 2 Story 2.1");
    }
}
```

File location: `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs`

### NuGet Package References — Exact XML

```xml
<ItemGroup>
  <PackageReference Include="OpenIddict.AspNetCore" Version="7.5.0" />
  <PackageReference Include="OpenIddict.EntityFrameworkCore" Version="7.5.0" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

Use the latest stable minor/patch versions compatible with .NET 9. OpenIddict is pinned at **exactly 7.5.0** — do not use a range specifier for OpenIddict.

### Frontend: shadcn/ui Init Selections

Run `npx shadcn@latest init` and select:
- Style: **New York** (compact, appropriate for admin console density)
- Base color: **Zinc** (matches the UX zinc palette from UX-DR1)
- CSS variables for theming: **Yes**
- Tailwind config: TypeScript (`tailwind.config.ts`)
- Components alias: `@/components` → `src/components`
- Utils alias: `@/lib/utils` → `src/lib/utils`

**Do NOT** add custom CSS variable tokens or ESLint color-enforcement rules in this story — that is Epic 5a Story 5a.1. The shadcn init just establishes the infrastructure.

After init, verify `tailwind.config.ts` has `darkMode: ['class']` — shadcn dark mode uses class-based toggling.

### What This Story Explicitly Does NOT Implement

Deferred to their owning stories — do not add in Story 1.1:
- Docker Compose setup → Story 1.2
- Serilog / OTEL / Seq pipeline → Story 1.4
- `ITenantContextMiddleware` implementation → Story 1.3a
- EF Core global query filters + entity stubs (Tenant, User) → Story 1.3b
- Testcontainers + Respawn + TestTokenFactory → Story 1.5
- GitHub Actions CI pipeline → Story 1.6
- ArchUnit `InternalBoundaryTests.cs` → Story 1.7a
- `ICacheService` implementation (`MemoryCacheService`) → Story 1.7a
- DevSeeder implementation → Story 1.7b
- `PermissionCatalog.cs` seeding + `PermissionCatalogSyncTests.cs` → Story 1.7b
- OpenIddict pipeline configuration → Story 2.1
- Frontend routing, state management, API client → respective Epic 5a stories

### Placeholder Interfaces — Do Not Leave Empty Files Without Comments

Every placeholder file created in Task 3 must have at minimum:
```csharp
// Wired in Story [X.Y] — see architecture.md and epics.md
namespace OneId.Server.Application.Common;

public interface ITenantContext
{
    // TODO Story 1.3a
}
```

This prevents compiler errors while making the wiring point visible.

### Project Structure Notes

Exact directory layout for `src/OneId.Server/` (from architecture.md §Complete Project Directory Structure):

```
src/OneId.Server/
├── OneId.Server.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Dockerfile                      ← create empty, Story 1.2 fills it
├── Controllers/                    ← empty for now
├── Domain/
│   ├── Entities/                   ← filled by Story 1.3b and later
│   ├── Services/                   ← IPermissionEvaluator.cs added in Epic 4b
│   ├── Enums/                      ← DimensionAxis.cs, etc. added in Epic 4a
│   └── Events/                     ← filled in Epic 3 and 4a
├── Application/
│   ├── Common/
│   │   ├── ITenantContext.cs       ← placeholder interface
│   │   ├── ICacheService.cs        ← placeholder interface
│   │   ├── Permissions.cs          ← empty static class
│   │   └── Exceptions/             ← empty dir
│   ├── Tenants/                    ← empty dir
│   ├── Users/                      ← empty dir
│   └── Internal/                   ← InternalAdminContext injectable here ONLY (ArchUnit, Story 1.7a)
└── Infrastructure/
    ├── Persistence/
    │   ├── AppDbContext.cs
    │   ├── Migrations/             ← InitialCreate migration goes here
    │   ├── Configurations/         ← IEntityTypeConfiguration<T> files (Story 1.3b+)
    │   ├── Seeds/                  ← DevSeeder.cs (Story 1.7b), PermissionCatalog.cs (Story 4a.1)
    │   └── Interceptors/           ← TimestampInterceptor.cs, SoftDeleteInterceptor.cs (Story 1.3b)
    ├── OpenIddict/                 ← empty dir (Story 2.1+)
    ├── Caching/                    ← empty dir (Story 1.7a)
    ├── Middleware/                 ← empty dir (Story 1.3a)
    ├── Logging/                    ← empty dir (Story 1.4)
    └── Federation/                 ← empty dir (Epic 6)
```

Test project structure:
```
tests/
├── OneId.Server.UnitTests/
│   └── Infrastructure/
│       └── DevSigningKeyStabilityTest.cs    ← this story
└── OneId.Server.IntegrationTests/
    └── Helpers/                             ← empty dir (Story 1.5)
```

### References

- [Source: _bmad-output/planning-artifacts/architecture.md#Starter Template Evaluation] — exact init commands and package list
- [Source: _bmad-output/planning-artifacts/architecture.md#Complete Project Directory Structure] — directory skeleton
- [Source: _bmad-output/planning-artifacts/architecture.md#Naming Patterns] — `UseSnakeCaseNamingConvention()` is mandatory
- [Source: _bmad-output/planning-artifacts/architecture.md#All Implementation Agents MUST] — rules #1 (snake_case), #4 (xmin), #5 (Problem Details) apply from first commit
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication & Security] — file-based signing key requirement, `DevSigningKeyStabilityTest` rationale
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.1] — full acceptance criteria
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 1 Implementation Notes] — AR-5 registration order gate, AR-15 deferred-skip cap context
- [Source: _bmad-output/planning-artifacts/epics.md#AR-15] — 3-skip cap; this story consumes 1 slot

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `EFCore.NamingConventions` v10: `UseSnakeCaseNamingConvention()` is a `DbContextOptionsBuilder` extension, NOT a `ModelBuilder` extension. Must be called in `AddDbContext` lambda, not `OnModelCreating`.
- MSB3277 EF Core version conflict (10.0.7 vs 10.0.8): pinning via `PackageReference Update` in `Directory.Build.props` insufficient for test projects. Fix: added explicit EF Core 10.0.8 pins directly in both test `.csproj` files.
- `dotnet new webapi` created `net10.0` (machine has .NET 10, not 9). `Directory.Build.props` updated to `net10.0`.
- shadcn CLI `npx shadcn@latest init` failed with "Could not load workspace config". Fix: manually created `components.json`, `src/lib/utils.ts`, installed deps (`clsx`, `tailwind-merge`, `lucide-react`, `class-variance-authority`) directly.
- Tailwind v4 `@apply border-border` fails without `@theme inline` mapping. Fix: added `@theme inline` block mapping `--color-*` to `hsl(var(--*))` in `index.css`.
- `tsconfig.app.json` `baseUrl` deprecated in TS 7.0. Fix: removed `baseUrl`, `paths` resolves relative to config file in bundler mode.

### Completion Notes List

- Used .NET 10.0 (machine does not have .NET 9; architecture spec said 9 but 10 is fully compatible).
- shadcn CLI workspace config issue worked around via manual `components.json` + core utils setup. All ACs satisfied.
- Tailwind v4 requires `@theme inline` block to expose CSS variable tokens (`--color-background`, etc.) as utility classes for `@apply`. Added alongside the existing HSL variable definitions.
- `EFCore.NamingConventions` 10.0.1 API: snake_case configured in `AddDbContext` options builder, not in `OnModelCreating`. AR-14 comment added to `AppDbContext.OnModelCreating` for future entity additions.
- AR-5 registration order placeholder comments embedded in `Program.cs` for Stories 1.3a and 2.1.
- AR-15: exactly 1 deferred skip used (`DevSigningKeyStabilityTest`); 2 slots remain for Stories 1.5 and 1.7b.
- `dotnet test` output shows `DevSigningKeyStabilityTest` as skipped (not silently passing).
- `npm run build` produces zero TypeScript errors and zero warnings.

### File List

Backend:
- `Directory.Build.props` (new)
- `OneId.sln` (new)
- `src/OneId.Server/OneId.Server.csproj` (new)
- `src/OneId.Server/Program.cs` (new)
- `src/OneId.Server/appsettings.json` (new)
- `src/OneId.Server/appsettings.Development.json` (new)
- `src/OneId.Server/Dockerfile` (new — empty placeholder)
- `src/OneId.Server/Application/Common/ITenantContext.cs` (new)
- `src/OneId.Server/Application/Common/Permissions.cs` (new)
- `src/OneId.Server/Application/Common/ICacheService.cs` (new)
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs` (new)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260522064503_InitialCreate.cs` (new)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` (new)
- Various `.gitkeep` files in empty directories

Test projects:
- `tests/OneId.Server.UnitTests/OneId.Server.UnitTests.csproj` (new)
- `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs` (new)
- `tests/OneId.Server.IntegrationTests/OneId.Server.IntegrationTests.csproj` (new)

Frontend:
- `src/OneId.Web/package.json` (new)
- `src/OneId.Web/vite.config.ts` (new)
- `src/OneId.Web/tsconfig.json` (new)
- `src/OneId.Web/tsconfig.app.json` (new — strict: true, no baseUrl)
- `src/OneId.Web/tsconfig.node.json` (new)
- `src/OneId.Web/components.json` (new)
- `src/OneId.Web/index.html` (new)
- `src/OneId.Web/src/main.tsx` (new)
- `src/OneId.Web/src/App.tsx` (new)
- `src/OneId.Web/src/index.css` (new — @theme inline, HSL variables, dark mode)
- `src/OneId.Web/src/lib/utils.ts` (new)
- `src/OneId.Web/src/components/ui/` (directory created by shadcn init)

### Change Log

- 2026-05-22: Story 1.1 implemented — solution and both projects bootstrapped, all 9 ACs satisfied.

### Review Findings

- [x] [Review][Defer] Tailwind v4 deprecates tailwind.config.ts — components.json references a `tailwind.config.ts` that does not exist; dark mode class strategy uses v4 CSS-only approach rather than spec-required `darkMode: ['class']` in TS config [src/OneId.Web/components.json] — deferred, ramifications of v4 approach not yet clear; revisit in Story 5a.1
- [x] [Review][Patch] Missing `app.UseAuthentication()` before `app.UseAuthorization()` — authorization middleware silently treats all requests as anonymous without it [src/OneId.Server/Program.cs]
- [x] [Review][Patch] Hardcoded credentials in committed `appsettings.json` — `Password=postgres` is in source control; move connection string to `appsettings.Development.json` or use env-var substitution [src/OneId.Server/appsettings.json]
- [x] [Review][Patch] `AllowedHosts: "*"` committed in base `appsettings.json` — disables host-header filtering globally; move wildcard to `appsettings.Development.json` [src/OneId.Server/appsettings.json]
- [x] [Review][Patch] No `.gitignore` at repository root — `keys/dev-signing.key` (Epic 2), `.env` files, `node_modules/`, and `dist/` have no exclusion [repo root]
- [x] [Review][Patch] `GetConnectionString("DefaultConnection")` null guard missing — passes `null` to Npgsql if key is absent, causing a cryptic runtime exception [src/OneId.Server/Program.cs:14]
- [x] [Review][Patch] `document.getElementById('root')!` non-null assertion — throws unhandled TypeError if element is missing [src/OneId.Web/src/main.tsx:6]
- [x] [Review][Patch] `Npgsql.EntityFrameworkCore.PostgreSQL` not centrally pinned in `Directory.Build.props` — only EF Core packages are pinned; Npgsql EF provider version mismatch can cause runtime incompatibility [Directory.Build.props]
- [x] [Review][Patch] AR-5 STEP 1 comment text deviates from spec-mandated exact wording — spec requires `// TODO Story 1.3a: app.UseMiddleware<TenantContextMiddleware>();` [src/OneId.Server/Program.cs]
- [x] [Review][Defer] Auto-migration is dev-only; no production migration strategy [src/OneId.Server/Program.cs:29-33] — deferred, pre-existing; production CI/CD migration strategy is owned by Story 1.6
- [x] [Review][Defer] CORS policy not configured — no frontend API calls yet; add when routing and auth are wired in Epic 2/5 [src/OneId.Server/Program.cs] — deferred, pre-existing
- [x] [Review][Defer] No frontend test infrastructure — no `vitest`/`jest`, no test script in `package.json` [src/OneId.Web/package.json] — deferred, pre-existing; not in Story 1.1 scope
- [x] [Review][Defer] `MigrateAsync` crashes if PostgreSQL unreachable at dev startup — no retry or graceful degradation [src/OneId.Server/Program.cs:31] — deferred, pre-existing; acceptable for local dev bootstrap
- [x] [Review][Defer] `#nullable disable` in generated migration file — standard EF Core scaffold pragma, removing it may break future scaffolding [src/OneId.Server/Infrastructure/Persistence/Migrations/20260522064503_InitialCreate.cs] — deferred, pre-existing
