# Story 3.4: Tenant Admin Designation (Internal Admin)

Status: done

## Story

As an Internal Admin,
I want to designate one or more users within a Tenant as Tenant Admins,
So that those users can manage their Tenant's configuration without Internal Admin involvement.

## Acceptance Criteria

**AC1: POST designates user as Tenant Admin**

**Given** an Internal Admin calls `POST /api/internal/tenants/{tenantId}/admins/{userId}`
**When** the request is processed
**Then** the target user is granted the Tenant Admin role within that Tenant
**And** the response is HTTP 200 with the updated user representation showing `isTenantAdmin: true`
**And** the user must already exist within the specified Tenant
**And** attempting to designate a user from a different Tenant returns HTTP 404 (tenant isolation enforced, not 403 — no cross-tenant existence disclosure)

**AC2: DELETE removes Tenant Admin role**

**Given** an Internal Admin calls `DELETE /api/internal/tenants/{tenantId}/admins/{userId}`
**When** the request is processed
**Then** the Tenant Admin role is removed from the user
**And** removing the last Tenant Admin from a Tenant returns HTTP 409 with `error: "last_tenant_admin"` — a Tenant must retain at least one Admin

**AC3: Tenant Admin role appears in JWT**

**Given** a user is designated as Tenant Admin
**When** they authenticate and receive a JWT
**Then** their `roles` claim includes `"TenantAdmin"`
**And** a `TenantAdminDesignationIntegrationTest` verifies the role appears in the JWT after designation and is absent after removal

## Tasks / Subtasks

- [x] Task 1: Extend User entity and EF Core configuration (AC: 1–3)
  - [x] Add `public bool IsTenantAdmin { get; set; }` to `src/OneId.Server/Domain/Entities/User.cs`
  - [x] Add to `UserConfiguration.cs`: `builder.Property(u => u.IsTenantAdmin).IsRequired().HasDefaultValue(false);`
  - [x] Run `dotnet ef migrations add AddIsTenantAdminToUser --project src/OneId.Server --startup-project src/OneId.Server`

- [x] Task 2: Create `UserDto` record (AC: 1, 2)
  - [x] Create `src/OneId.Server/Application/Internal/UserDto.cs` — new file alongside `TenantDto.cs`
  - [x] Shape: `public sealed record UserDto(Guid Id, string Email, bool IsTenantAdmin, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint Version);`

- [x] Task 3: Create command handlers (AC: 1, 2)
  - [x] Create `src/OneId.Server/Application/Internal/Commands/DesignateTenantAdminCommand.cs`
  - [x] Create `src/OneId.Server/Application/Internal/Commands/RemoveTenantAdminCommand.cs` + `LastTenantAdminException`
  - [x] Both handlers inject `InternalAdminContext` and `AppDbContext` (AR-8 pattern — see Dev Notes)

- [x] Task 4: Register new handlers (AC: 1, 2)
  - [x] Add `services.AddScoped<DesignateTenantAdminHandler>();` to `InternalServiceExtensions.AddInternalAdminHandlers()`
  - [x] Add `services.AddScoped<RemoveTenantAdminHandler>();`

- [x] Task 5: Add endpoints to `InternalTenantsController` (AC: 1, 2)
  - [x] Add `[HttpPost("{tenantId:guid}/admins/{userId:guid}")]` → `DesignateAdmin` action
  - [x] Add `[HttpDelete("{tenantId:guid}/admins/{userId:guid}")]` → `RemoveAdmin` action

- [x] Task 6: Implement `RoleClaimsEnricher` (AC: 3)
  - [x] Modify `src/OneId.Server/Application/TokenPipeline/RoleClaimsEnricher.cs`
  - [x] Query `db.Users` with `IgnoreQueryFilters()` by `context.UserId`; if `IsTenantAdmin == true`, add `"TenantAdmin"` role claim
  - [x] Inject `AppDbContext` into `RoleClaimsEnricher` via constructor; registered as scoped (no change needed in `TokenPipelineExtensions`)

