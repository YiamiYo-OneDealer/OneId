# Story 4a.6: Per-User Dimension Assignments (Tenant Admin)

Status: done

## Story

As a Tenant Admin,
I want to assign Dimensional Attribute values to Users within my Tenant across all 5 axes,
so that token evaluation can include the User's organizational context in the introspection response.

## Acceptance Criteria

1. **Given** the `UserDimensionAssignment` table is introduced via EF Core migration
   **When** the migration runs
   **Then** the table has columns: `Id` (Guid), `UserId` (Guid FK → Users), `DimensionValueId` (Guid FK → DimensionValues), `AssignedAt` (DateTimeOffset)
   **And** a unique constraint exists on `(UserId, DimensionValueId)` — prevents duplicate assignment of the same value to the same user
   **And** the manual xmin shadow property (AR-14 pattern) is applied to `UserDimensionAssignment`

2. **Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/dimensions`
   **When** the request body contains a `valueId` (Guid) referencing an `IsActive=true` `DimensionValue` in the same Tenant
   **Then** a `UserDimensionAssignment` record is created linking the User to that `DimensionValue`
   **And** the response is HTTP 201 with the created `UserDimensionAssignmentDto`
   **And** a User may hold multiple values per axis (assigning a second Company value does NOT replace the first)

3. **Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/dimensions`
   **When** the `valueId` references a `DimensionValue` that is inactive (`IsActive=false`) or belongs to a different Tenant
   **Then** HTTP 422 is returned with `{ "error": "invalid_dimension_value" }`

4. **Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/dimensions`
   **When** the `userId` does not exist in the Tenant or belongs to a different Tenant
   **Then** HTTP 404 is returned with `{ "error": "user_not_found" }`

5. **Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/dimensions`
   **When** the `(userId, valueId)` combination is already assigned
   **Then** HTTP 409 is returned with `{ "error": "already_assigned" }`

6. **Given** a Tenant Admin calls `DELETE /api/tenant/users/{userId}/dimensions/{assignmentId}`
   **When** the assignment exists and belongs to the given User in the Tenant
   **Then** the `UserDimensionAssignment` record is physically deleted (not soft-deleted)
   **And** the User's other dimension assignments on the same or other axes are unaffected
   **And** the response is HTTP 204

7. **Given** a Tenant Admin calls `DELETE /api/tenant/users/{userId}/dimensions/{assignmentId}`
   **When** the assignment does not exist or belongs to a different User/Tenant
   **Then** HTTP 404 is returned

8. **Given** a Tenant Admin calls `GET /api/tenant/users/{userId}/dimensions`
   **When** the request is processed
   **Then** all current dimension assignments for the User are returned grouped by axis
   **And** the response shape is: `{ "Company": ["value1", "value2"], "Location": [], "Branch": [], "Make": [], "MarketSegment": [] }`
   **And** axes with no assignments return an **empty array** — they are NOT omitted from the response
   **And** only the 5 defined axes (`Company`, `Location`, `Branch`, `Make`, `MarketSegment`) appear as keys

9. **Given** a Tenant Admin calls `GET /api/tenant/users/{userId}/dimensions`
   **When** the `userId` does not exist in the Tenant
   **Then** HTTP 404 is returned

10. **Given** `TenantIsolationRegressionTests.cs` exists
    **When** the tests run
    **Then** a new test confirms that `UserDimensionAssignment` records for a User in Tenant A are NOT visible under Tenant B's context

## Tasks / Subtasks

- [x] Task 1: Create `UserDimensionAssignment` domain entity (AC: 1)
  - [x] Create `src/OneId.Server/Domain/Entities/UserDimensionAssignment.cs` with properties: `Id`, `UserId`, `DimensionValueId`, `AssignedAt`, navigation properties `User` and `DimensionValue`

