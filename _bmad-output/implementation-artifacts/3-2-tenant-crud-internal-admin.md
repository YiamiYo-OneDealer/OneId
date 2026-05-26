# Story 3.2: Tenant CRUD (Internal Admin)

Status: review

## Story

As an Internal Admin,
I want to create, read, update, and deactivate Tenants via the management API,
So that I can provision and manage the organizations that use OneId.

## Acceptance Criteria

**AC1: Create tenant**

**Given** an authenticated request to `POST /api/internal/tenants`
**When** the body contains a valid `name` (non-empty, ≤ 200 chars, unique)
**Then** a new `Tenant` row is created with `DeletedAt = null`, a generated `Id`, `CreatedAt`, and `UpdatedAt`
**And** the response is HTTP 201 with the full tenant representation including a `version` field (uint xmin read after insert)
**And** creating a tenant with a duplicate name returns HTTP 400 with `error = "name_taken"`

**AC2: List all tenants**

**Given** an authenticated request to `GET /api/internal/tenants`
**When** processed
**Then** all tenants where `DeletedAt IS NULL` are returned (soft-deleted tenants are excluded)
**And** a `TenantListIntegrationTest` asserts that with two active tenants seeded, both appear in the list

**AC3: Get single tenant**

**Given** an authenticated request to `GET /api/internal/tenants/{id}`
**When** the tenant exists and is not soft-deleted
**Then** the response is HTTP 200 with the full tenant representation including `version`
**And** a request for a non-existent or soft-deleted id returns HTTP 404

**AC4: Update tenant**

**Given** an authenticated request to `PATCH /api/internal/tenants/{id}`
**When** the request body contains a valid `name` and the current `version` (xmin)
**Then** the tenant `Name` and `UpdatedAt` are updated, and HTTP 200 with the updated representation is returned
**And** a PATCH with a stale `version` (xmin mismatch) returns HTTP 409 Conflict (AR-14 optimistic concurrency)
**And** a PATCH with a duplicate name returns HTTP 400 with `error = "name_taken"`

**AC5: Deactivate tenant (soft delete)**

**Given** an authenticated request to `DELETE /api/internal/tenants/{id}`
**When** the tenant exists and is not already soft-deleted
**Then** the tenant `DeletedAt` is set to `DateTimeOffset.UtcNow` and `UpdatedAt` is updated
**And** the response is HTTP 204 No Content
**And** a subsequent `GET /api/internal/tenants/{id}` returns HTTP 404

**AC6: Token issuance blocked for deactivated tenant**

**Given** a user belonging to a deactivated tenant (tenant `DeletedAt` is non-null)
**When** a password grant request is submitted to `POST /connect/token`
**Then** the server returns HTTP 400 with `error = "access_denied"` and `error_description = "Tenant account has been deactivated."`
**And** this check is added as an integration test in the existing password-auth test class

## Tasks / Subtasks

- [x] Task 1: Create Application/Internal/ query and command handlers (AC: 1–5)
  - [x] Create `src/OneId.Server/Application/Internal/Queries/ListTenantsQuery.cs` — `record ListTenantsQuery` + `ListTenantsHandler : IRequestHandler<ListTenantsQuery, List<TenantDto>>` pattern (no MediatR — plain service class, see Dev Notes)
  - [x] Create `src/OneId.Server/Application/Internal/Queries/GetTenantQuery.cs` — returns `TenantDto?`
  - [x] Create `src/OneId.Server/Application/Internal/Commands/CreateTenantCommand.cs` — returns `TenantDto`
  - [x] Create `src/OneId.Server/Application/Internal/Commands/UpdateTenantCommand.cs` — returns `TenantDto`; throws `DbUpdateConcurrencyException` on stale version
  - [x] Create `src/OneId.Server/Application/Internal/Commands/DeactivateTenantCommand.cs` — void/bool
  - [x] Each handler injects `InternalAdminContext` (marker) + `AppDbContext` — this is required for AR-8 boundary enforcement; `InternalAdminContext` signals cross-tenant data access intent
  - [x] Register all handlers in `Program.cs` via extension method `AddInternalAdminHandlers()` (AR-8: registrations isolated inside Application/Internal/)

- [x] Task 2: Create `InternalTenantsController` (AC: 1–5)
  - [x] Create `src/OneId.Server/Controllers/InternalTenantsController.cs`
  - [x] Route: `[Route("api/internal/tenants")]`, `[ApiController]`
  - [x] `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` — note: proper InternalAdmin role check deferred to Epic 4a
  - [x] Controller injects command/query handlers via constructor DI (NOT `InternalAdminContext` directly — AR-8)
  - [x] `GET /api/internal/tenants` → `ListTenantsHandler`
  - [x] `POST /api/internal/tenants` → `CreateTenantHandler`, returns `CreatedAtAction`
  - [x] `GET /api/internal/tenants/{id}` → `GetTenantHandler`, returns 404 if null
  - [x] `PATCH /api/internal/tenants/{id}` → `UpdateTenantHandler`, catches `DbUpdateConcurrencyException` → 409, catches name-taken exception → 400
  - [x] `DELETE /api/internal/tenants/{id}` → `DeactivateTenantHandler`, returns 204 or 404

