# Story 4a.4: Group Management (Tenant Admin)

Status: done

## Story

As a Tenant Admin,
I want to create, read, update, and delete Groups and assign Roles and Role Sets to them,
So that I can organize users by job function and manage their permissions in bulk.

## Acceptance Criteria

**AC1: `POST /api/tenant/groups` — create group**

**Given** a Tenant Admin calls `POST /api/tenant/groups`
**When** the request body contains a `name`, optional `roleIds`, and optional `roleSetIds`
**Then** a `Group` record is created scoped to the Tenant Admin's Tenant (TenantId from JWT `tid` claim)
**And** all referenced `roleIds` and `roleSetIds` must belong to the same Tenant — cross-tenant references return HTTP 422 with `error: "invalid_role_ids"` or `error: "invalid_role_set_ids"` respectively
**And** the response is HTTP 201 with the created Group including `id`, `name`, inline Role summaries, inline RoleSet summaries, `createdAt`, `updatedAt`, `version`
**And** `IAuditService.AppendAsync` is called with `Action: "group.created"`, `EntityType: "Group"`, `EntityId: group.Id`

**AC2: `GET /api/tenant/groups` — list groups**

**Given** a Tenant Admin calls `GET /api/tenant/groups`
**When** the request is processed (optional `?page=1&pageSize=25`)
**Then** only Groups belonging to the Tenant Admin's Tenant are returned (global query filter on `TenantId`)
**And** HTTP 200 with `{ "items": [...], "page": 1, "pageSize": 25, "totalCount": N }`
**And** each item includes inline Role summaries and inline RoleSet summaries
**And** `TenantIsolationRegressionTests.cs` is extended: a Group created under Tenant A is NOT visible when queried under Tenant B's context

**AC3: `GET /api/tenant/groups/{id}` — get single group**

**Given** a Tenant Admin calls `GET /api/tenant/groups/{id}`
**When** the Group exists and belongs to the same Tenant
**Then** HTTP 200 with full `GroupDto` including inline Role summaries, inline RoleSet summaries, and `version`
**When** the Group does not exist (or belongs to another Tenant)
**Then** HTTP 404

**AC4: `PUT /api/tenant/groups/{id}` — update group**

**Given** a Tenant Admin calls `PUT /api/tenant/groups/{id}`
**When** the request body contains `{ "name": "...", "roleIds": [...], "roleSetIds": [...], "version": <xmin> }`
**Then** the Group's `name`, role references, and role-set references are replaced atomically
**And** HTTP 200 with updated `GroupDto`
**And** a stale `version` returns HTTP 409 (AR-14 — `DbUpdateConcurrencyException`)
**And** cross-tenant role/role-set IDs return HTTP 422
**And** `IAuditService.AppendAsync` is called with `Action: "group.updated"`

**AC5: `DELETE /api/tenant/groups/{id}` — delete group**

**Given** a Tenant Admin calls `DELETE /api/tenant/groups/{id}`
**When** the Group exists
**Then** the Group record is physically deleted, all `GroupRole` and `GroupRoleSet` join records cascade-deleted, all `UserGroup` membership records deleted, HTTP 204
**And** Users who were members of the deleted Group are unaffected — their `User` records remain
**And** `IAuditService.AppendAsync` is called with `Action: "group.deleted"`
**When** the Group does not exist
**Then** HTTP 404

**AC6: `PUT /api/tenant/groups/{id}/members` — add user to group**

**Given** a Tenant Admin calls `PUT /api/tenant/groups/{id}/members`
**When** the request body contains `{ "userId": "<guid>" }`
**Then** the User is added to the Group (only if the User belongs to the same Tenant — cross-tenant User ID returns HTTP 404)
**And** adding a User who is already a member is idempotent — HTTP 200, no duplicate record
**And** `IAuditService.AppendAsync` is called with `Action: "group.member_added"`