- [x] Task 2: Create EF Core configuration and migration (AC: 1)
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/UserDimensionAssignmentConfiguration.cs` — table `user_dimension_assignments`, unique index on `(UserId, DimensionValueId)`, manual xmin shadow property (AR-14)
  - [x] Add `DbSet<UserDimensionAssignment>` to `AppDbContext.cs`
  - [x] Add global query filter for `UserDimensionAssignment` in `AppDbContext.OnModelCreating` (via `DimensionValue.TenantId` navigation)
  - [x] Run `dotnet ef migrations add AddUserDimensionAssignments` to generate migration

- [x] Task 3: Create DTOs (AC: 2, 8)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/UserDimensionAssignmentDto.cs` — single assignment record
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/UserDimensionsGroupedDto.cs` — grouped response with fixed 5-axis shape

- [x] Task 4: Create application handlers (AC: 2–9)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/Queries/GetUserDimensionsHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/AssignUserDimensionHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/RemoveUserDimensionHandler.cs`

- [x] Task 5: Create or extend controller (AC: 2–9)
  - [x] Create `src/OneId.Server/Controllers/TenantUserDimensionsController.cs` — route `api/tenant/users/{userId}/dimensions`, Authorize TenantAdmin

- [x] Task 6: Register handlers in DI (AC: 2, 6, 8)
  - [x] Update `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs` — add 3 handler registrations

- [x] Task 7: Extend isolation regression tests (AC: 10)
  - [x] Update `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs` — add `UserDimensionAssignment_IsNotVisible_FromOtherTenant` test

- [x] Task 8: Create integration tests (AC: 2–9)
  - [x] Create `tests/OneId.Server.IntegrationTests/TenantUserDimensionsIntegrationTests.cs` — cover POST (201, 409, 422, 404), GET (grouped response with all 5 axes, 404), DELETE (204, 404)

## Dev Notes

### Entity Shape

```csharp
// src/OneId.Server/Domain/Entities/UserDimensionAssignment.cs
using OneId.Server.Domain.Entities;

namespace OneId.Server.Domain.Entities;

public class UserDimensionAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DimensionValueId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public User User { get; set; } = null!;
    public DimensionValue DimensionValue { get; set; } = null!;
}
```

No `TenantId` column on `UserDimensionAssignment` itself — tenant isolation is enforced indirectly: `UserId` FK → `Users` (already has global query filter on `TenantId`), and `DimensionValueId` FK → `DimensionValues` (also has global query filter). The query filter strategy is described below.

### EF Configuration

```csharp
// src/OneId.Server/Infrastructure/Persistence/Configurations/UserDimensionAssignmentConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class UserDimensionAssignmentConfiguration : IEntityTypeConfiguration<UserDimensionAssignment>
{
    public void Configure(EntityTypeBuilder<UserDimensionAssignment> builder)
    {
        builder.ToTable("user_dimension_assignments");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);  // deleting user removes their assignments

        builder.HasOne(a => a.DimensionValue)
            .WithMany()
            .HasForeignKey(a => a.DimensionValueId)
            .OnDelete(DeleteBehavior.Restrict);  // deactivating a value does NOT cascade-delete assignments

        builder.HasIndex(a => new { a.UserId, a.DimensionValueId }).IsUnique();

        // AR-14: manual xmin shadow property (Npgsql v10)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        // DO NOT add HasQueryFilter here — set in AppDbContext.OnModelCreating
    }
}
```

### Global Query Filter Strategy

`UserDimensionAssignment` has no direct `TenantId` column. Two approaches — **use the join-through-DimensionValue approach** which is cleaner and already aligned with the existing `DimensionValues` global filter:

```csharp
// In AppDbContext.OnModelCreating — Story 4a.6
builder.Entity<UserDimensionAssignment>().HasQueryFilter(a =>
    a.DimensionValue.TenantId == tenantContext.TenantId);
```

This requires that EF navigation `DimensionValue` is loaded or that EF generates a JOIN in the query. EF Core handles this correctly when using `.Include()` or when the navigation is in the filter expression. **Important:** This filter will only work if `DimensionValues` also has a global filter — which it does (added in 4a-5). The compound effect means: assignments visible only if the DimensionValue belongs to the current tenant.

**Alternative (simpler, avoids join in filter):** Add a `TenantId` column to `UserDimensionAssignment` for a direct filter — but this violates normalization since TenantId can be derived from User. **Use the navigation-based filter** as specified above to keep the design clean.

### AppDbContext Updates

Two additions in `AppDbContext.cs`:

1. Add `DbSet`:
```csharp
public DbSet<UserDimensionAssignment> UserDimensionAssignments => Set<UserDimensionAssignment>();
```

2. Add global query filter (after DimensionValue filter):
```csharp
// Story 4a.6: UserDimensionAssignment tenant isolation (via DimensionValue navigation).
builder.Entity<UserDimensionAssignment>().HasQueryFilter(a =>
    a.DimensionValue.TenantId == tenantContext.TenantId);
```

### DTO Shapes

```csharp
// src/OneId.Server/Application/TenantAdmin/Dimensions/UserDimensionAssignmentDto.cs
namespace OneId.Server.Application.TenantAdmin.Dimensions;

public sealed record UserDimensionAssignmentDto(
    Guid Id,
    Guid UserId,
    Guid DimensionValueId,
    string Axis,
    string Value,
    DateTimeOffset AssignedAt);
```

```csharp
// src/OneId.Server/Application/TenantAdmin/Dimensions/UserDimensionsGroupedDto.cs
namespace OneId.Server.Application.TenantAdmin.Dimensions;

// Represents the grouped response for GET /api/tenant/users/{userId}/dimensions
// All 5 axis keys MUST be present, even if the list is empty.
public sealed record UserDimensionsGroupedDto(
    IReadOnlyList<string> Company,
    IReadOnlyList<string> Location,
    IReadOnlyList<string> Branch,
    IReadOnlyList<string> Make,
    IReadOnlyList<string> MarketSegment);
```

### Handler Patterns

**GetUserDimensionsHandler** — returns `UserDimensionsGroupedDto` (all 5 axes):
```csharp
// First verify user exists in this tenant
var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
if (!userExists) throw new UserNotFoundException();

// Load assignments with navigation
var assignments = await db.UserDimensionAssignments
    .Include(a => a.DimensionValue)
    .Where(a => a.UserId == userId)
    .ToListAsync(ct);

// Build grouped response — all 5 axes MUST be present
var grouped = assignments
    .GroupBy(a => a.DimensionValue.Axis)
    .ToDictionary(g => g.Key, g => g.Select(a => a.DimensionValue.Value).ToList());

return new UserDimensionsGroupedDto(
    Company:       grouped.GetValueOrDefault(DimensionAxis.Company, []),
    Location:      grouped.GetValueOrDefault(DimensionAxis.Location, []),
    Branch:        grouped.GetValueOrDefault(DimensionAxis.Branch, []),
    Make:          grouped.GetValueOrDefault(DimensionAxis.Make, []),
    MarketSegment: grouped.GetValueOrDefault(DimensionAxis.MarketSegment, []));
```

**AssignUserDimensionHandler** — validate user + value, check duplicate:
```csharp
// 1. Validate user exists in this tenant (global query filter handles cross-tenant)
var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
if (!userExists) throw new UserNotFoundException();

// 2. Validate DimensionValue is active and belongs to this tenant
// Must use .IgnoreQueryFilters() here? NO — the global filter on DimensionValues already
// scopes to current tenant. But we also need IsActive check.
var dimValue = await db.DimensionValues
    .FirstOrDefaultAsync(d => d.Id == valueId && d.IsActive, ct);
if (dimValue is null) throw new InvalidDimensionValueException();

// 3. Check duplicate
var alreadyAssigned = await db.UserDimensionAssignments
    .AnyAsync(a => a.UserId == userId && a.DimensionValueId == valueId, ct);
if (alreadyAssigned) throw new DimensionAlreadyAssignedException();

// 4. Create assignment
var assignment = new UserDimensionAssignment
{
    Id = Guid.NewGuid(),
    UserId = userId,
    DimensionValueId = valueId,
    AssignedAt = DateTimeOffset.UtcNow,
};
db.UserDimensionAssignments.Add(assignment);
await db.SaveChangesAsync(ct);

// 5. Return DTO with axis/value info from loaded dimValue
return new UserDimensionAssignmentDto(
    assignment.Id, userId, valueId,
    dimValue.Axis.ToString(), dimValue.Value, assignment.AssignedAt);
```

