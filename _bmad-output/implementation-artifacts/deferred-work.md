# Deferred Work Log

## Deferred from: code review of 3-2-tenant-crud-internal-admin + 3-4-tenant-admin-designation-internal-admin (2026-05-26)

- **No role authorization on `InternalTenantsController`** (`src/OneId.Server/Controllers/InternalTenantsController.cs`) — any valid bearer token can call internal tenant admin endpoints. Intentional; Epic 4a adds `[Authorize(Policy = "InternalAdmin")]` gate.
- **`DeactivateTenantHandler` does not revoke active tokens** (`src/OneId.Server/Application/Internal/Commands/DeactivateTenantCommand.cs`) — users authenticated before deactivation can use tokens until expiry. Deactivate (soft-delete) is intentionally lighter than Suspend; Story 3.6 adds JTI revocation on Suspend.
- **TOCTOU race in `RemoveTenantAdminHandler` last-admin check** (`src/OneId.Server/Application/Internal/Commands/RemoveTenantAdminCommand.cs`) — concurrent remove requests can both pass the `CountAsync` check and both succeed, leaving a tenant with zero admins. Requires explicit transaction/row-lock; acceptable for v1 low-concurrency admin operations.
- **`DesignateTenantAdminHandler` allows designation on soft-deleted tenants** (`src/OneId.Server/Application/Internal/Commands/DesignateTenantAdminCommand.cs`) — `IsTenantAdmin` can be set on users belonging to a deactivated tenant. Harmless because deactivated-tenant users are blocked at token issuance.

## Deferred from: code review of 3-6 + 3-8 (2026-05-26)

- **`AppDbContext.cs` comment L41 incorrectly lists `AuditLog` in `UseXminAsConcurrencyToken` group** — AuditLog is append-only; no xmin is applied. Comment is a copy-paste artefact. Cosmetic only; update when `OnModelCreating` is next touched.

## Deferred from: code review of 3-1-itenant-context-middleware-and-tenant-isolation-regression-tests (2026-05-26)

- **`SeedSecondTenantAsync` seeds `Tenant` without initialized `TenantContext`** (`TenantIsolationRegressionTests.cs:SeedSecondTenantAsync`) — safe today because `Tenant` has no query filter and EF Core applies `HasQueryFilter` only to SELECTs, not INSERTs. Fragile if Epic 4a adds a tenant-scoped filter to `Tenant`. Add `.IgnoreQueryFilters()` on any future entity seeds that run without a tenant context.
- **Unauthenticated request reaching downstream EF code (without `IgnoreQueryFilters`) throws 500 instead of 401** (`TenantContextMiddleware` / `AppDbContext`) — architectural concern: if any middleware or authorization policy handler touches `AppDbContext.Users` on an unauthenticated path, the guard fires with `InvalidOperationException` (500) rather than returning 401. Document at the middleware level when adding new protected routes that could reach EF before auth short-circuits.

## Deferred from: code review of 5c-5-audit-log-ui + 5c-6-commandpalette (2026-05-26)

- **No error state for audit log pages** (`routes/tenant/audit-log.tsx`, `routes/internal/audit-log.tsx`) — consistent with all other pages in the app; all use mock data with no real failure path. Revisit when real API is wired.
- **DataTable row click creates new arrow function per render** (`components/shared/DataTable.tsx`) — no row-level memoization in place today; harmless until rows are individually memoized.
- **`JSON.stringify` on payload with circular refs/BigInt throws in AuditEventSheet** (`components/shared/AuditEventSheet.tsx`) — mock data only, no real risk until real API payloads flow through.

## Deferred from: code review of 1-1-initialize-backend-and-frontend-projects (2026-05-22)

- **Tailwind v4 / missing `tailwind.config.ts`** (`src/OneId.Web/components.json`) — Tailwind v4 deprecates `tailwind.config.ts`; `components.json` references a file that doesn't exist; dark mode uses v4 CSS-only approach instead of spec-required `darkMode: ['class']` in TS config. Reason: ramifications of v4 approach not yet clear. Revisit in Story 5a.1.

- **Auto-migration is dev-only; no production migration strategy** (`src/OneId.Server/Program.cs:29-33`) — `db.Database.MigrateAsync()` runs only in Development. Production migration strategy (CLI entrypoint, deployment pipeline step) is owned by Story 1.6.
- **CORS policy not configured** (`src/OneId.Server/Program.cs`) — No `AddCors()`/`UseCors()` calls. No frontend API calls exist yet; add when routing and auth are wired in Epic 2/5a.
- **No frontend test infrastructure** (`src/OneId.Web/package.json`) — No `vitest`/`jest`, no `test` script. Frontend testing is not in Story 1.1 scope; add alongside first testable frontend feature.
- **`MigrateAsync` crashes if PostgreSQL unreachable at dev startup** (`src/OneId.Server/Program.cs:31`) — No retry policy or graceful degradation around startup migration. Acceptable for local dev bootstrap; revisit if Docker Compose health checks in Story 1.2 don't cover it.
## Deferred from: code review of 1-2-local-development-stack-docker-compose (2026-05-22)

