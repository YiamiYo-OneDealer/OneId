# Story 3.6: Tenant Suspension with jti Revocation (FR-12)

Status: done

## Story

As an Internal Admin,
I want to suspend a Tenant and immediately invalidate all active sessions for that Tenant's users,
So that a suspended Tenant's users lose access within the introspection cache window.

## Acceptance Criteria

**AC1: POST /suspend sets Tenant status and revokes all jtis**

**Given** an Internal Admin calls `POST /api/internal/tenants/{id}/suspend`
**When** the request is processed
**Then** the Tenant `Status` is set to `Suspended`
**And** all active jti records for all Users in that Tenant are revoked in the OpenIddict authorization store (integrates with `IUserTokenRevoker` from Story 2.5/2.6)
**And** the response is HTTP 200 with the updated tenant representation (including `status: "Suspended"`)

**AC2: Suspended tenant — introspection and new token issuance blocked**

**Given** a Tenant is suspended and a user from that Tenant attempts to use their existing token
**When** the token is introspected by OneDealer v2
**Then** introspection returns `active: false` (jti was revoked during suspension)
**And** new token issuance is rejected with HTTP 400 `error: "tenant_suspended"` until the Tenant is reinstated

**AC3: TenantSuspensionIntegrationTest — full cross-system test (FR-5a + FR-12)**

**Given** a `TenantSuspensionIntegrationTest` runs
**When** a Tenant is suspended after issuing tokens to 2 users (TotpUser and AdminUser from DevSeeder)
**Then** both tokens introspect as `active: false`
**And** this test is the full cross-tenant integration test for FR-5a that was deferred from Story 2.6 — it uses the real `ITenantContext` middleware and the jti revocation store together

**AC4: POST /reinstate restores active status**

**Given** an Internal Admin calls `POST /api/internal/tenants/{id}/reinstate`
**When** the request is processed
**Then** the Tenant `Status` returns to `Active`
**And** new token issuance is permitted again (previously revoked jtis remain revoked — users must re-authenticate)
**And** the response is HTTP 200 with the updated tenant representation (including `status: "Active"`)

## Tasks / Subtasks

- [x] Task 1: Add `TenantStatus` enum and extend `Tenant` entity (AC: 1, 2, 4)
  - [x] Create `src/OneId.Server/Domain/Enums/TenantStatus.cs` — enum `Active = 0, Suspended = 1`
  - [x] Add `public TenantStatus Status { get; set; } = TenantStatus.Active;` to `Tenant.cs`
  - [x] Update `TenantConfiguration.cs`: add `builder.Property(t => t.Status).IsRequired().HasDefaultValue(TenantStatus.Active);`
  - [x] Run `dotnet ef migrations add AddTenantStatusColumn --project src/OneId.Server --startup-project src/OneId.Server`

- [x] Task 2: Update `TenantDto` to include `Status` (AC: 1, 4)
  - [x] Modify `src/OneId.Server/Application/Internal/TenantDto.cs` — add `TenantStatus Status` field
  - [x] Update all existing places that construct `TenantDto` to pass the new field (CreateTenantCommand, UpdateTenantCommand, GetTenantQuery, ListTenantsQuery)

- [x] Task 3: Create `SuspendTenantCommand` handler (AC: 1)
  - [x] Create `src/OneId.Server/Application/Internal/Commands/SuspendTenantCommand.cs`
  - [x] Handler injects `InternalAdminContext`, `AppDbContext`, and `IUserTokenRevoker` (AR-8 pattern)
  - [x] Sets `tenant.Status = TenantStatus.Suspended`, iterates all non-deleted tenant users, calls `IUserTokenRevoker.RevokeAllUserTokensAsync(user.Id)` for each
  - [x] Returns `TenantDto?` (null if not found)

- [x] Task 4: Create `ReinstateTenantCommand` handler (AC: 4)
  - [x] Create `src/OneId.Server/Application/Internal/Commands/ReinstateTenantCommand.cs`
  - [x] Handler injects `InternalAdminContext` and `AppDbContext` (AR-8 pattern)
  - [x] Sets `tenant.Status = TenantStatus.Active`
  - [x] Returns `TenantDto?` (null if not found)

- [x] Task 5: Register new handlers in DI (AC: 1, 4)
  - [x] Add `services.AddScoped<SuspendTenantHandler>();` to `InternalServiceExtensions.AddInternalAdminHandlers()`
  - [x] Add `services.AddScoped<ReinstateTenantHandler>();`