**RemoveUserDimensionHandler** — physical delete, return bool for 404:
```csharp
var assignment = await db.UserDimensionAssignments
    .FirstOrDefaultAsync(a => a.Id == assignmentId && a.UserId == userId, ct);
if (assignment is null) return false;
db.UserDimensionAssignments.Remove(assignment);
await db.SaveChangesAsync(ct);
return true;
```

### Exception Types

Define inline in respective handler files (same pattern as prior stories):
```csharp
// In AssignUserDimensionHandler.cs:
public sealed class UserNotFoundException()
    : Exception("User not found in this tenant.");

public sealed class InvalidDimensionValueException()
    : Exception("DimensionValue is inactive or belongs to a different tenant.");

public sealed class DimensionAlreadyAssignedException()
    : Exception("This dimension value is already assigned to the user.");
```

### Controller Pattern

```csharp
[ApiController]
[Route("api/tenant/users/{userId:guid}/dimensions")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantUserDimensionsController(
    GetUserDimensionsHandler getHandler,
    AssignUserDimensionHandler assignHandler,
    RemoveUserDimensionHandler removeHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        try
        {
            var dto = await getHandler.HandleAsync(userId, ct);
            return Ok(dto);
        }
        catch (UserNotFoundException)
        {
            return NotFound(new { error = "user_not_found" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Assign(Guid userId, [FromBody] AssignDimensionBody body, CancellationToken ct)
    {
        try
        {
            var dto = await assignHandler.HandleAsync(userId, body.ValueId, ct);
            return CreatedAtAction(nameof(Get), new { userId }, dto);
        }
        catch (UserNotFoundException)
        {
            return NotFound(new { error = "user_not_found" });
        }
        catch (InvalidDimensionValueException)
        {
            return UnprocessableEntity(new { error = "invalid_dimension_value" });
        }
        catch (DimensionAlreadyAssignedException)
        {
            return Conflict(new { error = "already_assigned" });
        }
    }

    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        var found = await removeHandler.HandleAsync(userId, assignmentId, ct);
        return found ? NoContent() : NotFound();
    }
}

public sealed record AssignDimensionBody(Guid ValueId);
```

### No Audit Logging

The epics spec states: *"dimension assignments are not audit-sensitive — removal is clean"*. Do NOT add audit log entries for `UserDimensionAssignment` mutations. This is a deliberate design decision to keep assignment management lightweight.

### Important: Global Query Filter Limitation

The filter `a.DimensionValue.TenantId == tenantContext.TenantId` on `UserDimensionAssignment` requires EF Core to join `DimensionValues` when querying. This is valid and supported but has a subtlety: **do not call `.IgnoreQueryFilters()` on `UserDimensionAssignments` in the duplicate check** — the filter is what prevents cross-tenant duplicate checks from returning false positives. Keep the filter active for all queries in this story.

For the cross-tenant `valueId` validation (AC3): the `DimensionValues` global filter already ensures `dimValue` returned from `db.DimensionValues.FirstOrDefaultAsync(...)` only includes values from the current tenant. If the `valueId` belongs to another tenant, it returns `null` → `InvalidDimensionValueException` → 422. This is correct behavior.

### TenantIsolation Regression Test Pattern

```csharp
[Fact]
public async Task UserDimensionAssignment_IsNotVisible_FromOtherTenant()
{
    // 1. In DevTenant scope: seed a DimensionValue and a User, create an assignment
    //    - Use direct DB writes (same pattern as DimensionValueIsolationRegressionTests)
    // 2. Create TenantB (SeedSecondTenantAsync)
    // 3. Initialize TenantContext with TenantB
    // 4. Query UserDimensionAssignments — assert empty
    //    (the filter on DimensionValue.TenantId prevents cross-tenant visibility)
}
```

**NOTE on writing the isolation test:** `UserDimensionAssignments` does not have a direct `TenantId` column. The global query filter goes through `DimensionValue.TenantId`. When querying `db.UserDimensionAssignments` with TenantB context, EF will JOIN to `dimension_values` and apply the tenant filter there. The isolation test should assert that assignments seeded under DevTenant are NOT visible with a TenantB context.