- [x] Task 7: Write integration tests (AC: all)
  - [x] Create `tests/OneId.Server.IntegrationTests/TenantAdminDesignationIntegrationTests.cs`
  - [x] Cover: designate returns 200 with `isTenantAdmin: true`, non-existent user returns 404, cross-tenant user returns 404, idempotent double-designation succeeds
  - [x] Cover: remove returns 204, last-admin returns 409 with `error: "last_tenant_admin"`, remove when other admins exist succeeds
  - [x] Cover (AC3): 3 separate tests — non-admin JWT has no role, designated-admin JWT contains "TenantAdmin", removed-admin JWT has no role

- [x] Task 8: Verify ArchUnit and skip cap
  - [x] `dotnet test --filter "Category=InternalAdmin"` — 23/23 pass
  - [x] Skip count remains at 2 (`TestTokenFactoryContractTests`, `PermissionCatalogSyncTests`) — zero new skips

### Review Findings

- [x] [Review][Patch] `RoleClaimsEnricher` soft-deleted user enrichment — `IgnoreQueryFilters()` without `u.DeletedAt == null` guard means a soft-deleted user with a live token still gets `TenantAdmin` claim [`src/OneId.Server/Application/TokenPipeline/RoleClaimsEnricher.cs`] — **fixed**
- [x] [Review][Defer] No role authorization on `InternalTenantsController` — deferred, pre-existing (intentional; Epic 4a adds `InternalAdmin` policy)
- [x] [Review][Defer] TOCTOU race in last-admin check (not atomic) — deferred, pre-existing (v1 acceptable; requires transaction infrastructure)
- [x] [Review][Defer] `DesignateTenantAdminHandler` allows designation on soft-deleted tenants — deferred, pre-existing (harmless; deactivated-tenant users cannot authenticate)

## Dev Notes

### AR-8: InternalAdminContext Pattern (MUST FOLLOW)

Every handler in `Application/Internal/` must declare `InternalAdminContext` as a constructor parameter.
This is the ArchUnit signal that the class is authorized for cross-tenant data access. However, a C#12
`-warnaserror` treats an unused discard `InternalAdminContext _` as CS9113.
Store it in a backing field (never use, but keep it to satisfy the boundary rule):

```csharp
public sealed class DesignateTenantAdminHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8
    // ...
}
```

### UserDto — Where to Place It

Place `UserDto.cs` in `src/OneId.Server/Application/Internal/` (same folder as `TenantDto.cs`).
Do NOT add it to a different namespace or create a new subfolder.

```csharp
namespace OneId.Server.Application.Internal;

public sealed record UserDto(
    Guid Id,
    string Email,
    bool IsTenantAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
```

### Reading xmin After Save

After `db.SaveChangesAsync()`, read the updated xmin via:
```csharp
var version = db.Entry(user).Property<uint>("xmin").CurrentValue;
```

### DesignateTenantAdminCommand.cs Pattern

```csharp
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed record DesignateTenantAdminRequest(Guid TenantId, Guid UserId);

public sealed class DesignateTenantAdminHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<UserDto?> HandleAsync(DesignateTenantAdminRequest request, CancellationToken ct = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.Id == request.UserId &&
                u.TenantId == request.TenantId &&
                u.DeletedAt == null, ct);

        if (user is null)
            return null;

        user.IsTenantAdmin = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var version = db.Entry(user).Property<uint>("xmin").CurrentValue;
        return new UserDto(user.Id, user.Email, user.IsTenantAdmin, user.CreatedAt, user.UpdatedAt, version);
    }
}
```

### RemoveTenantAdminCommand.cs Pattern