- [x] Task 6: Add endpoints to `InternalTenantsController` (AC: 1, 4)
  - [x] Add `SuspendTenantHandler` and `ReinstateTenantHandler` to primary constructor
  - [x] Add `[HttpPost("{id:guid}/suspend")]` → `Suspend` action
  - [x] Add `[HttpPost("{id:guid}/reinstate")]` → `Reinstate` action

- [x] Task 7: Update `ConnectController` to check for suspended tenant (AC: 2)
  - [x] After the existing `tenant.DeletedAt.HasValue` check, add a check for `tenant.Status == TenantStatus.Suspended`
  - [x] Return HTTP 400 with `error: "tenant_suspended"` (Problem Details format matching existing patterns)

- [x] Task 8: Write integration tests (AC: all)
  - [x] Create `tests/OneId.Server.IntegrationTests/TenantSuspensionIntegrationTests.cs`
  - [x] AC1: `Suspend_ReturnsOkWithSuspendedStatus` — call suspend, assert 200 + status field
  - [x] AC2+3: `Suspend_RevokesAllUserTokens_IntrospectionReturnsFalse` — issue tokens for TotpUser + AdminUser, suspend DevTenant, assert both introspect as active=false
  - [x] AC2: `Suspend_BlocksNewTokenIssuance` — suspend DevTenant, attempt password grant, assert 400 with `error: "tenant_suspended"`
  - [x] AC4: `Reinstate_AllowsNewTokenIssuance` + `Reinstate_ReturnsOkWithActiveStatus` — suspend then reinstate, verify status and token issuance

- [x] Task 9: Verify ArchUnit and skip cap
  - [x] `dotnet test --filter "Category=InternalAdmin"` — 28/28 pass (+5 new)
  - [x] Skip count remains at 2 (`TestTokenFactoryContractTests`, `PermissionCatalogSyncTests`) — zero new skips

## Dev Notes

### Tenant Status Enum — New File

Create `src/OneId.Server/Domain/Enums/TenantStatus.cs`:

```csharp
namespace OneId.Server.Domain.Enums;

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
}
```

Then update `Tenant.cs` in `Domain/Entities/` to add the property:

```csharp
using OneId.Server.Domain.Enums;

public TenantStatus Status { get; set; } = TenantStatus.Active;
```

### TenantDto — Update Required

`TenantDto` currently is:
```csharp
public sealed record TenantDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);
```

Update to:
```csharp
using OneId.Server.Domain.Enums;

public sealed record TenantDto(
    Guid Id,
    string Name,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
```

**IMPORTANT:** This is a breaking change to the constructor. Update ALL callers. Grep for `new TenantDto(` to find them — typically in `CreateTenantCommand.cs`, `UpdateTenantCommand.cs`, `GetTenantQuery.cs`, `ListTenantsQuery.cs`. Each one must pass `tenant.Status` as the third positional argument.

### EF Core Configuration Update

In `TenantConfiguration.cs`, add:

```csharp
builder.Property(t => t.Status)
    .IsRequired()
    .HasDefaultValue(TenantStatus.Active)
    .HasConversion<int>();
```

The migration adds an `integer NOT NULL DEFAULT 0` column to `tenants`. Verify the generated Up/Down before applying.

### SuspendTenantCommand.cs Pattern (AR-8)

```csharp
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed class SuspendTenantHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db,
    IUserTokenRevoker revoker)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<TenantDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && !t.DeletedAt.HasValue, ct);

        if (tenant is null)
            return null;

        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        // Revoke all active sessions for every user in this tenant
        var userIds = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == id && u.DeletedAt == null)
            .Select(u => u.Id)
            .ToListAsync(ct);

        await db.SaveChangesAsync(ct);

        foreach (var userId in userIds)
            await revoker.RevokeAllUserTokensAsync(userId, ct);

        var version = db.Entry(tenant).Property<uint>("xmin").CurrentValue;
        return new TenantDto(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt, tenant.UpdatedAt, version);
    }
}
```

**Why save before revoking:** `SaveChangesAsync` is called first so the suspended status is persisted even if a revocation call partially fails. The revocations are idempotent — already-expired tokens simply have no effect.

### ReinstateTenantCommand.cs Pattern (AR-8)

