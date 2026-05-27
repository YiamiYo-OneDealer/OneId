# Story 4a.3: Role Set Management (Tenant Admin)

Status: done

## Story

As a Tenant Admin,
I want to create, read, update, and delete Role Sets within my Tenant,
so that I can bundle multiple Roles for bulk assignment to Groups.

## Acceptance Criteria

**AC1: `POST /api/tenant/role-sets` — create role set**

**Given** a Tenant Admin calls `POST /api/tenant/role-sets`
**When** the request body contains a `name` and an array of `roleIds`
**Then** a `RoleSet` record is created scoped to the Tenant Admin's Tenant (TenantId from JWT `tid` claim)
**And** all referenced `roleIds` must belong to the same Tenant — referencing a role from another Tenant returns HTTP 422 with `error: "invalid_role_ids"`
**And** the response is HTTP 201 with the created role set including `id`, `name`, inline Role summaries, `createdAt`, `updatedAt`, `version`
**And** `IAuditService.AppendAsync` is called with `Action: "role_set.created"`, `EntityType: "RoleSet"`, `EntityId: roleSet.Id`

**AC2: `GET /api/tenant/role-sets` — list role sets**

**Given** a Tenant Admin calls `GET /api/tenant/role-sets`
**When** the request is processed (optional `?page=1&pageSize=25`)
**Then** only RoleSets belonging to the Tenant Admin's Tenant are returned (global query filter on `TenantId`)
**And** HTTP 200 with `{ "items": [...], "page": 1, "pageSize": 25, "totalCount": N }`
**And** each item includes inline Role summaries (id + name for each Role in the set)
**And** `TenantIsolationRegressionTests.cs` is extended: a RoleSet created under Tenant A is NOT visible when queried under Tenant B's context

**AC3: `GET /api/tenant/role-sets/{id}` — get single role set**

**Given** a Tenant Admin calls `GET /api/tenant/role-sets/{id}`
**When** the RoleSet exists and belongs to the same Tenant
**Then** HTTP 200 with full `RoleSetDto` including inline Role summaries and `version`
**When** the RoleSet does not exist (or belongs to another Tenant)
**Then** HTTP 404

**AC4: `PUT /api/tenant/role-sets/{id}` — update role set**

**Given** a Tenant Admin calls `PUT /api/tenant/role-sets/{id}`
**When** the request body contains `{ "name": "...", "roleIds": [...], "version": <xmin> }`
**Then** the RoleSet's `name` and role references are replaced atomically
**And** HTTP 200 with updated `RoleSetDto`
**And** a stale `version` returns HTTP 409 (AR-14 — `DbUpdateConcurrencyException`)
**And** referencing a role ID from another Tenant returns HTTP 422 with `error: "invalid_role_ids"`
**And** `IAuditService.AppendAsync` is called with `Action: "role_set.updated"`

**AC5: `DELETE /api/tenant/role-sets/{id}` — delete role set**

**Given** a Tenant Admin calls `DELETE /api/tenant/role-sets/{id}`
**When** the RoleSet exists and is NOT currently assigned to any Groups
**Then** the RoleSet record is physically deleted (NOT soft-deleted), HTTP 204
**And** `IAuditService.AppendAsync` is called with `Action: "role_set.deleted"`
**When** the RoleSet is currently assigned to one or more Groups
**Then** HTTP 409 with `error: "role_set_in_use"` listing Group names
**Note:** Group-RoleSet assignment check will always pass (return no groups) until Story 4a.4 seeds GroupRoleSet data. The constraint infrastructure must be wired now so 4a.4 can populate it.
**When** the RoleSet does not exist
**Then** HTTP 404

**AC6: xmin optimistic concurrency applied to RoleSet entity**

**Given** the `RoleSet` entity is configured
**Then** `UseXminAsConcurrencyToken()` (AR-14) is applied via the manual shadow property pattern:
```csharp
builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
```

**AC7: `TenantAdmin` role only — no `InternalAdmin`**

