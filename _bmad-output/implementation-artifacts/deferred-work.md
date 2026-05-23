# Deferred Work Log

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