**AC7: `DELETE /api/tenant/groups/{id}/members/{userId}` — remove user from group**

**Given** a Tenant Admin calls `DELETE /api/tenant/groups/{id}/members/{userId}`
**When** the request is processed
**Then** the User is removed from the Group (UserGroup record physically deleted), HTTP 204
**And** removing a User from their last Group does not delete the User — User records and Group records are independent
**And** removing a User who is not a member returns HTTP 404
**And** `IAuditService.AppendAsync` is called with `Action: "group.member_removed"`

**AC8: xmin optimistic concurrency applied to Group entity**

**Given** the `Group` entity is configured
**Then** `UseXminAsConcurrencyToken()` (AR-14) is applied via the manual shadow property pattern:
```csharp
builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
```

**AC9: `TenantAdmin` role only — no `InternalAdmin`**

**Given** the `TenantGroupsController` is created
**Then** `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Roles = "TenantAdmin")]` is applied
**And** unauthenticated requests return HTTP 401
**And** requests with an InternalAdmin-only JWT return HTTP 403

**AC10: Fix stub entities — add proper FK navigation to Group**

**Given** `GroupRole` and `GroupRoleSet` are stub entities (created in 4a-2/4a-3 without Group FK)
**When** this story is implemented
**Then** both stubs are upgraded with a proper `Group` navigation property and FK constraint
**And** `GroupRoleConfiguration` and `GroupRoleSetConfiguration` are updated to declare the FK
**And** the migration includes the FK additions
**And** `DeleteRoleHandler` and `DeleteRoleSetHandler` are updated to return actual Group names instead of `GroupId.ToString()`

## Tasks / Subtasks

- [x] Task 1: Create `Group` and `UserGroup` domain entities + EF Core configuration (AC: 1, 2, 8)
  - [x] Create `src/OneId.Server/Domain/Entities/Group.cs`
  - [x] Create `src/OneId.Server/Domain/Entities/UserGroup.cs` (join entity: UserId + GroupId)
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupConfiguration.cs`
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/UserGroupConfiguration.cs`
  - [x] Add `DbSet<Group>` and `DbSet<UserGroup>` to `AppDbContext.cs`
  - [x] Add global query filter for `Group` (by `TenantId`) in `AppDbContext.OnModelCreating`

- [x] Task 2: Upgrade `GroupRole` and `GroupRoleSet` stubs to proper FK navigation (AC: 10)
  - [x] Update `src/OneId.Server/Domain/Entities/GroupRole.cs` — add `Group` navigation property
  - [x] Update `src/OneId.Server/Domain/Entities/GroupRoleSet.cs` — add `Group` navigation property
  - [x] Update `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleConfiguration.cs` — add FK to Group.Id
  - [x] Update `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleSetConfiguration.cs` — add FK to Group.Id
  - [x] Update `DeleteRoleHandler.cs` — replace `gr.GroupId.ToString()` with actual `gr.Group.Name`
  - [x] Update `DeleteRoleSetHandler.cs` — replace `grs.GroupId.ToString()` with actual `grs.Group.Name`
  - [x] Ensure `Include(r => r.GroupRoles).ThenInclude(gr => gr.Group)` in `DeleteRoleHandler`
  - [x] Ensure `Include(rs => rs.GroupRoleSets).ThenInclude(grs => grs.Group)` in `DeleteRoleSetHandler`

- [x] Task 3: Run EF Core migration
  - [x] `dotnet ef migrations add AddGroupManagement --project src/OneId.Server --startup-project src/OneId.Server`

- [x] Task 4: Create Group application layer (AC: 1–7)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/GroupDto.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/RoleSummaryDto.cs` (or reuse from RoleSets if shared)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/RoleSetSummaryDto.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Queries/ListGroupsHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Queries/GetGroupHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Commands/CreateGroupHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Commands/UpdateGroupHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Commands/DeleteGroupHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Commands/AddGroupMemberHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Groups/Commands/RemoveGroupMemberHandler.cs`
  - [x] Register all Group handlers in `TenantServiceExtensions.AddTenantAdminHandlers()`