```csharp
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed class RemoveTenantAdminHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<UserDto?> HandleAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.TenantId == tenantId &&
                u.DeletedAt == null, ct);

        if (user is null)
            return null;

        if (user.IsTenantAdmin)
        {
            var otherAdmins = await db.Users
                .IgnoreQueryFilters()
                .CountAsync(u =>
                    u.TenantId == tenantId &&
                    u.IsTenantAdmin &&
                    u.Id != userId &&
                    u.DeletedAt == null, ct);

            if (otherAdmins == 0)
                throw new LastTenantAdminException(tenantId);
        }

        user.IsTenantAdmin = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var version = db.Entry(user).Property<uint>("xmin").CurrentValue;
        return new UserDto(user.Id, user.Email, user.IsTenantAdmin, user.CreatedAt, user.UpdatedAt, version);
    }
}

public sealed class LastTenantAdminException(Guid tenantId)
    : Exception($"Cannot remove the last Tenant Admin from tenant {tenantId}.")
{
    public Guid TenantId { get; } = tenantId;
}
```

### Controller Endpoints to Add in InternalTenantsController.cs

Inject the two new handlers via primary constructor (same pattern as existing handlers).
Add after `Deactivate`:

```csharp
[HttpPost("{tenantId:guid}/admins/{userId:guid}")]
public async Task<IActionResult> DesignateAdmin(Guid tenantId, Guid userId, CancellationToken ct)
{
    var result = await designateAdminHandler.HandleAsync(new DesignateTenantAdminRequest(tenantId, userId), ct);
    return result is null ? NotFound() : Ok(result);
}

[HttpDelete("{tenantId:guid}/admins/{userId:guid}")]
public async Task<IActionResult> RemoveAdmin(Guid tenantId, Guid userId, CancellationToken ct)
{
    try
    {
        var result = await removeAdminHandler.HandleAsync(tenantId, userId, ct);
        return result is null ? NotFound() : NoContent();
    }
    catch (LastTenantAdminException)
    {
        return Conflict(new ProblemDetails
        {
            Title = "Conflict",
            Detail = "Cannot remove the last Tenant Admin from a tenant.",
            Status = StatusCodes.Status409Conflict,
            Extensions = { ["error"] = "last_tenant_admin" },
        });
    }
}
```

**IMPORTANT:** The controller is `class InternalTenantsController(... deactivateHandler) : ControllerBase`.
You must extend the primary constructor parameter list to include the two new handlers. Don't add a second constructor.

### RoleClaimsEnricher — What to Change

The current stub does nothing. Add `AppDbContext` injection and query `IsTenantAdmin`:

```csharp
using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public sealed class RoleClaimsEnricher(AppDbContext db) : ITokenClaimsEnricher
{
    public async Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == context.UserId, ct);

        if (user?.IsTenantAdmin == true)
        {
            identity.AddClaim(new Claim(
                ClaimTypes.Role,
                "TenantAdmin",
                ClaimValueTypes.String,
                "OpenIddict"));
        }

        // Epic 4a: add fine-grained role claims from UserRoles/Groups here.
    }
}
```

Check `Program.cs` to see how `RoleClaimsEnricher` is registered. It should be registered as `ITokenClaimsEnricher` — verify the DI registration includes this service. If it's registered as a scoped service directly (no change needed); if it's registered as a singleton, change to scoped because `AppDbContext` is scoped.

### Integration Test Auth Pattern

Use the same `AuthClientAsync()` helper from `InternalTenantsIntegrationTests.cs` (two-step password+MFA flow
via `DevSeeder.TotpUserEmail` / `JBSWY3DPEHPK3PXP`). This is required because `TestTokenFactory` HMAC tokens are
not accepted by the OpenIddict validation scheme.

For AC3 (JWT roles check): after designation, call `/connect/token` to issue a fresh token, decode the JWT payload
(Base64url-decode the middle segment), and assert `"TenantAdmin"` is in the `roles` claim.

```csharp
// Helper: decode JWT payload without validation (we trust the server issued it)
private static JsonElement DecodeJwtPayload(string jwt)
{
    var payload = jwt.Split('.')[1];
    var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
    var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
    return JsonSerializer.Deserialize<JsonElement>(bytes);
}
```