- [x] Task 3: Add tenant active check to ConnectController (AC: 6)
  - [x] Modify `src/OneId.Server/Controllers/ConnectController.cs`
  - [x] After the user lookup in `HandlePasswordGrantAsync`, add: look up `db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId)` — if tenant is null or `DeletedAt != null`, return `access_denied`
  - [x] Use the exact error code/description from AC6 so integration tests can assert it precisely

- [x] Task 4: Write integration tests (AC: all)
  - [x] Create `tests/OneId.Server.IntegrationTests/InternalTenantsIntegrationTests.cs`
  - [x] Inherit `IntegrationTestBase` (TestContainers + Respawn)
  - [x] Auth via real OpenIddict token (two-step password+MFA flow using TotpUser — `TestTokenFactory` HMAC tokens not accepted by OpenIddict validation scheme)
  - [x] Add `[Trait("Category", "InternalAdmin")]` on the class
  - [x] Test: `POST` creates tenant → 201 with body including `version`
  - [x] Test: `POST` with duplicate name → 400 `name_taken`
  - [x] Test: `GET /api/internal/tenants` with two seeded tenants → both appear
  - [x] Test: `GET /api/internal/tenants/{id}` for existing tenant → 200
  - [x] Test: `GET /api/internal/tenants/{id}` for non-existent id → 404
  - [x] Test: `PATCH` with valid version → 200 updated
  - [x] Test: `PATCH` with stale version → 409
  - [x] Test: `DELETE` deactivates → 204, subsequent GET → 404
  - [x] Test: token issuance for deactivated tenant user → 400 `access_denied` (added to `PasswordAuthTests.cs`)
  - [x] `dotnet test` — 60 passed, 2 skipped (unchanged), 1 pre-existing flaky DevSigningKeyStabilityTest

- [x] Task 5: Verify AR-15 skip cap and ArchUnit boundary (AC: all)
  - [x] Skip count remains at 2 (`TestTokenFactoryContractTests`, `PermissionCatalogSyncTests`) — zero new skips
  - [x] `dotnet test --filter "Category=InternalAdmin"` → 17/17 pass
  - [x] `InternalBoundaryTests.cs` passes with new handlers (AR-8 enforced via `InternalServiceExtensions.AddInternalAdminHandlers()`)

## Dev Notes

### No Repository Pattern — Plain Handlers

The project does NOT use MediatR or a full CQRS library. The "commands/queries" in `Application/Internal/` are plain C# classes registered as scoped services, following the same direct-DI style as `AccountController`. Each handler is a single class with a constructor and one public method:

```csharp
// Application/Internal/Queries/ListTenantsQuery.cs
public sealed class ListTenantsHandler(InternalAdminContext _, AppDbContext db)
{
    public async Task<List<TenantDto>> HandleAsync(CancellationToken ct = default)
    {
        return await db.Tenants
            .Where(t => !t.DeletedAt.HasValue)
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(t.Id, t.Name, t.CreatedAt, t.UpdatedAt,
                EF.Property<uint>(t, "xmin")))
            .ToListAsync(ct);
    }
}
```

The `_` parameter for `InternalAdminContext` is intentional — it satisfies the AR-8 boundary rule (the class must declare a dependency on `InternalAdminContext` to be classified as an internal-only handler by ArchUnit).

Register in `Program.cs`:
```csharp
builder.Services.AddScoped<InternalAdminContext>();
builder.Services.AddScoped<ListTenantsHandler>();
builder.Services.AddScoped<GetTenantHandler>();
builder.Services.AddScoped<CreateTenantHandler>();
builder.Services.AddScoped<UpdateTenantHandler>();
builder.Services.AddScoped<DeactivateTenantHandler>();
```

### TenantDto — Reading xmin Shadow Property

`xmin` is a PostgreSQL shadow property — it's not on the C# entity. Use `EF.Property<uint>(t, "xmin")` in LINQ projections, or `db.Entry(entity).Property<uint>("xmin").CurrentValue` after loading an entity.

**DTO shape:**
```csharp
public sealed record TenantDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);   // xmin value — used for optimistic concurrency on PATCH
```

### Optimistic Concurrency on PATCH