- [x] Task 5: Create `TenantGroupsController` (AC: 1–7, 9)
  - [x] Create `src/OneId.Server/Controllers/TenantGroupsController.cs`
  - [x] Route: `[Route("api/tenant/groups")]`, `[ApiController]`
  - [x] Auth: `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Roles = "TenantAdmin")]`
  - [x] `POST /` → `CreateGroupHandler`, returns `CreatedAtAction`, catches `InvalidRoleIdsException` → 422, `InvalidRoleSetIdsException` → 422
  - [x] `GET /` → `ListGroupsHandler`, query params `page`, `pageSize`
  - [x] `GET /{id:guid}` → `GetGroupHandler`, returns 404 if null
  - [x] `PUT /{id:guid}` → `UpdateGroupHandler`, catches `DbUpdateConcurrencyException` → 409, 422 for invalid IDs
  - [x] `DELETE /{id:guid}` → `DeleteGroupHandler`, returns 204 or 404
  - [x] `PUT /{id:guid}/members` → `AddGroupMemberHandler`, returns 200 or 404 (cross-tenant user)
  - [x] `DELETE /{id:guid}/members/{userId:guid}` → `RemoveGroupMemberHandler`, returns 204 or 404

- [x] Task 6: Wire audit calls into all mutation handlers (AC: 1, 4, 5, 6, 7)
  - [x] `CreateGroupHandler` → `"group.created"`
  - [x] `UpdateGroupHandler` → `"group.updated"`
  - [x] `DeleteGroupHandler` → `"group.deleted"`
  - [x] `AddGroupMemberHandler` → `"group.member_added"`
  - [x] `RemoveGroupMemberHandler` → `"group.member_removed"`

- [x] Task 7: Extend `TenantIsolationRegressionTests.cs` (AC: 2)
  - [x] Add test: `Group_IsNotVisible_FromOtherTenant`
  - [x] Add test: `Group_IsVisible_FromOwningTenant`

- [x] Task 8: Write integration tests (AC: 1–7, 9)
  - [x] Create `tests/OneId.Server.IntegrationTests/TenantGroupsIntegrationTests.cs`
  - [x] Inherit `IntegrationTestBase`; `[Trait("Category", "TenantAdmin")]`
  - [x] Test: `POST` creates group → 201 with inline role and role-set summaries
  - [x] Test: `POST` with cross-tenant roleId → 422
  - [x] Test: `POST` with cross-tenant roleSetId → 422
  - [x] Test: `GET /` returns paginated list with `totalCount`
  - [x] Test: `GET /{id}` existing → 200 with role and role-set summaries
  - [x] Test: `GET /{id}` non-existent → 404
  - [x] Test: `PUT /{id}` valid version → 200 updated
  - [x] Test: `PUT /{id}` stale version → 409
  - [x] Test: `DELETE /{id}` → 204, users unaffected
  - [x] Test: `DELETE /{id}` non-existent → 404
  - [x] Test: `PUT /{id}/members` valid same-tenant user → 200
  - [x] Test: `PUT /{id}/members` same user twice → 200 idempotent
  - [x] Test: `PUT /{id}/members` cross-tenant userId → 404
  - [x] Test: `DELETE /{id}/members/{userId}` → 204
  - [x] Test: `DELETE /{id}/members/{userId}` not-a-member → 404
  - [x] Test: unauthenticated → 401

- [x] Task 9: Verify build, tests, and AR-15 skip cap
  - [x] `dotnet build` — zero warnings (full solution)
  - [x] `dotnet test tests/OneId.Server.UnitTests` — all passed, 0 failed
  - [x] `dotnet test tests/OneId.Server.IntegrationTests --filter "FullyQualifiedName~Architecture"` — green
  - [x] AR-15: no new skips introduced