- **Hardcoded dev credentials (postgres:postgres) in docker-compose.yml** — intentional for local dev; must not be reused for staging/prod. Use secrets management or a `.env` file excluded from git when moving up-stack.
- **Unpinned `latest` image tags for seq and otel-collector** — reproducibility concern; a `docker compose pull` can silently break the stack. Pin to explicit versions when the dev stack stabilizes.
- **OTEL collector receiver exposed on 0.0.0.0 with no authentication** — acceptable for local dev; requires auth extension configuration for any shared or production deployment.
- **No `restart` policy on `oneid-server`** — a transient migration failure permanently stops the container. Add `restart: on-failure` or `restart: unless-stopped` when the stack is used beyond solo dev.
- **Serilog OTLP sink has `OtlpProtocol.Grpc` hardcoded** — if `OTEL_EXPORTER_OTLP_ENDPOINT` is ever changed to an HTTP/protobuf endpoint (4318), the Serilog sink will fail silently while the OTEL SDK exporter adapts. Reconsider when OTEL transport is changed.
- **No Serilog minimum level configured** — `appsettings.json` `Logging:LogLevel` entries are ignored by Serilog; EF Core debug SQL logs may flood output. Story 1.4 enricher work should add a `MinimumLevel` block in Serilog config.
- **Only `OneId.Server.csproj` copied before `dotnet restore` in Dockerfile** — will cause restore failure when shared library project references are introduced. Fix Dockerfile COPY layer when first project reference is added.
- **Seq healthcheck assumes `curl` present in `datalust/seq` image** — validated against current version; fragile if Seq switches to a more minimal base image. Use `wget` fallback or switch to `nc`/`httpie` if seq healthcheck breaks on image update.
- **`AllowedHosts: "localhost"` in appsettings.json** — will block service-to-service HTTP requests when other containers call `oneid-server` with a non-localhost Host header. Add `appsettings.Docker.json` with `AllowedHosts: "*"` when first inter-service HTTP call is introduced.
- **`pg_isready` may pass before `POSTGRES_DB` init script completes** — theoretical race where `oneid-server` connects before `oneid_dev` DB exists. Monitor for "database does not exist" failures; if observed, switch to a `psql -c "SELECT 1"` healthcheck scoped to the target DB.

## Deferred from: code review of 1-3a-tenant-context-middleware-and-registration-order-enforcement (2026-05-22)

- **Thread-safety on `TenantContext._tenantId`** (`src/OneId.Server/Application/Common/TenantContext.cs:5`) — No `volatile` or `Interlocked` protection on the nullable Guid backing field. Architectural assumption: HTTP request scope is single-threaded (same as DbContext). Revisit if scope is ever reused across threads or in background services.
- **Unauthenticated HTTP path through `TenantContextMiddleware` not integration-tested** (`tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs:86`) — `TestAuthHandler` always returns `Success`; no test covers a real unauthenticated HTTP request through the middleware. Add a negative integration test when anonymous endpoints that access `ITenantContext` are introduced.

- **`#nullable disable` in generated migration file** (`Migrations/20260522064503_InitialCreate.cs`) — EF Core scaffolds migrations with this pragma by default. Removing it may break future `dotnet ef migrations add` output consistency.

## Deferred from: code review of Epic 1 (2026-05-23)

- **Guid.Empty row data-leak vector** (`AppDbContext.cs`) — a row with `tenant_id = '00000000...'` would be visible to all uninitialized `ITenantContext` contexts. Requires actively bad data; `TenantContext.Initialize` rejects Guid.Empty on normal code paths. Revisit if direct-SQL seeding is ever introduced.
- **No FK constraint `users.tenant_id → tenants.id`** (`UserConfiguration.cs`) — referential integrity not enforced at DB level. May be intentional for multi-tenant flexibility. Revisit in Epic 3 when Tenant lifecycle (suspend/reinstate) is built.
- **Cross-tenant isolation tests (1.3b AC3) not confirmed in Group 1 diff** — verify presence in Group 2 test file review (`DevSeederIntegrationTests.cs`).
- **DevSeeder hard-codes `"Admin123!"` in source** (`DevSeeder.cs`) — dev-only by `IsDevelopment()` guard; by spec. Revisit if staging environment ever uses this seeder.
- **`User.PasswordHash` nullable ambiguity** (`User.cs`) — `null` could mean "federated user (no password)" or "pre-migration user". Auth logic in Epic 2 must define the contract; consider a `HasPassword` flag or sentinel value at that point.
- **`ICacheService.Set` no default TTL** (`MemoryCacheService.cs`) — entries with `expiry = null` have no expiration and no size limit, risking unbounded memory growth. Enforce TTL policy at first caching consumer (Epic 3/4a).
- **`DevSeeder` no wrapping transaction** (`DevSeeder.cs`) — stable well-known IDs make re-runs idempotent. Revisit if seeder grows in complexity with inter-dependent records.
- **`SeedAdminUserAsync` unique-constraint risk** (`DevSeeder.cs`) — checks by `AdminUserId`; a different-ID user with `email = admin@oneid.dev` would abort startup. Dev-only, requires manual DB interference to trigger.