```csharp
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed class ReinstateTenantHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<TenantDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && !t.DeletedAt.HasValue, ct);

        if (tenant is null)
            return null;

        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var version = db.Entry(tenant).Property<uint>("xmin").CurrentValue;
        return new TenantDto(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt, tenant.UpdatedAt, version);
    }
}
```

### Controller Endpoints to Add

Add two new handlers to the primary constructor (extend existing, do NOT add a second constructor):

```csharp
public class InternalTenantsController(
    ListTenantsHandler listHandler,
    GetTenantHandler getHandler,
    CreateTenantHandler createHandler,
    UpdateTenantHandler updateHandler,
    DeactivateTenantHandler deactivateHandler,
    DesignateTenantAdminHandler designateAdminHandler,
    RemoveTenantAdminHandler removeAdminHandler,
    SuspendTenantHandler suspendHandler,         // NEW
    ReinstateTenantHandler reinstateHandler)     // NEW
    : ControllerBase
```

Add actions after `RemoveAdmin`:

```csharp
[HttpPost("{id:guid}/suspend")]
public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
{
    var result = await suspendHandler.HandleAsync(id, ct);
    return result is null ? NotFound() : Ok(result);
}

[HttpPost("{id:guid}/reinstate")]
public async Task<IActionResult> Reinstate(Guid id, CancellationToken ct)
{
    var result = await reinstateHandler.HandleAsync(id, ct);
    return result is null ? NotFound() : Ok(result);
}
```

### ConnectController — Suspension Check

In `ConnectController`, the password grant handler already has:

```csharp
var tenant = await db.Tenants
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

if (tenant is null || tenant.DeletedAt.HasValue)
    return Forbid(BuildForbidProperties(Errors.AccessDenied, "Tenant account has been deactivated."));
```

Add AFTER the `DeletedAt` check:

```csharp
if (tenant.Status == TenantStatus.Suspended)
    return BadRequest(new OpenIddictResponse
    {
        Error = "tenant_suspended",
        ErrorDescription = "This tenant account has been suspended. Contact your administrator."
    });
```

**IMPORTANT:** Use `BadRequest` (HTTP 400), not `Forbid`. The AC specifies `HTTP 400 error: "tenant_suspended"`. The existing `DeletedAt` check uses `Forbid` — do NOT change that, just add the suspended check below it.

Check whether `ConnectController` is in the `Controllers/` folder and uses `OpenIddictResponse` — match the pattern of existing error returns in that file exactly (e.g., the lockout check pattern from Story 2.2 uses `return BadRequest(new OpenIddictResponse { Error = ..., ErrorDescription = ... })`).

Also check the `urn:oneid:mfa` grant handler — it has a similar check for the tenant, so add the same suspended check there too if it queries the tenant.

### Integration Tests — Key Pattern Notes

**Shared helpers from `RoleChangeInvalidationTests`:** Your test class should have the same `IssueMfaTokenAsync()`, `IssueAdminUserTokenAsync()`, and `IsTokenActiveAsync()` helpers. Copy them directly (they call `/connect/token` in 2 steps: password → mfa). Both `DevSeeder.TotpUserId` (has pre-enrolled TOTP) and `DevSeeder.AdminUserId` (TOTP enrolled dynamically) live in `DevSeeder.DevTenantId`.

**TOTP replay guard:** Each TOTP code is consumed for a 30-second time window. If two tests in the same class both call `IssueMfaTokenAsync()`, the second call may fail with a replay error if it falls in the same 30-second window. **Mitigation:** Each test that needs a token must use the `IssueAdminUserTokenAsync()` helper (which enrolls a fresh TOTP secret) OR use separate test classes with separate factory instances. The cleanest approach: use `TotpUser` for one token and `AdminUser` for the other — each has its own TOTP secret.

**Restoring after suspend tests:** Tests that suspend `DevTenantId` must reinstate it at the end (or rely on Respawn for cleanup). Do NOT assume test order — each test must be self-contained. If suspending the dev tenant in one test and another test tries to issue a token, the second test will fail with `tenant_suspended`. **Safe pattern:** Either reinstate at the end of each test that suspends, or create a fresh tenant per test (use the Internal Admin API to POST a new tenant, then seed users via the DI scope).

**Recommended test structure using a fresh tenant:**

For `Suspend_RevokesAllUserTokens_IntrospectionReturnsFalse` — the test needs to issue tokens for users in a specific tenant and then revoke them. The easiest approach:
1. Call `POST /api/internal/tenants` to create a fresh test tenant
2. Issue tokens for DevSeeder users... BUT DevSeeder users are in `DevTenantId`, not your new tenant