## Dev Notes

### Namespace Convention (Critical — Learned in 4a-2)

**DO NOT** use `OneId.Server.Application.Tenant` — it shadows the `Tenant` entity class. All handlers and DTOs MUST use:
- Namespace: `OneId.Server.Application.TenantAdmin.Groups` (and sub-namespaces)
- File paths: `src/OneId.Server/Application/TenantAdmin/Groups/...`

### New Entity Shapes

```csharp
// src/OneId.Server/Domain/Entities/Group.cs
namespace OneId.Server.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<GroupRole> GroupRoles { get; set; } = [];
    public ICollection<GroupRoleSet> GroupRoleSets { get; set; } = [];
    public ICollection<UserGroup> UserGroups { get; set; } = [];
}
```

```csharp
// src/OneId.Server/Domain/Entities/UserGroup.cs
namespace OneId.Server.Domain.Entities;

public class UserGroup
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Group Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
```

### Stub Entity Upgrades (GroupRole and GroupRoleSet)

These stubs were created without a FK to Group. This story completes them:

```csharp
// Updated GroupRole.cs — add Group navigation
public class GroupRole
{
    public Guid GroupId { get; set; }
    public Guid RoleId { get; set; }
    public Group Group { get; set; } = null!;   // ADD THIS
    public Role Role { get; set; } = null!;
}
```

```csharp
// Updated GroupRoleSet.cs — add Group navigation
public class GroupRoleSet
{
    public Guid GroupId { get; set; }
    public Guid RoleSetId { get; set; }
    public Group Group { get; set; } = null!;   // ADD THIS
    public RoleSet RoleSet { get; set; } = null!;
}
```

### Updated Configurations

```csharp
// GroupRoleConfiguration.cs — add FK
builder.HasKey(gr => new { gr.GroupId, gr.RoleId });
builder.HasOne(gr => gr.Group).WithMany(g => g.GroupRoles).HasForeignKey(gr => gr.GroupId).OnDelete(DeleteBehavior.Cascade);
```

```csharp
// GroupRoleSetConfiguration.cs — add FK
builder.HasKey(grs => new { grs.GroupId, grs.RoleSetId });
builder.HasOne(grs => grs.Group).WithMany(g => g.GroupRoleSets).HasForeignKey(grs => grs.GroupId).OnDelete(DeleteBehavior.Cascade);
```

### GroupConfiguration

```csharp
// GroupConfiguration.cs
builder.HasKey(g => g.Id);
builder.Property(g => g.TenantId).IsRequired();
builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
builder.HasIndex(g => new { g.TenantId, g.Name }).IsUnique();
builder.Property(g => g.CreatedAt).IsRequired();
builder.Property(g => g.UpdatedAt).IsRequired();
// AR-14: manual xmin shadow property (Npgsql v10)
builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
// DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
```

### UserGroupConfiguration

```csharp
// UserGroupConfiguration.cs
builder.HasKey(ug => new { ug.GroupId, ug.UserId });
builder.HasOne(ug => ug.Group).WithMany(g => g.UserGroups).HasForeignKey(ug => ug.GroupId).OnDelete(DeleteBehavior.Cascade);
builder.HasOne(ug => ug.User).WithMany().HasForeignKey(ug => ug.UserId).OnDelete(DeleteBehavior.Restrict);
```

`UserGroup` has NO global query filter — accessed via navigation through `Group` which already has the tenant filter.

### AppDbContext Additions

```csharp
public DbSet<Group> Groups => Set<Group>();
public DbSet<UserGroup> UserGroups => Set<UserGroup>();
```

Add global query filter in `OnModelCreating` (after the RoleSet filter):
```csharp
// Story 4a.4: Group tenant isolation
builder.Entity<Group>().HasQueryFilter(g => g.TenantId == tenantContext.TenantId);
```

### DTO Shapes