## Deferred from: code review of Epic 1 Group ② tests (2026-05-23)

- **Respawner wipes `HasData()` seed rows** (`Helpers/WebApplicationFactory.cs:39-42`) — `Respawner` truncates all tables (except `__EFMigrationsHistory`) back to post-migration baseline, which also deletes any rows inserted by EF Core `HasData()` calls inside migrations. When Epic 4a adds `PermissionCatalog` seeding via `HasData()`, `ResetDatabaseAsync` will wipe those rows before each test. Fix before Epic 4a: add permission-catalog tables to `TablesToIgnore`, or switch to a dedicated seed method called after Respawn reset.

## Deferred from: code review of Epic 5a (2026-05-23)

- **TenantSwitchQueryInvalidationTest tests key factory but not layout URL-change path** (`src/OneId.Web/src/routes/internal/tenants/_layout.tsx`) — the existing test validates key shape in isolation; no test exercises the `useEffect` → `queryClient.invalidateQueries` path triggered by a real URL parameter change. Revisit in Epic 5c when Playwright flows exercise tenant navigation.
- **ESLint `globalIgnores` disables all rules for `src/components/ui/**`** (`src/OneId.Web/eslint.config.js`) — pragmatic workaround for shadcn-generated files; pre-existing pattern. Revisit if custom code is ever added inside `src/components/ui/`.
- **`getSortedRowModel()` unconditionally in table options with `manualSorting=true`** (`src/OneId.Web/src/components/shared/DataTable.tsx:69`) — no-op per TanStack Table docs but registers a row model each render for rows that won't be client-sorted. Conditionally include only when `manualSorting=false`; defer until server-side sort is actively used in Epic 5c to confirm the optimization is needed.
- **`Breadcrumbs.tsx` has no test coverage** (`src/OneId.Web/src/components/shared/Breadcrumbs.tsx`) — no `Breadcrumbs.test.tsx`; UUID filter logic (P6 patch), breadcrumb rendering, and separator placement are untested. Add tests alongside the P6 UUID filter patch.
- **Custom `toHaveNoViolations` matcher in `test-setup.ts` may conflict on `vitest-axe` upgrade** (`src/OneId.Web/src/test-setup.ts`) — manually defined because `vitest-axe@0.1.0` does not export one; if a future release ships an official matcher, the custom `expect.extend` will shadow it. Monitor and remove the custom definition when upgrading.
- **Frontend vitest tests (33 passing) not wired in CI** (`.github/workflows/ci.yml`) — no CI job runs `npm test`; the only frontend job is `playwright-tests` with `if: false`. Defer to Epic 5c when Playwright is enabled; add a `vitest-tests` job (checkout → setup-node → `npm ci` → `npm test -- --run`) alongside.

## Deferred from: code review of 5c-1b-tenant-management-crud-forms (2026-05-23)