Assert roles presence:
```csharp
var roles = payload.GetProperty("role"); // OpenIddict uses "role" not "roles"
// roles may be a string (single) or array (multiple) — handle both:
var roleList = roles.ValueKind == JsonValueKind.Array
    ? roles.EnumerateArray().Select(r => r.GetString()).ToList()
    : new List<string?> { roles.GetString() };
Assert.Contains("TenantAdmin", roleList);
```

**Note:** OpenIddict uses the claim type `"role"` (not `"roles"`) in the serialized JWT.
The `ClaimTypes.Role` constant maps to the long URL form but OpenIddict remaps it to `"role"` in the JWT.

### Tenant Isolation Rule

Use `.IgnoreQueryFilters()` in all handlers — the global query filter for users requires `ITenantContext`
to be set, and InternalAdmin handlers run without a tenant context. This is the established pattern
from `ConnectController` and all existing `Application/Internal/` handlers.

For cross-tenant isolation enforcement (AC1): filter on BOTH `u.Id == request.UserId && u.TenantId == request.TenantId`.
A user from a different tenant will not match this filter and `FirstOrDefaultAsync` returns null → controller returns 404.
Never return 403 for a cross-tenant check — this would disclose whether the user ID exists in another tenant.

### No Repository Pattern

Do NOT introduce a repository or generic service wrapper. Inject `AppDbContext` directly, as established
in all prior stories. The "plain handler" pattern is the project's DI style (no MediatR).

### EF Core Migration

```
dotnet ef migrations add AddIsTenantAdminToUser --project src/OneId.Server --startup-project src/OneId.Server
```

The migration should add a single column: `is_tenant_admin boolean NOT NULL DEFAULT false`.
EF Core infers this from `HasDefaultValue(false)` in `UserConfiguration`. Verify the generated Up/Down before applying.

### AR-15 Deferred-Skip Governance

| Skip | Owner Story | Status after 3.4 |
|---|---|---|
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 2 / 3 cap** — zero new skips permitted in this story.

### File Structure

```
src/
  OneId.Server/
    Domain/Entities/
      User.cs                                        ← MODIFY (add IsTenantAdmin)
    Application/Internal/
      UserDto.cs                                     ← NEW
      InternalServiceExtensions.cs                  ← MODIFY (register 2 new handlers)
      Commands/
        DesignateTenantAdminCommand.cs               ← NEW
        RemoveTenantAdminCommand.cs                  ← NEW (includes LastTenantAdminException)
    Application/TokenPipeline/
      RoleClaimsEnricher.cs                         ← MODIFY (inject AppDbContext, add TenantAdmin claim)
    Controllers/
      InternalTenantsController.cs                  ← MODIFY (add 2 new action methods + constructor params)
    Infrastructure/Persistence/Configurations/
      UserConfiguration.cs                          ← MODIFY (add IsTenantAdmin property config)
    Infrastructure/Persistence/Migrations/
      <timestamp>_AddIsTenantAdminToUser.cs         ← NEW (generated by dotnet ef)
      <timestamp>_AddIsTenantAdminToUser.Designer.cs ← NEW (generated)
      AppDbContextModelSnapshot.cs                  ← UPDATED (generated)

tests/
  OneId.Server.IntegrationTests/
    TenantAdminDesignationIntegrationTests.cs        ← NEW
```

No frontend changes. No new npm packages.

### References