To write directly to DB (bypassing tenant context) for the seed: use `.IgnoreQueryFilters()` is NOT needed for writes — only reads are filtered. Direct `db.UserDimensionAssignments.Add(...)` + `db.SaveChangesAsync()` always works regardless of tenant context.

### TenantServiceExtensions

Add after the existing Dimension handler registrations:
```csharp
using OneId.Server.Application.TenantAdmin.Dimensions.Commands; // already imported
using OneId.Server.Application.TenantAdmin.Dimensions.Queries;  // already imported

// in AddTenantAdminHandlers, after DeactivateDimensionValueHandler:
services.AddScoped<GetUserDimensionsHandler>();
services.AddScoped<AssignUserDimensionHandler>();
services.AddScoped<RemoveUserDimensionHandler>();
```

No new `using` statements needed — the namespaces are already imported for the 4a-5 handlers.

### Integration Test Seed Helpers

```csharp
private async Task<Guid> SeedDimensionValueAsync(DimensionAxis axis, string value)
{
    using var scope = Factory.Services.CreateScope();
    var tenantCtx = scope.ServiceProvider.GetRequiredService<TenantContext>();
    tenantCtx.Initialize(DevSeeder.DevTenantId);
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var entity = new DimensionValue
    {
        Id = Guid.NewGuid(), TenantId = DevSeeder.DevTenantId,
        Axis = axis, Value = value, IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };
    db.DimensionValues.Add(entity);
    await db.SaveChangesAsync();
    return entity.Id;
}

private async Task<Guid> SeedUserAsync(string email)
{
    // Same pattern as TenantGroupsIntegrationTests.SeedUserAsync
}
```

### AR-15 Deferred-Skip Governance

Current skip count: 1 (`DevSigningKeyStabilityTest` — confirmed through 4a-5). Cap is 3. No new skips in this story.

### Architecture Compliance

- AR-8 (boundary): all new handlers must live in `Application.TenantAdmin.Dimensions.*` (Queries/Commands subfolders).
- AR-14 (xmin): `UserDimensionAssignment` needs xmin shadow property — confirmed by AppDbContext comment `// Epic 4a adds: ... UserDimensionAssignment`.
- No `PagedResponse<T>` needed — `UserDimensionsGroupedDto` is a fixed-shape response.
- Story 4a.7 (User Lifecycle) will also touch `TenantUsersController` or a similar user-management controller. Keep `TenantUserDimensionsController` focused only on dimension assignment endpoints.

### Project Structure Notes

- New controller: `TenantUserDimensionsController.cs` — no existing `TenantUsersController` exists yet, so no merge conflict risk.
- New entity: `UserDimensionAssignment.cs` in `Domain/Entities/` — not yet present (confirmed by directory listing).
- Handler namespace: `OneId.Server.Application.TenantAdmin.Dimensions.Commands` and `.Queries` — reuse existing folders from 4a-5.

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 4a, Story 4a.6]
- [Source: _bmad-output/planning-artifacts/architecture.md — FR-10, UserDimensionAssignment entity, Application/Dimensions/]
- [Source: _bmad-output/implementation-artifacts/4a-5-dimensional-attribute-reference-lists.md — DimensionValue entity, global query filter pattern, xmin pattern, controller pattern]
- [Source: src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs — existing global query filter pattern]
- [Source: src/OneId.Server/Infrastructure/Persistence/Configurations/GroupConfiguration.cs — manual xmin pattern]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 10 ACs satisfied.
- `UserDimensionAssignment` entity with FK to `User` (cascade delete) and FK to `DimensionValue` (restrict delete — deactivating a value does not cascade-delete assignments).
- No `TenantId` column on `UserDimensionAssignment` — tenant isolation via navigation-based global query filter `a.DimensionValue.TenantId == tenantContext.TenantId` (EF generates a JOIN to dimension_values for the filter).
- EF migration `20260526211950_AddUserDimensionAssignments` generated with `user_dimension_assignments` table, unique index on `(UserId, DimensionValueId)`, xmin shadow property.
- `GetUserDimensionsHandler`: loads assignments with `.Include(a => a.DimensionValue)`, groups by axis, returns `UserDimensionsGroupedDto` with all 5 axes always present (empty array if no assignments for that axis).
- `AssignUserDimensionHandler`: validates user exists (via global filter — cross-tenant user returns not-found), validates DimensionValue is active in current tenant (global filter + IsActive check), checks duplicate before insert. No audit logging per spec.
- `RemoveUserDimensionHandler`: physical delete (not soft-delete), returns bool for 404 dispatch. Query filters `a.UserId == userId` to prevent deleting another user's assignment.
- Exception types defined inline in handler files: `AssignDimensionUserNotFoundException`, `UserDimensionUserNotFoundException`, `InvalidDimensionValueException`, `DimensionAlreadyAssignedException`.
- `TenantUserDimensionsController`: route `api/tenant/users/{userId:guid}/dimensions`, TenantAdmin authorized, maps exceptions to HTTP 404/422/409/204.
- `UserDimensionAssignmentIsolationRegressionTests` added to isolation tests — seeds DevTenant data directly then asserts empty under TenantB context.
- `TenantUserDimensionsIntegrationTests` created with 12 tests covering all ACs. Integration tests require Docker (Testcontainers).
- AR-8 boundary: all handlers in `Application.TenantAdmin.Dimensions.*`.
- AR-14 xmin: shadow property applied to `UserDimensionAssignment` via manual pattern.
- AR-15 skip cap: no new skips; count remains at 1.