The standard EF Core xmin pattern:
1. Client PATCHes with `{ "name": "new name", "version": 12345 }`
2. Handler loads the tenant: `var tenant = await db.Tenants.FindAsync(id)`
3. Set original concurrency token: `db.Entry(tenant).Property<uint>("xmin").OriginalValue = request.Version`
4. Mutate entity: `tenant.Name = request.Name; tenant.UpdatedAt = DateTimeOffset.UtcNow`
5. `await db.SaveChangesAsync()` — EF Core generates: `UPDATE tenants SET ... WHERE id = @id AND xmin = @original_xmin`
6. If 0 rows affected → `DbUpdateConcurrencyException` → catch in controller → return 409

**Controller catch pattern:**
```csharp
catch (DbUpdateConcurrencyException)
{
    return Conflict(new ProblemDetails
    {
        Title = "Conflict",
        Detail = "The tenant was modified by another request. Fetch the latest version and retry.",
        Status = StatusCodes.Status409Conflict
    });
}
```

### Unique Name Constraint — Catching DB Exception

The `TenantConfiguration` already has `builder.HasIndex(t => t.Name).IsUnique()`. On duplicate insert/update, Npgsql throws `PostgresException` with `SqlState = "23505"`. Catch it in the handler and wrap in a domain exception or return a result type:

```csharp
catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
{
    // name_taken
}
```

In the controller, translate this to:
```csharp
return BadRequest(new { error = "name_taken" });
```

### Token Issuance Guard — ConnectController

In `HandlePasswordGrantAsync` in `ConnectController`, after the user lookup (line where `var user = await db.Users.IgnoreQueryFilters()...`), add this check:

```csharp
var tenant = await db.Tenants
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

if (tenant is null || tenant.DeletedAt.HasValue)
{
    var error = OpenIddictResponse.Create(
        error: Errors.AccessDenied,
        errorDescription: "Tenant account has been deactivated.");
    return Forbid(error, ...);
}
```

Look at the existing `Forbid` call pattern in `ConnectController` for the exact shape (it uses `OpenIddictServerAspNetCoreDefaults.AuthenticationScheme` as the scheme).

### Auth on InternalTenantsController

Use `[Authorize]` only for this story. The architecture specifies `InternalAdmin` role but roles come from Epic 4a (no Role entity exists yet). Add a `// TODO Epic 4a: replace with [Authorize(Policy = "InternalAdmin")]` comment. Tests provide a valid JWT via `TestTokenFactory` — no role claims needed since the check is just `[Authorize]` (valid token only).

### Integration Test Auth Pattern

`IntegrationTestBase` provides `Factory` and `Client`. For authorized requests, add an `Authorization: Bearer <token>` header:

```csharp
var token = TestTokenFactory.CreateToken(
    tenantId: DevSeeder.DevTenantId,
    userId: DevSeeder.AdminUserId);
Client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);
```

But `Client` is shared — be careful to set this per-test or create a new client. Prefer `Factory.CreateClient()` per test to avoid header pollution between tests in the same class.

For the token-issuance-blocked test, use a `FormUrlEncodedContent` POST to `/connect/token`:
```csharp
var body = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "password",
    ["client_id"] = "oneid-dev-client",
    ["username"] = "admin@oneid.dev",
    ["password"] = "Admin123!",
    ["scope"] = "openid offline_access",
});
var response = await anonClient.PostAsync("/connect/token", body);
Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
var json = await response.Content.ReadFromJsonAsync<JsonElement>();
Assert.Equal("access_denied", json.GetProperty("error").GetString());
```

To test this: seed a second tenant with a user, soft-delete the tenant, then attempt password grant with that user's credentials.

### No Migration Needed

The `Tenant` entity already has all required fields (`Id`, `Name`, `CreatedAt`, `UpdatedAt`, `DeletedAt`) and `TenantConfiguration.cs` already defines:
- Unique index on `Name`
- xmin shadow property as concurrency token
- Soft-delete query filter

No `dotnet ef migrations add` is required for this story.

### Existing Patterns to NOT Duplicate

- `DevSeeder.SeedDevTenantAsync` shows the `IgnoreQueryFilters()` + soft-delete pattern for Tenant
- `AccountController` shows the controller pattern (inject `AppDbContext` directly, no repository)
- `InternalBoundaryTests.cs` shows how the ArchUnit rule is written — do NOT modify this test unless there is an actual boundary violation to address
- `IntegrationTestBase` + `OneIdWebApplicationFactory` (TestContainers + Respawn) — use this infrastructure for all integration tests

### AR-15 Deferred-Skip Governance

| Skip | Owner Story | Status after 3.2 |
|---|---|---|
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 2 / 3 cap** — zero new skips permitted in this story.

### Architecture Compliance Rules (Must Follow)