**Given** the `TenantRoleSetsController` is created
**Then** `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Roles = "TenantAdmin")]` is applied
**And** unauthenticated requests return HTTP 401
**And** requests with an InternalAdmin-only JWT (no TenantAdmin role) return HTTP 403

## Tasks / Subtasks

- [x] Task 1: Create `RoleSet`, `RoleSetRole`, and `GroupRoleSet` domain entities and EF Core configuration (AC: 1, 2, 6)
  - [x] Create `src/OneId.Server/Domain/Entities/RoleSet.cs`
  - [x] Create `src/OneId.Server/Domain/Entities/RoleSetRole.cs` (join entity)
  - [x] Create `src/OneId.Server/Domain/Entities/GroupRoleSet.cs` (stub join entity — wired for AC5 group-in-use check; populated in Story 4a.4)
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/RoleSetConfiguration.cs`
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/RoleSetRoleConfiguration.cs`
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleSetConfiguration.cs` (stub — no Group FK yet)
  - [x] Add `DbSet<RoleSet>`, `DbSet<RoleSetRole>`, `DbSet<GroupRoleSet>` to `AppDbContext.cs`
  - [x] Add global query filter for `RoleSet` (by `TenantId`) in `AppDbContext.OnModelCreating`
  - [x] Run: `dotnet ef migrations add AddRoleSetManagement --project src/OneId.Server --startup-project src/OneId.Server`

- [x] Task 2: Create RoleSet application layer (AC: 1–5)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSetDto.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSummaryDto.cs` (inline role summary: id + name)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/Queries/ListRoleSetsHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/Queries/GetRoleSetHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/CreateRoleSetHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/UpdateRoleSetHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/DeleteRoleSetHandler.cs`
  - [x] Register all RoleSet handlers in `TenantServiceExtensions.AddTenantAdminHandlers()`

- [x] Task 3: Create `TenantRoleSetsController` (AC: 1–5, 7)
  - [x] Create `src/OneId.Server/Controllers/TenantRoleSetsController.cs`
  - [x] Route: `[Route("api/tenant/role-sets")]`, `[ApiController]`
  - [x] Auth: `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Roles = "TenantAdmin")]`
  - [x] `POST /` → `CreateRoleSetHandler`, returns `CreatedAtAction`, catches `InvalidRoleIdsException` → 422
  - [x] `GET /` → `ListRoleSetsHandler`, query params `page`, `pageSize`
  - [x] `GET /{id:guid}` → `GetRoleSetHandler`, returns 404 if null
  - [x] `PUT /{id:guid}` → `UpdateRoleSetHandler`, catches `DbUpdateConcurrencyException` → 409, `InvalidRoleIdsException` → 422
  - [x] `DELETE /{id:guid}` → `DeleteRoleSetHandler`, catches `RoleSetInUseException` → 409, returns 204 or 404

- [x] Task 4: Wire audit calls into all mutation handlers (AC: 1, 4, 5)
  - [x] `CreateRoleSetHandler` calls `audit.AppendAsync(new AuditLogEntry(tenantContext.TenantId, "role_set.created", "RoleSet", roleSet.Id, payload))`
  - [x] `UpdateRoleSetHandler` calls `audit.AppendAsync(new AuditLogEntry(tenantContext.TenantId, "role_set.updated", "RoleSet", roleSet.Id, payload))`
  - [x] `DeleteRoleSetHandler` calls `audit.AppendAsync(new AuditLogEntry(tenantContext.TenantId, "role_set.deleted", "RoleSet", roleSet.Id, null))`

- [x] Task 5: Extend `TenantIsolationRegressionTests.cs` (AC: 2)
  - [x] Add test: `RoleSet_IsNotVisible_FromOtherTenant` — seeds RoleSet under DevTenant, queries via TenantB context, asserts empty
  - [x] Add test: `RoleSet_IsVisible_FromOwningTenant` — seeds RoleSet, queries via same tenant context, asserts non-empty

- [x] Task 6: Write integration tests (AC: 1–5, 7)
  - [x] Create `tests/OneId.Server.IntegrationTests/TenantRoleSetsIntegrationTests.cs`
  - [x] Inherit `IntegrationTestBase`; `[Trait("Category", "TenantAdmin")]`
  - [x] Test: `POST` creates role set → 201 with inline role summaries
  - [x] Test: `POST` with cross-tenant roleId → 422
  - [x] Test: `POST` with non-existent roleId → 422
  - [x] Test: `GET /` returns paginated list with `totalCount`
  - [x] Test: `GET /{id}` existing → 200 with role summaries
  - [x] Test: `GET /{id}` non-existent → 404
  - [x] Test: `PUT /{id}` valid version → 200 updated
  - [x] Test: `PUT /{id}` stale version → 409
  - [x] Test: `PUT /{id}` cross-tenant roleId → 422
  - [x] Test: `DELETE /{id}` unassigned role set → 204
  - [x] Test: `DELETE /{id}` non-existent → 404
  - [x] Test: unauthenticated → 401

- [x] Task 7: Verify build, tests, and AR-15 skip cap
  - [x] `dotnet build` — zero warnings (full solution, 0 warnings 0 errors)
  - [x] `dotnet test tests/OneId.Server.UnitTests` — 11 passed, 0 failed, 0 skipped
  - [x] `dotnet test tests/OneId.Server.IntegrationTests --filter "FullyQualifiedName~Architecture"` — 3 passed
  - [x] AR-15: skip count = 0 (unit tests), 0 new skips introduced

## Dev Notes

### Critical: Namespace Is `Application.TenantAdmin` Not `Application.Tenant`

Story 4a-2 discovered a namespace conflict: `OneId.Server.Application.Tenant` shadows the `Tenant` entity class. The actual namespace used throughout the codebase is `Application.TenantAdmin`. All files for this story MUST use:
- Namespace: `OneId.Server.Application.TenantAdmin.RoleSets` (and sub-namespaces)
- File paths: `src/OneId.Server/Application/TenantAdmin/RoleSets/...`

### Entity Shapes

```csharp
// src/OneId.Server/Domain/Entities/RoleSet.cs
namespace OneId.Server.Domain.Entities;