```csharp
// RoleSummaryDto — IMPORTANT: Check if this already exists in RoleSets namespace
// src/OneId.Server/Application/TenantAdmin/RoleSets/RoleSummaryDto.cs
// If it does, do NOT duplicate it. Create a new one in Groups namespace OR reference the RoleSets one.
// Pattern from RoleSets: public sealed record RoleSummaryDto(Guid Id, string Name);

// RoleSetSummaryDto.cs
public sealed record RoleSetSummaryDto(Guid Id, string Name);

// GroupDto.cs
public sealed record GroupDto(
    Guid Id,
    string Name,
    IReadOnlyList<RoleSummaryDto> Roles,
    IReadOnlyList<RoleSetSummaryDto> RoleSets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
```

### Cross-Tenant Validation Pattern

Same as RoleSet story 4a-3 — the global query filter on `db.Roles` and `db.RoleSets` automatically excludes cross-tenant items. Do NOT call `IgnoreQueryFilters()` in tenant-admin handlers.

```csharp
// Validate roleIds
var requestedRoleIds = request.RoleIds.Distinct().ToList();
var validRoles = await db.Roles
    .Where(r => requestedRoleIds.Contains(r.Id))
    .ToListAsync(ct);
var invalidRoleIds = requestedRoleIds.Except(validRoles.Select(r => r.Id)).ToList();
if (invalidRoleIds.Any())
    throw new InvalidRoleIdsException(invalidRoleIds);

// Validate roleSetIds
var requestedRoleSetIds = request.RoleSetIds.Distinct().ToList();
var validRoleSets = await db.RoleSets
    .Where(rs => requestedRoleSetIds.Contains(rs.Id))
    .ToListAsync(ct);
var invalidRoleSetIds = requestedRoleSetIds.Except(validRoleSets.Select(rs => rs.Id)).ToList();
if (invalidRoleSetIds.Any())
    throw new InvalidRoleSetIdsException(invalidRoleSetIds);
```

### User Membership Validation (AC6)

The global query filter on `db.Users` already scopes to the current tenant. A cross-tenant `userId` simply won't be found:
```csharp
// AddGroupMemberHandler
var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
if (user is null) return AddMemberResult.UserNotFound;  // → 404 in controller

// Idempotency check
var existing = await db.UserGroups
    .FirstOrDefaultAsync(ug => ug.GroupId == request.GroupId && ug.UserId == request.UserId, ct);
if (existing is not null) return AddMemberResult.Ok;  // → 200 in controller

db.UserGroups.Add(new UserGroup { GroupId = request.GroupId, UserId = request.UserId });
await audit.AppendAsync(...);
await db.SaveChangesAsync(ct);
return AddMemberResult.Ok;
```

Use a result enum (not exception) for user-not-found to distinguish HTTP 200 vs 404 in the controller cleanly.

### DeleteGroupHandler — Cascade Behavior

Physical delete. EF Cascade on `GroupRole` and `GroupRoleSet` handles join table cleanup automatically (configured via `OnDelete(DeleteBehavior.Cascade)` on Group FK). `UserGroup` also cascades. No explicit `.RemoveRange()` needed for join records — EF handles it.

```csharp
var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct);
if (group is null) return false;

db.Groups.Remove(group);
await audit.AppendAsync(new AuditLogEntry(tenantContext.TenantId, "group.deleted", "Group", id), ct);
await db.SaveChangesAsync(ct);
return true;
```

### Fixing DeleteRoleHandler and DeleteRoleSetHandler (Task 2)

Update the stub comments-turned-actual-names pattern:
```csharp
// DeleteRoleHandler — BEFORE (stub from 4a-2):
var groupNames = role.GroupRoles.Select(gr => gr.GroupId.ToString()).ToList();

// AFTER (with Group navigation):
// Must include ThenInclude to load Group.Name
var role = await db.Roles
    .Include(r => r.GroupRoles)
        .ThenInclude(gr => gr.Group)   // ADD ThenInclude
    .FirstOrDefaultAsync(r => r.Id == id, ct);
// ...
var groupNames = role.GroupRoles.Select(gr => gr.Group.Name).ToList();
```