From `architecture.md` — All Implementation Agents MUST:
- `UseSnakeCaseNamingConvention()` — never add `[Column]` overrides
- `IEntityTypeConfiguration<T>` per entity — never configure in `OnModelCreating` directly
- `UseXminAsConcurrencyToken()` on all mutable entities (already done for Tenant in `TenantConfiguration.cs`)
- Problem Details for all API errors — no custom error shapes (except the specific `{ error: "name_taken" }` override for AC parity with existing error shapes in `AccountController`)
- `InternalAdminContext` only under `Application/Internal/` — ArchUnit enforced
- Soft-delete pattern for all entities (use `DeletedAt`, never physical deletes)

### File Structure

```
src/
  OneId.Server/
    Application/
      Internal/
        Queries/
          ListTenantsQuery.cs         ← NEW
          GetTenantQuery.cs           ← NEW
        Commands/
          CreateTenantCommand.cs      ← NEW
          UpdateTenantCommand.cs      ← NEW
          DeactivateTenantCommand.cs  ← NEW
    Controllers/
      InternalTenantsController.cs    ← NEW
      ConnectController.cs            ← MODIFY (tenant active check)
    Program.cs                        ← MODIFY (register new handlers)

tests/
  OneId.Server.IntegrationTests/
    InternalTenantsIntegrationTests.cs ← NEW
```

No frontend changes. No new migrations. No new npm packages.

### References

- AC source: `_bmad-output/planning-artifacts/epics.md` — Epic 3, Story 3.2
- Architecture rules: `_bmad-output/planning-artifacts/architecture.md` — "All Implementation Agents MUST", "InternalAdminContext", "AR-14 xmin"
- InternalAdminContext boundary enforcement: `tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs`
- Existing entity + config: `src/OneId.Server/Domain/Entities/Tenant.cs`, `src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs`
- Existing token issuance: `src/OneId.Server/Controllers/ConnectController.cs`
- Test infrastructure: `tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs`, `WebApplicationFactory.cs`, `TestTokenFactory.cs`
- DevSeeder: `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- CS9113 unused primary constructor parameter: C#12 `-warnaserror` treats `InternalAdminContext _` discard as error → store in `private readonly InternalAdminContext _ctx = internalAdminContext`
- ArchUnit AR-8 violation: `Program.cs` cannot directly reference `InternalAdminContext` even via fully-qualified name → created `InternalServiceExtensions.AddInternalAdminHandlers()` extension method inside `Application/Internal/`
- Auth scheme 401: bare `[Authorize]` with no default challenge scheme → must specify `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`
- TestTokenFactory HMAC tokens not accepted by OpenIddict validation → integration tests must use real tokens via password+MFA grant flow (TotpUser)
- AC6 status code: OpenIddict maps `Forbid()` to HTTP 400 (not 403) per OAuth2 spec → test must assert `BadRequest` not `Forbidden`

### Completion Notes List

- Created `TenantDto` record and `NameTakenException` in `Application/Internal/TenantDto.cs`
- Created 5 plain handler classes in `Application/Internal/Queries/` and `Application/Internal/Commands/`
- Created `InternalServiceExtensions.AddInternalAdminHandlers()` to isolate AR-8 DI registrations
- Created `InternalTenantsController` with OpenIddict validation scheme auth
- Modified `ConnectController.HandlePasswordGrantAsync` to block tokens for deactivated tenants
- Added AC6 test `PasswordGrant_DeactivatedTenant_ReturnsAccessDenied` to `PasswordAuthTests.cs`
- Created 11 integration tests in `InternalTenantsIntegrationTests.cs` covering all ACs
- Final: 60 passed, 2 skipped (unchanged), 1 pre-existing flaky `DevSigningKeyStabilityTest`

### File List

- `src/OneId.Server/Application/Internal/TenantDto.cs` — NEW
- `src/OneId.Server/Application/Internal/InternalServiceExtensions.cs` — NEW
- `src/OneId.Server/Application/Internal/Queries/ListTenantsQuery.cs` — NEW
- `src/OneId.Server/Application/Internal/Queries/GetTenantQuery.cs` — NEW
- `src/OneId.Server/Application/Internal/Commands/CreateTenantCommand.cs` — NEW
- `src/OneId.Server/Application/Internal/Commands/UpdateTenantCommand.cs` — NEW
- `src/OneId.Server/Application/Internal/Commands/DeactivateTenantCommand.cs` — NEW
- `src/OneId.Server/Controllers/InternalTenantsController.cs` — NEW
- `src/OneId.Server/Controllers/ConnectController.cs` — MODIFIED (tenant deactivation check)
- `src/OneId.Server/Program.cs` — MODIFIED (AddInternalAdminHandlers() extension method)
- `tests/OneId.Server.IntegrationTests/InternalTenantsIntegrationTests.cs` — NEW
- `tests/OneId.Server.IntegrationTests/PasswordAuthTests.cs` — MODIFIED (AC6 test)