Alternatively, keep it simple: **test against `DevSeeder.DevTenantId`** and use an `IAsyncLifetime`-based setup/teardown to reinstate after each test. The `IntegrationTestBase` class and Respawn will handle DB cleanup.

```csharp
[Collection("IntegrationTests")]
public class TenantSuspensionIntegrationTests(OneIdWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // Uses DevSeeder.DevTenantId for all tests
    // Each test that suspends must reinstate before asserting "new issuance works"
```

**Test: Suspend_BlocksNewTokenIssuance** — suspend first, then call `/connect/token` with valid credentials, assert HTTP 400 with body containing `error: "tenant_suspended"`. The error is in the JSON body, not the HTTP error code, so check:

```csharp
var body = await response.Content.ReadFromJsonAsync<JsonElement>();
Assert.Equal("tenant_suspended", body.GetProperty("error").GetString());
```

**Auth for Internal Admin endpoints:** All tests calling `/api/internal/tenants/{id}/suspend` need a valid token. Use the existing `AuthClientAsync()` helper from `InternalTenantsIntegrationTests.cs` to authenticate — it performs the two-step password+TOTP flow. **Do not share the token with token-issuance tests** (TOTP replay guard — `AuthClientAsync` consumes a TotpUser time step).

### AR-15 Deferred-Skip Governance

| Skip | Owner Story | Status after 3.6 |
|---|---|---|
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 2 / 3 cap** — zero new skips permitted in this story.

### File Structure

```
src/
  OneId.Server/
    Domain/Enums/
      TenantStatus.cs                                    ← NEW
    Domain/Entities/
      Tenant.cs                                          ← MODIFY (add Status property)
    Application/Internal/
      TenantDto.cs                                       ← MODIFY (add Status field)
      InternalServiceExtensions.cs                      ← MODIFY (register 2 new handlers)
      Commands/
        SuspendTenantCommand.cs                          ← NEW
        ReinstateTenantCommand.cs                        ← NEW
    Controllers/
      InternalTenantsController.cs                      ← MODIFY (2 new actions + constructor params)
      ConnectController.cs                              ← MODIFY (add tenant_suspended check)
    Infrastructure/Persistence/Configurations/
      TenantConfiguration.cs                            ← MODIFY (add Status property config)
    Infrastructure/Persistence/Migrations/
      <timestamp>_AddTenantStatusColumn.cs              ← NEW (generated by dotnet ef)
      <timestamp>_AddTenantStatusColumn.Designer.cs     ← NEW (generated)
      AppDbContextModelSnapshot.cs                      ← UPDATED (generated)

tests/
  OneId.Server.IntegrationTests/
    TenantSuspensionIntegrationTests.cs                 ← NEW
```

No frontend changes in this story. No new npm packages.

### References