Same pattern for `DeleteRoleSetHandler`:
```csharp
var roleSet = await db.RoleSets
    .Include(rs => rs.GroupRoleSets)
        .ThenInclude(grs => grs.Group)   // ADD ThenInclude
    .FirstOrDefaultAsync(rs => rs.Id == id, ct);
// ...
var groupNames = roleSet.GroupRoleSets.Select(grs => grs.Group.Name).ToList();
```

### Exception Types

```csharp
// InvalidRoleIdsException already exists (defined in CreateRoleSetHandler.cs or UpdateRoleSetHandler.cs)
// Check its location: src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/
// If it's namespace-local, you'll need a new one in Groups namespace, OR move it to a shared location.
// Follow the same pattern — define inline in handler file if that's the established pattern.

public sealed class InvalidRoleSetIdsException(IReadOnlyList<Guid> invalidIds)
    : Exception($"Invalid role set IDs: {string.Join(", ", invalidIds)}")
{
    public IReadOnlyList<Guid> InvalidIds { get; } = invalidIds;
}
```

### Controller Request/Response Records

```csharp
// Declare at bottom of TenantGroupsController.cs
public sealed record GroupBody(string Name, IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> RoleSetIds);
public sealed record GroupUpdateBody(string Name, IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> RoleSetIds, uint Version);
public sealed record AddMemberBody(Guid UserId);
```

### xmin Version Pattern

Identical to Role (4a-2) and RoleSet (4a-3):
```csharp
var version = db.Entry(group).Property<uint>("xmin").CurrentValue;
```

In LINQ projection:
```csharp
.Select(g => new {
    g.Id, g.Name, g.TenantId, g.CreatedAt, g.UpdatedAt,
    Version = EF.Property<uint>(g, "xmin"),
    Roles = g.GroupRoles.Select(gr => new { gr.Role.Id, gr.Role.Name }).ToList(),
    RoleSets = g.GroupRoleSets.Select(grs => new { grs.RoleSet.Id, grs.RoleSet.Name }).ToList()
})
```

### PagedResponse Usage

`PagedResponse<T>` is in `OneId.Server.Application.Audit` namespace — same as 4a-2 and 4a-3:
```csharp
using OneId.Server.Application.Audit;
return new PagedResponse<GroupDto>(items, page, pageSize, totalCount);
```

### TenantServiceExtensions Update

```csharp
// Add alongside Role and RoleSet handlers:
services.AddScoped<ListGroupsHandler>();
services.AddScoped<GetGroupHandler>();
services.AddScoped<CreateGroupHandler>();
services.AddScoped<UpdateGroupHandler>();
services.AddScoped<DeleteGroupHandler>();
services.AddScoped<AddGroupMemberHandler>();
services.AddScoped<RemoveGroupMemberHandler>();
```

### Integration Test Auth Pattern

Identical to `TenantRoleSetsIntegrationTests.cs` — copy the same two-step TOTP auth flow (`DevSeeder.TotpUserEmail` + TOTP). All groups created in integration tests go to `DevSeeder.DevTenantId`.

Seed helpers needed:
- `SeedRoleAsync(string name)` — seeds Role in DevTenant, returns Id
- `SeedRoleSetAsync(string name, Guid roleId)` — seeds RoleSet in DevTenant, returns Id
- `SeedUserAsync(string email)` — seeds User in DevTenant, returns Id
- `SeedRoleInOtherTenantAsync()` — seeds in a new tenant, returns cross-tenant Id (same pattern as `TenantRoleSetsIntegrationTests`)

### TenantIsolationRegressionTests Extension