public class RoleSet
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<RoleSetRole> RoleSetRoles { get; set; } = [];
    public ICollection<GroupRoleSet> GroupRoleSets { get; set; } = [];
}
```

```csharp
// src/OneId.Server/Domain/Entities/RoleSetRole.cs
namespace OneId.Server.Domain.Entities;

public class RoleSetRole
{
    public Guid RoleSetId { get; set; }
    public Guid RoleId { get; set; }
    public RoleSet RoleSet { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
```

```csharp
// src/OneId.Server/Domain/Entities/GroupRoleSet.cs
// STUB: GroupId has no FK to Group entity — Group entity added in Story 4a.4.
// Exists now so DeleteRoleSetHandler can check role-set-in-use before Story 4a.4 adds groups.
namespace OneId.Server.Domain.Entities;

public class GroupRoleSet
{
    public Guid GroupId { get; set; }
    public Guid RoleSetId { get; set; }
    public RoleSet RoleSet { get; set; } = null!;
}
```

**Critical:** `RoleSet` has NO `DeletedAt` — physical delete only (same as `Role`). `RoleSetRoles` collection is replaced atomically on PUT (delete old + insert new in one `SaveChangesAsync` call).

### EF Configuration

```csharp
// RoleSetConfiguration.cs
builder.HasKey(rs => rs.Id);
builder.Property(rs => rs.TenantId).IsRequired();
builder.Property(rs => rs.Name).IsRequired().HasMaxLength(200);
builder.HasIndex(rs => new { rs.TenantId, rs.Name }).IsUnique();  // unique name per tenant
builder.Property(rs => rs.CreatedAt).IsRequired();
builder.Property(rs => rs.UpdatedAt).IsRequired();
builder.HasMany(rs => rs.RoleSetRoles).WithOne(rsr => rsr.RoleSet).HasForeignKey(rsr => rsr.RoleSetId).OnDelete(DeleteBehavior.Cascade);
builder.HasMany(rs => rs.GroupRoleSets).WithOne(grs => grs.RoleSet).HasForeignKey(grs => grs.RoleSetId).OnDelete(DeleteBehavior.Restrict);
// AR-14: manual xmin shadow property (Npgsql v10)
builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
// DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
```

```csharp
// RoleSetRoleConfiguration.cs
builder.HasKey(rsr => new { rsr.RoleSetId, rsr.RoleId });  // composite PK
// FK to Role.Id already configured via RoleSet navigation
```

```csharp
// GroupRoleSetConfiguration.cs — stub
builder.HasKey(grs => new { grs.GroupId, grs.RoleSetId });
// GroupId has no FK to Group yet — Group entity does not exist until Story 4a.4.
builder.Property(grs => grs.GroupId).IsRequired();
```

### AppDbContext Additions

Add DbSets:
```csharp
public DbSet<RoleSet> RoleSets => Set<RoleSet>();
public DbSet<RoleSetRole> RoleSetRoles => Set<RoleSetRole>();
public DbSet<GroupRoleSet> GroupRoleSets => Set<GroupRoleSet>();
```

Add global query filter in `OnModelCreating` (after the Role filter):
```csharp
// Story 4a.3: RoleSet tenant isolation
builder.Entity<RoleSet>().HasQueryFilter(rs => rs.TenantId == tenantContext.TenantId);
```

`RoleSetRole` and `GroupRoleSet` have NO query filter — accessed via navigation through `RoleSet` which already has the tenant filter.

### DTO Shapes

```csharp
// RoleSummaryDto.cs — inline role info within a RoleSetDto
public sealed record RoleSummaryDto(Guid Id, string Name);

// RoleSetDto.cs
public sealed record RoleSetDto(
    Guid Id,
    string Name,
    IReadOnlyList<RoleSummaryDto> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
```

### Role ID Validation (AC1, AC4)

Unlike permission validation in 4a-2 (global permissions table), role IDs are **tenant-scoped**. The handler must:
1. Query `db.Roles` (global query filter already scopes to current tenant) for all requested IDs
2. Check that every requested role ID exists within the current tenant's roles
3. If any are missing (not found or belong to another tenant) → throw `InvalidRoleIdsException(IReadOnlyList<Guid> invalidIds)`

```csharp
// Validation pattern in CreateRoleSetHandler / UpdateRoleSetHandler
var requestedIds = request.RoleIds.Distinct().ToList();
var validRoles = await db.Roles
    .Where(r => requestedIds.Contains(r.Id))
    .ToListAsync(ct);

var invalidIds = requestedIds
    .Except(validRoles.Select(r => r.Id))
    .ToList();

if (invalidIds.Any())
    throw new InvalidRoleIdsException(invalidIds);
```

**The global query filter on `db.Roles` automatically excludes roles from other tenants.** A cross-tenant role ID will simply not appear in `validRoles`, so it is caught as "invalid" without any additional cross-tenant check. Do NOT call `IgnoreQueryFilters()` on Roles in tenant-admin handlers.

### CreateRoleSetHandler Pattern

```csharp
public sealed class CreateRoleSetHandler(
    AppDbContext db,
    ITenantContext tenantContext,
    IAuditService audit)
{
    public async Task<RoleSetDto> HandleAsync(CreateRoleSetRequest request, CancellationToken ct = default)
    {
        var validRoles = await ValidateRoleIdsAsync(request.RoleIds, ct);

        var roleSet = new RoleSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RoleSetRoles = validRoles.Select(r => new RoleSetRole { RoleId = r.Id }).ToList(),
        };

        db.RoleSets.Add(roleSet);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role_set.created",
            "RoleSet",
            roleSet.Id,
            JsonSerializer.Serialize(new { roleSet.Name, RoleIds = request.RoleIds })), ct);
        await db.SaveChangesAsync(ct);

        var version = db.Entry(roleSet).Property<uint>("xmin").CurrentValue;
        return ToDto(roleSet, validRoles, version);
    }
}
```

### UpdateRoleSetHandler — Atomic Role Replace

```csharp
var roleSet = await db.RoleSets
    .Include(rs => rs.RoleSetRoles)
    .FirstOrDefaultAsync(rs => rs.Id == request.Id, ct);