- AC source: `_bmad-output/planning-artifacts/epics.md` — Epic 3, Story 3.4 (lines 999–1022)
- Architecture rules: `_bmad-output/planning-artifacts/architecture.md` — "All Implementation Agents MUST", "InternalAdminContext", "AR-14 xmin"
- InternalAdminContext boundary enforcement: `tests/OneId.Server.IntegrationTests/Architecture/InternalBoundaryTests.cs`
- Existing User entity: `src/OneId.Server/Domain/Entities/User.cs`
- Existing UserConfiguration: `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Handler pattern reference: `src/OneId.Server/Application/Internal/Commands/CreateTenantCommand.cs`
- Controller pattern reference: `src/OneId.Server/Controllers/InternalTenantsController.cs`
- InternalServiceExtensions: `src/OneId.Server/Application/Internal/InternalServiceExtensions.cs`
- RoleClaimsEnricher (to modify): `src/OneId.Server/Application/TokenPipeline/RoleClaimsEnricher.cs`
- Integration test pattern: `tests/OneId.Server.IntegrationTests/InternalTenantsIntegrationTests.cs`
- DevSeeder constants: `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- CS0105 duplicate using: Added `using OneId.Server.Application.Internal;` to `InternalTenantsController.cs` which already imported that namespace → removed duplicate
- `ClaimTypes.Role` vs `OpenIddictConstants.Claims.Role`: `ClaimTypes.Role` is the long-form URL type; `RoleClaimsEnricher` must use `OpenIddictConstants.Claims.Role` (`"role"`) to match the `roleType` on the `ClaimsIdentity` created in `ConnectController`
- TOTP replay guard: `AuthClientAsync()` consumes TOTP time-step N for `DevSeeder.TotpUser`. Issuing a second token for the same user within 30 seconds is rejected by the replay guard (`TotpLastUsedTimeStep == timeStepMatched`). AC3 JWT tests use freshly seeded TOTP users (one per test) so each token call uses a fresh, unconsumed time step.

### Completion Notes List

- Added `IsTenantAdmin bool` property to `User` entity and `UserConfiguration`; EF Core migration `AddIsTenantAdminToUser` adds `is_tenant_admin boolean NOT NULL DEFAULT false` to the `users` table
- Created `UserDto` record in `Application/Internal/UserDto.cs`
- Created `DesignateTenantAdminHandler` and `RemoveTenantAdminHandler` (with `LastTenantAdminException`) following AR-8 InternalAdminContext pattern
- Extended `InternalTenantsController` primary constructor with two new handlers; added `DesignateAdmin` (POST) and `RemoveAdmin` (DELETE) actions
- Implemented `RoleClaimsEnricher` with `AppDbContext` injection; uses `OpenIddictConstants.Claims.Role` for correct JWT claim type; uses `IgnoreQueryFilters()` to bypass tenant filter
- 10 new integration tests in `TenantAdminDesignationIntegrationTests.cs` covering all 3 ACs; AC3 split into 3 focused tests (non-admin, designated, removed) to avoid TOTP replay conflicts
- Final: 70 passed (+10 new), 2 skipped (unchanged), 1 pre-existing flaky `DevSigningKeyStabilityTest`
- ArchUnit boundary tests: 3/3 pass; AR-8 InternalAdminContext constraint satisfied

### File List

- `src/OneId.Server/Domain/Entities/User.cs` — MODIFIED (added IsTenantAdmin property)
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` — MODIFIED (added IsTenantAdmin config)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260526120705_AddIsTenantAdminToUser.cs` — NEW (generated)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260526120705_AddIsTenantAdminToUser.Designer.cs` — NEW (generated)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — UPDATED (generated)
- `src/OneId.Server/Application/Internal/UserDto.cs` — NEW
- `src/OneId.Server/Application/Internal/Commands/DesignateTenantAdminCommand.cs` — NEW
- `src/OneId.Server/Application/Internal/Commands/RemoveTenantAdminCommand.cs` — NEW (includes LastTenantAdminException)
- `src/OneId.Server/Application/Internal/InternalServiceExtensions.cs` — MODIFIED (registered 2 new handlers)
- `src/OneId.Server/Application/TokenPipeline/RoleClaimsEnricher.cs` — MODIFIED (AppDbContext injection, TenantAdmin role claim)
- `src/OneId.Server/Controllers/InternalTenantsController.cs` — MODIFIED (2 new action methods + constructor params)
- `tests/OneId.Server.IntegrationTests/TenantAdminDesignationIntegrationTests.cs` — NEW