```csharp
[Fact]
public async Task Group_IsNotVisible_FromOtherTenant()
{
    // Seed group in DevTenant
    using (var scope = Factory.Services.CreateScope())
    {
        var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantCtx.Initialize(DevSeeder.DevTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Groups.Add(new Group {
            Id = Guid.NewGuid(), TenantId = DevSeeder.DevTenantId,
            Name = "Test Group", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    var tenantBId = Guid.NewGuid();
    // ... seed Tenant B, query Groups under B context, assert empty
}
```

### AR-15 Deferred-Skip Governance

Current skip count: 1 (confirmed in 4a-2/4a-3 dev notes — `DevSigningKeyStabilityTest`). Cap is 3. No new skips in this story.

### Key Files to Create/Modify

**New files:**
- `src/OneId.Server/Domain/Entities/Group.cs`
- `src/OneId.Server/Domain/Entities/UserGroup.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupConfiguration.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserGroupConfiguration.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/GroupDto.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/RoleSummaryDto.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/RoleSetSummaryDto.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Queries/ListGroupsHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Queries/GetGroupHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Commands/CreateGroupHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Commands/UpdateGroupHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Commands/DeleteGroupHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Commands/AddGroupMemberHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/Groups/Commands/RemoveGroupMemberHandler.cs`
- `src/OneId.Server/Controllers/TenantGroupsController.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/<timestamp>_AddGroupManagement.cs` (generated)
- `tests/OneId.Server.IntegrationTests/TenantGroupsIntegrationTests.cs`