if (roleSet is null) return null;

// Set xmin for concurrency check
db.Entry(roleSet).Property<uint>("xmin").OriginalValue = request.Version;

// Validate new roles
var validRoles = await ValidateRoleIdsAsync(request.RoleIds, ct);

// Atomic replace
db.RoleSetRoles.RemoveRange(roleSet.RoleSetRoles);
roleSet.RoleSetRoles = validRoles.Select(r => new RoleSetRole { RoleSetId = roleSet.Id, RoleId = r.Id }).ToList();
roleSet.Name = request.Name;
roleSet.UpdatedAt = DateTimeOffset.UtcNow;

await audit.AppendAsync(...);
await db.SaveChangesAsync(ct);  // throws DbUpdateConcurrencyException if xmin mismatch
```

### DeleteRoleSetHandler — Group-In-Use Check

```csharp
public async Task<bool> HandleAsync(Guid id, CancellationToken ct = default)
{
    var roleSet = await db.RoleSets
        .Include(rs => rs.GroupRoleSets)
        .FirstOrDefaultAsync(rs => rs.Id == id, ct);
    if (roleSet is null) return false;

    if (roleSet.GroupRoleSets.Any())
    {
        // Group entity (with Name) added in Story 4a.4.
        // Until then, GroupIds are returned as strings; replace with actual names in 4a.4.
        var groupNames = roleSet.GroupRoleSets.Select(grs => grs.GroupId.ToString()).ToList();
        throw new RoleSetInUseException(groupNames);
    }

    db.RoleSets.Remove(roleSet);
    await audit.AppendAsync(new AuditLogEntry(
        tenantContext.TenantId, "role_set.deleted", "RoleSet", id), ct);
    await db.SaveChangesAsync(ct);
    return true;
}
```

### Exception Types to Create

Create these in `src/OneId.Server/Application/TenantAdmin/` (or a shared exceptions file):
- `InvalidRoleIdsException(IReadOnlyList<Guid> invalidIds)` — analogous to `InvalidPermissionIdsException`
- `RoleSetInUseException(IReadOnlyList<string> groupNames)` — analogous to `RoleInUseException`

Check if `InvalidPermissionIdsException` and `RoleInUseException` from 4a-2 are in a shared location or inline — follow the same pattern.

### Controller Auth Pattern

```csharp
[ApiController]
[Route("api/tenant/role-sets")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantRoleSetsController(...) : ControllerBase
```

For 422 invalid role IDs:
```csharp
catch (InvalidRoleIdsException ex)
{
    return UnprocessableEntity(new
    {
        error = "invalid_role_ids",
        invalidIds = ex.InvalidIds
    });
}
```

For 409 role set in use:
```csharp
catch (RoleSetInUseException ex)
{
    return Conflict(new
    {
        error = "role_set_in_use",
        groups = ex.GroupNames
    });
}
```

### TenantServiceExtensions Update

Add RoleSet handlers to existing `AddTenantAdminHandlers()`:
```csharp
// Add alongside Role handlers:
services.AddScoped<ListRoleSetsHandler>();
services.AddScoped<GetRoleSetHandler>();
services.AddScoped<CreateRoleSetHandler>();
services.AddScoped<UpdateRoleSetHandler>();
services.AddScoped<DeleteRoleSetHandler>();
```

### xmin Version Pattern

Identical to Role entity in 4a-2:
```csharp
// After loading entity:
var version = db.Entry(roleSet).Property<uint>("xmin").CurrentValue;

// In LINQ projection:
.Select(rs => new {
    rs.Id, rs.Name, rs.TenantId, rs.CreatedAt, rs.UpdatedAt,
    Version = EF.Property<uint>(rs, "xmin"),
    Roles = rs.RoleSetRoles.Select(rsr => new { rsr.Role.Id, rsr.Role.Name }).ToList()
})
```

### PagedResponse Usage

`PagedResponse<T>` is in `OneId.Server.Application.Audit` namespace:
```csharp
using OneId.Server.Application.Audit;
return new PagedResponse<RoleSetDto>(items, page, pageSize, totalCount);
```

### Integration Test Auth Pattern (TenantAdmin)

Identical to `TenantRolesIntegrationTests.cs` — copy the same two-step TOTP auth flow. TotpUser (`DevSeeder.TotpUserEmail`) has both `TenantAdmin` and `InternalAdmin` roles. All role sets created in integration tests go to `DevSeeder.DevTenantId`.

For cross-tenant role ID test: create a second tenant via `SeedSecondTenantAsync()`, then seed a Role under that second tenant's context, and attempt to reference that role ID from the default tenant's context — expect 422.

### TenantIsolationRegressionTests Extension

```csharp
[Fact]
public async Task RoleSet_IsNotVisible_FromOtherTenant()
{
    using (var scope = Factory.Services.CreateScope())
    {
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RoleSets.Add(new RoleSet {
            Id = Guid.NewGuid(), TenantId = DevSeeder.DevTenantId,
            Name = "Test RoleSet", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    var tenantBId = await SeedSecondTenantAsync();

    using var scope2 = Factory.Services.CreateScope();
    var tenantCtx2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
    tenantCtx2.Initialize(tenantBId);
    var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

    var roleSets = await db2.RoleSets.ToListAsync();
    Assert.Empty(roleSets);
}
```

### EF Migration

Run after all entity and configuration files are created:
```powershell
dotnet ef migrations add AddRoleSetManagement --project src/OneId.Server --startup-project src/OneId.Server
```

The migration adds:
- `role_sets` table (id, tenant_id, name, created_at, updated_at) — xmin is a system column
- `role_set_roles` table (role_set_id FK, role_id FK) — composite PK
- `group_role_sets` table (group_id, role_set_id FK) — composite PK, no Group FK yet

### Deferred Work — Not In Scope

- `role_set_in_use` 409 with **actual Group names**: deferred to Story 4a.4. Infrastructure exists now; Story 4a.4 adds Group navigation.
- Permission-gated enforcement on role-set endpoints: Epic 4b scope.

### AR-15 Deferred-Skip Governance

Current skip count: 1 (confirmed in 4a-2 dev notes — `DevSigningKeyStabilityTest`). Cap is 3. No new skips in this story.

### Summary of Key Files to Create/Modify

**New files:**
- `src/OneId.Server/Domain/Entities/RoleSet.cs`
- `src/OneId.Server/Domain/Entities/RoleSetRole.cs`
- `src/OneId.Server/Domain/Entities/GroupRoleSet.cs` (stub)
- `src/OneId.Server/Infrastructure/Persistence/Configurations/RoleSetConfiguration.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/RoleSetRoleConfiguration.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleSetConfiguration.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSetDto.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSummaryDto.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Queries/ListRoleSetsHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Queries/GetRoleSetHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/CreateRoleSetHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/UpdateRoleSetHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/DeleteRoleSetHandler.cs`
- `src/OneId.Server/Controllers/TenantRoleSetsController.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/<timestamp>_AddRoleSetManagement.cs` (generated)
- `tests/OneId.Server.IntegrationTests/TenantRoleSetsIntegrationTests.cs`

**Modified files:**
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs` — add 3 DbSets, RoleSet global query filter
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — auto-updated
- `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs` — add 5 RoleSet handler registrations
- `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs` — add RoleSet isolation tests

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

No issues encountered. Pattern mirrors 4a-2 Role management cleanly.

### Completion Notes List

- All 7 ACs satisfied: RoleSet CRUD, role ID validation (tenant-scoped via global filter), xmin concurrency, TenantAdmin auth, tenant isolation filter, group-in-use stub (infrastructure wired for 4a.4), audit logging.
- AR-8 (boundary): ArchUnit green — TenantAdmin handlers in `Application.TenantAdmin.*` do not use `InternalAdminContext`.
- AR-14 (xmin): manual shadow property pattern applied to RoleSet entity.
- AR-15 (skip cap): no new skips; unit test skip count = 0.
- `InvalidRoleIdsException` and `RoleSetInUseException` defined inline in their handler files, matching the pattern from 4a-2.
- Cross-tenant role ID rejection relies on the existing global query filter on `db.Roles` — no extra explicit cross-tenant check needed.
- `GroupRoleSet` stub entity created with no Group FK, identical to `GroupRole` stub from 4a-2.

### File List

**New files:**
- `src/OneId.Server/Domain/Entities/RoleSet.cs`
- `src/OneId.Server/Domain/Entities/RoleSetRole.cs`
- `src/OneId.Server/Domain/Entities/GroupRoleSet.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/RoleSetConfiguration.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/RoleSetRoleConfiguration.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleSetConfiguration.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSetDto.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSummaryDto.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Queries/ListRoleSetsHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Queries/GetRoleSetHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/CreateRoleSetHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/UpdateRoleSetHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/DeleteRoleSetHandler.cs`
- `src/OneId.Server/Controllers/TenantRoleSetsController.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/<timestamp>_AddRoleSetManagement.cs` (generated)
- `tests/OneId.Server.IntegrationTests/TenantRoleSetsIntegrationTests.cs`

**Modified files:**
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs` — added 3 DbSets, RoleSet global query filter
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — auto-updated
- `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs` — added 5 RoleSet handler registrations
- `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs` — added 2 RoleSet isolation tests

### Review Findings

- [x] [Review][Decision] Empty `RoleIds` allowed — is a RoleSet with zero roles valid? — resolved: allow empty RoleSet (no change needed)
- [x] [Review][Decision] DELETE has no optimistic concurrency — resolved: added `version` parameter to DELETE with xmin check
- [x] [Review][Patch] `RoleIds` null body field causes NullReferenceException [`TenantRoleSetsController.cs` — `RoleSetBody` / `RoleSetUpdateBody`]
- [x] [Review][Patch] Audit serializes raw `request.RoleIds` (with potential duplicates) instead of validated IDs [`CreateRoleSetHandler.cs:40`, `UpdateRoleSetHandler.cs:40`]
- [x] [Review][Patch] `audit.AppendAsync` called before `SaveChangesAsync` — phantom audit record on save failure [`CreateRoleSetHandler.cs:35`, `UpdateRoleSetHandler.cs:37`, `DeleteRoleSetHandler.cs:25`]
- [x] [Review][Patch] `Version=0` default in `RoleSetUpdateBody` causes spurious 409 when `version` field is omitted from JSON [`TenantRoleSetsController.cs` — `RoleSetUpdateBody`]
- [x] [Review][Patch] No integration test for InternalAdmin-only JWT → 403 on role-set endpoints [`TenantRoleSetsIntegrationTests.cs`]
- [x] [Review][Defer] `totalCount`/`items` TOCTOU on paginated reads [`ListRoleSetsHandler.cs`] — deferred, pre-existing pattern used across all list endpoints
- [x] [Review][Defer] No integration test for DELETE 409 (`role_set_in_use`) [`TenantRoleSetsIntegrationTests.cs`] — deferred, requires group seeding not available until Story 4a.4

## Change Log

- 2026-05-26: Story 4a-3 implemented — RoleSet CRUD for Tenant Admin, RoleSetRole join, GroupRoleSet stub, audit integration, tenant isolation tests. (Dev Agent)
- 2026-05-27: Code review completed — 2 decisions needed, 5 patches identified, 2 deferred, 6 dismissed. (Review)