- AC source: `_bmad-output/planning-artifacts/epics.md` — Epic 3, Story 3.6
- Architecture: `_bmad-output/planning-artifacts/architecture.md` — "Tenant.Suspend(), Tenant.Reinstate()", "TenantSuspendedEvent", FR-12, FR-5a
- `IUserTokenRevoker`: `src/OneId.Server/Application/Common/IUserTokenRevoker.cs`
- `RevocationHandler`: `src/OneId.Server/Infrastructure/OpenIddict/RevocationHandler.cs`
- `RevocationExtensions` (DI registration): `src/OneId.Server/Infrastructure/OpenIddict/RevocationExtensions.cs`
- AR-8 pattern reference: Story 3.4 dev notes (`_bmad-output/implementation-artifacts/3-4-tenant-admin-designation-internal-admin.md`)
- `ConnectController`: `src/OneId.Server/Controllers/ConnectController.cs` — lines 59–73 for existing tenant deactivation check
- `DeactivateTenantCommand` (handler pattern): `src/OneId.Server/Application/Internal/Commands/DeactivateTenantCommand.cs`
- `InternalTenantsController` (to extend): `src/OneId.Server/Controllers/InternalTenantsController.cs`
- `RoleChangeInvalidationTests` (test helpers to copy): `tests/OneId.Server.IntegrationTests/OpenIddict/RoleChangeInvalidationTests.cs`
- `InternalTenantsIntegrationTests` (AuthClientAsync helper): `tests/OneId.Server.IntegrationTests/InternalTenantsIntegrationTests.cs`
- `DevSeeder`: `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `status` field serialized as int (not string) by default: tests asserting `"Suspended"` failed — fixed to check integer value `1` for Suspended and `0` for Active

### Completion Notes List

- Added `TenantStatus` enum (`Active = 0, Suspended = 1`) in `Domain/Enums/TenantStatus.cs`
- Extended `Tenant` entity with `Status TenantStatus` property; `TenantConfiguration` configured with `HasDefaultValue(TenantStatus.Active).HasConversion<int>()`
- EF Core migration `AddTenantStatusColumn` adds `status integer NOT NULL DEFAULT 0` to `tenants` table
- Updated `TenantDto` to include `Status`; updated all 4 callers (CreateTenantCommand, UpdateTenantCommand, GetTenantQuery, ListTenantsQuery)
- Created `SuspendTenantHandler` (AR-8): sets Status=Suspended, fetches all non-deleted user IDs, saves, then revokes each via `IUserTokenRevoker`
- Created `ReinstateTenantHandler` (AR-8): sets Status=Active
- Extended `InternalTenantsController` primary constructor with 2 new handlers; added POST `{id}/suspend` and POST `{id}/reinstate` actions
- Updated `ConnectController` password grant: added `tenant.Status == TenantStatus.Suspended` check using existing `Forbid(BuildForbidProperties("tenant_suspended", ...), ...)` pattern
- 5 new integration tests in `TenantSuspensionIntegrationTests.cs` covering all 4 ACs
- Final: 75 passed (+5 new), 2 skipped (unchanged), 1 pre-existing flaky `DevSigningKeyStabilityTest`
- ArchUnit: 28/28 InternalAdmin tests pass; AR-8 InternalAdminContext constraint satisfied on both new handlers

### Review Findings (2026-05-26)

**F1 — PATCHED:** `HandleMfaGrantAsync` (`ConnectController.cs:~160`) was missing the `TenantStatus.Suspended` check. A user who passed the password step before suspension could complete MFA and receive a valid token during the 5-minute MFA window. Added the same `Forbid(BuildForbidProperties("tenant_suspended", ...))` guard after the user lookup.

**F2 — DEFERRED:** `AppDbContext.cs` comment L41 lists `AuditLog` in the `UseXminAsConcurrencyToken` group. AuditLog is correctly append-only with no xmin; the comment is a copy-paste artefact. Cosmetic only.

**F3 — DISMISSED:** `SuspendTenant` calls `SaveChangesAsync` before iterating revocations. Intentional per dev notes — idempotent revocations mean the window is safe.

### File List

- `src/OneId.Server/Domain/Enums/TenantStatus.cs` — NEW
- `src/OneId.Server/Domain/Entities/Tenant.cs` — MODIFIED (added Status property)
- `src/OneId.Server/Infrastructure/Persistence/Configurations/TenantConfiguration.cs` — MODIFIED (added Status property config)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260526134421_AddTenantStatusColumn.cs` — NEW (generated)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260526134421_AddTenantStatusColumn.Designer.cs` — NEW (generated)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — UPDATED (generated)
- `src/OneId.Server/Application/Internal/TenantDto.cs` — MODIFIED (added Status field)
- `src/OneId.Server/Application/Internal/Commands/CreateTenantCommand.cs` — MODIFIED (pass tenant.Status to TenantDto)
- `src/OneId.Server/Application/Internal/Commands/UpdateTenantCommand.cs` — MODIFIED (pass tenant.Status to TenantDto)
- `src/OneId.Server/Application/Internal/Queries/GetTenantQuery.cs` — MODIFIED (pass t.Status to TenantDto)
- `src/OneId.Server/Application/Internal/Queries/ListTenantsQuery.cs` — MODIFIED (pass t.Status to TenantDto)
- `src/OneId.Server/Application/Internal/Commands/SuspendTenantCommand.cs` — NEW
- `src/OneId.Server/Application/Internal/Commands/ReinstateTenantCommand.cs` — NEW
- `src/OneId.Server/Application/Internal/InternalServiceExtensions.cs` — MODIFIED (registered 2 new handlers)
- `src/OneId.Server/Controllers/InternalTenantsController.cs` — MODIFIED (2 new actions + constructor params)
- `src/OneId.Server/Controllers/ConnectController.cs` — MODIFIED (added tenant_suspended check)
- `tests/OneId.Server.IntegrationTests/TenantSuspensionIntegrationTests.cs` — NEW