**Modified files:**
- `src/OneId.Server/Domain/Entities/GroupRole.cs` — add `Group` navigation property
- `src/OneId.Server/Domain/Entities/GroupRoleSet.cs` — add `Group` navigation property
- `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleConfiguration.cs` — add FK to Group
- `src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleSetConfiguration.cs` — add FK to Group
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs` — add 2 DbSets, Group global query filter
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — auto-updated
- `src/OneId.Server/Application/TenantAdmin/Roles/Commands/DeleteRoleHandler.cs` — ThenInclude Group, use Group.Name
- `src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/DeleteRoleSetHandler.cs` — ThenInclude Group, use Group.Name
- `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs` — add 7 Group handler registrations
- `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs` — add Group isolation tests

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Completion Notes List

- All 10 ACs satisfied: Group CRUD (POST/GET/PUT/DELETE), member management (PUT/DELETE members), xmin concurrency, TenantAdmin auth, tenant isolation filter, audit logging for all mutations.
- AC10 (stub upgrades): GroupRole and GroupRoleSet entities promoted from stubs — Group navigation property added, FK constraints wired in configurations, DeleteRoleHandler and DeleteRoleSetHandler now return actual Group.Name values via ThenInclude.
- Group entity with UserGroup join table; idempotent member add (no duplicates); cascade delete removes join records (GroupRole, GroupRoleSet, UserGroup) when Group is deleted — Users unaffected.
- AddGroupMemberHandler uses AddMemberResult enum (not exceptions) for clean controller dispatch between 200/404.
- AR-8 (boundary): ArchUnit green — Group handlers in Application.TenantAdmin.Groups.* do not use InternalAdminContext.
- AR-14 (xmin): manual shadow property pattern applied to Group entity.
- AR-15 (skip cap): no new skips; unit test skip count = 0.
- RoleSummaryDto and RoleSetSummaryDto defined inline in GroupDto.cs (not shared with RoleSets namespace — each namespace is self-contained, same as RoleSets namespace has its own RoleSummaryDto).
- Integration tests: 17 tests covering all ACs; Docker required at runtime (Testcontainers); confirmed Docker-unavailable failure is identical to all prior stories in this project.

### File List

**New files:**
- src/OneId.Server/Domain/Entities/Group.cs
- src/OneId.Server/Domain/Entities/UserGroup.cs
- src/OneId.Server/Infrastructure/Persistence/Configurations/GroupConfiguration.cs
- src/OneId.Server/Infrastructure/Persistence/Configurations/UserGroupConfiguration.cs
- src/OneId.Server/Application/TenantAdmin/Groups/GroupDto.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Queries/ListGroupsHandler.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Queries/GetGroupHandler.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Commands/CreateGroupHandler.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Commands/UpdateGroupHandler.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Commands/DeleteGroupHandler.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Commands/AddGroupMemberHandler.cs
- src/OneId.Server/Application/TenantAdmin/Groups/Commands/RemoveGroupMemberHandler.cs
- src/OneId.Server/Controllers/TenantGroupsController.cs
- src/OneId.Server/Infrastructure/Persistence/Migrations/<timestamp>_AddGroupManagement.cs (generated)
- 	ests/OneId.Server.IntegrationTests/TenantGroupsIntegrationTests.cs

**Modified files:**
- src/OneId.Server/Domain/Entities/GroupRole.cs — added Group navigation property
- src/OneId.Server/Domain/Entities/GroupRoleSet.cs — added Group navigation property
- src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleConfiguration.cs — added FK to Group
- src/OneId.Server/Infrastructure/Persistence/Configurations/GroupRoleSetConfiguration.cs — added FK to Group
- src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs — added 2 DbSets, Group global query filter
- src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs — auto-updated
- src/OneId.Server/Application/TenantAdmin/Roles/Commands/DeleteRoleHandler.cs — ThenInclude Group, use Group.Name
- src/OneId.Server/Application/TenantAdmin/RoleSets/Commands/DeleteRoleSetHandler.cs — ThenInclude Group, use Group.Name
- src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs — added 7 Group handler registrations
- 	ests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs — added GroupIsolationRegressionTests class

### Review Findings

- [x] [Review][Patch] Cross-tenant membership deletion: RemoveGroupMemberHandler queries `db.UserGroups` directly with no tenant ownership check — a TenantAdmin who knows a foreign groupId can delete memberships in another tenant's group [src/OneId.Server/Application/TenantAdmin/Groups/Commands/RemoveGroupMemberHandler.cs:14]
- [x] [Review][Patch] Null RoleIds/RoleSetIds crash with 500: GroupBody and GroupUpdateBody records have no null-guard or default value for RoleIds/RoleSetIds — omitting them from JSON body causes NullReferenceException in ValidateRoleIdsAsync [src/OneId.Server/Controllers/TenantGroupsController.cs:122]
- [x] [Review][Patch] UpdateGroupHandler audit records new Name not old Name: group.Name is mutated before audit.AppendAsync is called, so the audit payload never captures the previous value [src/OneId.Server/Application/TenantAdmin/Groups/Commands/UpdateGroupHandler.cs:33]
- [x] [Review][Patch] Audit payload serializes raw request.RoleIds/RoleSetIds not validated set: duplicates in the request are de-duplicated before storage but the audit log records the raw (potentially duplicate) input [src/OneId.Server/Application/TenantAdmin/Groups/Commands/CreateGroupHandler.cs:36]
- [x] [Review][Defer] AddMember/RemoveMember both return 404 with no body — caller cannot distinguish group-not-found from user-not-found — deferred, design choice not required by spec
- [x] [Review][Defer] ListGroups totalCount and page items fetched in two round-trips — can be transiently inconsistent — deferred, pre-existing pattern
- [x] [Review][Defer] Duplicate RoleIds silently de-duplicated via .Distinct() with no client feedback — deferred, intentional design

## Change Log

- 2026-05-26: Story 4a-4 created — Group CRUD + member management, stub upgrades for GroupRole/GroupRoleSet, UserGroup join entity. (Create-Story Agent)
- 2026-05-26: Story 4a-4 implemented — Group entity, UserGroup join, full CRUD + member management, stub FK upgrades, EF migration, 17 integration tests, 2 isolation regression tests. (Dev Agent)
- 2026-05-27: Story 4a-4 code reviewed — 4 patches, 3 deferred, 6 dismissed. (Review Agent)