### File List

**New files:**
- src/OneId.Server/Domain/Entities/UserDimensionAssignment.cs
- src/OneId.Server/Infrastructure/Persistence/Configurations/UserDimensionAssignmentConfiguration.cs
- src/OneId.Server/Infrastructure/Persistence/Migrations/20260526211950_AddUserDimensionAssignments.cs (generated)
- src/OneId.Server/Infrastructure/Persistence/Migrations/20260526211950_AddUserDimensionAssignments.Designer.cs (generated)
- src/OneId.Server/Application/TenantAdmin/Dimensions/UserDimensionAssignmentDto.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/UserDimensionsGroupedDto.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/Queries/GetUserDimensionsHandler.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/AssignUserDimensionHandler.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/RemoveUserDimensionHandler.cs
- src/OneId.Server/Controllers/TenantUserDimensionsController.cs
- tests/OneId.Server.IntegrationTests/TenantUserDimensionsIntegrationTests.cs

**Modified files:**
- src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs — added `DbSet<UserDimensionAssignment>`, global query filter via navigation
- src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs — auto-updated by EF tooling
- src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs — added 3 handler registrations
- tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs — added `UserDimensionAssignmentIsolationRegressionTests` class

### Review Findings

- [x] [Review][Decision] GET grouped response returns value strings — no `assignmentId` in response; clients cannot call DELETE without a separate lookup. **Resolved:** replaced POST+DELETE with PUT replace-on-save; GET value strings are now sufficient for the write contract.
- [x] [Review][Patch] Race condition on duplicate concurrent writes causing unhandled `DbUpdateException` → 500. **Fixed:** `SetUserDimensionsHandler` catches `DbUpdateException` on `23505` and maps to `DimensionAssignmentConflictException` → 409.
- [x] [Review][Patch] DELETE 404 returned bare `NotFound()` with no body. **Fixed:** DELETE endpoint removed; superseded by PUT replace-on-save.
- [x] [Review][Patch] No cross-tenant write isolation test. **Fixed:** added `UserDimensionAssignment_Put_CannotTargetOtherTenantUser` to `UserDimensionAssignmentIsolationRegressionTests`.
- [x] [Review][Defer] `RemoveUserDimensionHandler` cross-tenant isolation relied on navigation-join filter only. Pre-existing architectural constraint — **moot**, handler removed.
- [x] [Review][Defer] Double-query without transaction in GET/Assign handlers. Low-probability race, pre-existing pattern. [GetUserDimensionsHandler.cs]

## Change Log

- 2026-05-27: Story 4a-6 implemented — UserDimensionAssignment entity + EF migration, 3 handlers, TenantUserDimensionsController, 12 integration tests, 1 isolation regression test. (Dev Agent)
- 2026-05-27: Code review complete — 1 decision-needed, 3 patches, 2 deferred, 4 dismissed. (Review)