- **`columns` array defined inside component body** (`TenantRolesPage.tsx`, `TenantRoleSetsPage.tsx`, `TenantGroupsPage.tsx`, `TenantUsersPage.tsx`) — columns moved inside component to close over state setters; should be wrapped in `useMemo` to avoid TanStack Table internal churn. Defer to a future cleanup pass.
- **All row action buttons disabled while any single mutation is pending** (`TenantUsersPage.tsx` and delete columns on other pages) — shared hook instance (`updateUser`, `deleteGroup`, etc.) blocks all row buttons when any one row is in flight. Acceptable for Phase 5 mock demo; fix when pages connect to a real API.
- **Email validation uses only `@` presence check** (`TenantUsersPage.tsx`) — too weak for an IDP. Upgrade to a proper regex or library validation in Phase 2 when real auth is wired.
- **`CheckboxList`/`RoleSelectList`/`PermissionSelect`/`GroupSelectList` duplicated 4 times** — near-identical filtered checkbox list components across all 4 page files. `CheckboxList` is already generic; refactor the others to reuse it in a future cleanup.
- **`deleteGroup` in mockStore is silent no-op for non-existent ID** (`store.ts`) — filter returns empty, `onSuccess` fires, UI shows success. Idempotent in practice since IDs come from the list; revisit when a real API is wired.
- **`deleteRoleSet` 409 error message is unbounded** (`TenantRoleSetsPage.tsx`, `TenantRolesPage.tsx`) — `assignedTo.join(', ')` has no truncation or count limit. Harmless with fixture data; add truncation (e.g., "… and N more") before production use.
- **`tenantId` defaults to `''`** (all 4 page files) — pre-existing pattern throughout codebase; routing failure surfaces the problem before any query fires. No change needed until real API validation is introduced.
- **`Date.now()` ID collision across tenants in mockStore** (`store.ts`) — all `createX` methods use `Date.now()` suffix for IDs; theoretical cross-tenant collision if two creates happen in the same millisecond. Pre-existing; use `crypto.randomUUID()` when mock store is replaced with real API.
- **`memberCount` stays stale** (`TenantGroupsPage.tsx`, `store.ts`) — denormalised counter from fixtures; not updated when users join groups. Explicitly accepted mock limitation per story completion notes.
- **`useDeleteGroup` does not invalidate individual group query** (`useGroups.ts`) — `onSuccess` only invalidates the list key. No detail view exists yet; add individual key invalidation when a group detail page is introduced.
- **Inactive permissions surfaced without filtering** (`TenantRolesPage.tsx`) — `Permission.isActive` field exists in types but all fixtures are `isActive: true`. No practical impact now; add `filter(p => p.isActive)` in `PermissionSelect` before any inactive permission is added to fixtures.

## Deferred from: code review of 5c-1c-user-edit-dialog (2026-05-23)

## Deferred from: code review of 5c-4-f-3-tenant-provisioning-stepper (2026-05-24)

- **Admin creation misleading error after tenant created** (`TenantProvisioningPage.tsx:322-332`) — if `mockStore.createUser` throws after `createTenant.mutateAsync` succeeds, the catch block shows "Failed to create tenant" when the tenant was in fact created. Demo scope / mock pattern; split into tenant-success + admin-error messaging when real API is wired.
- **Email validation accepts `@`, `foo@`, `@bar`** (`TenantProvisioningPage.tsx:298`) — `@`-presence check only; pre-existing pattern from 5c-1b, explicitly deferred to Phase 2 in story dev notes.
- **`blocker.reset?.()` / `blocker.proceed?.()` optional chaining silently no-ops** (`TenantProvisioningPage.tsx:460-461`) — if RR7 minor removes `reset`/`proceed` from the blocker object, dialog buttons do nothing. Covered by the `unstable_useBlocker` code comment; monitor on RR7 upgrades.
- **Route order fragility: `tenants/new` before `tenants/:tenantId`** (`routes/index.tsx:36`) — no guard prevents reordering during maintenance. Static segment resolution makes it safe today; add a test assertion or comment guard when route file is next touched.
- **`parseInt` imprecision for seat counts > `Number.MAX_SAFE_INTEGER`** (`TenantProvisioningPage.tsx:284`) — demo scope; add a max-value guard when the real API enforces seat limits.
- **Tests use `fireEvent` instead of `userEvent`** (`TenantProvisioningPage.test.tsx`) — blur-triggered validation paths not exercised by the test suite. Pre-existing pattern across all codebase tests; upgrade when `@testing-library/user-event` is adopted project-wide.

- **Stale `editUser` snapshot can clobber concurrent Activate/Deactivate status change** (`TenantUsersPage.tsx`) — `editUser` state is captured at click time; if Activate/Deactivate fires and TanStack Query re-fetches while the dialog is open, saving writes back the old status. Low practical risk in demo with mock data; revisit when pages connect to a real API.
- **`onOpenChange` silently blocks close while save is in-flight** (`TenantUsersPage.tsx`) — Escape/backdrop close is swallowed with no visual feedback when `updateUser.isPending` is true. Intentional per 5c-1b delete-dialog pattern; consider adding a loading indicator or disabled-X visual for production use.
- **Weak email validation (`@` presence only)** (`TenantUsersPage.tsx` `validateEmail`) — pre-existing pattern from 5c-1b; upgrade to proper regex or library validation in Phase 2 when real auth is wired.
- **`user.groupIds` has no null-fallback in EditUserDialog** (`TenantUsersPage.tsx`) — mock data always provides the array; add `user.groupIds ?? []` when moving to a real API that may return partial user objects.
