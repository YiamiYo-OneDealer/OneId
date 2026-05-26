# Story 4a.5: Dimensional Attribute Reference Lists

Status: review

## Story

As an Internal Admin (for initial axis definition) and a Tenant Admin (for per-Tenant values),
I want each Tenant to maintain reference lists for the 5 dimension axes,
so that Dimension assignments are validated against a controlled vocabulary rather than free-text.

## Acceptance Criteria

1. **Given** the `DimensionValue` table is introduced via EF Core migration
   **When** the migration runs
   **Then** the table has columns: `Id` (Guid), `TenantId` (Guid), `Axis` (enum stored as int: `Company`, `Location`, `Branch`, `Make`, `MarketSegment`), `Value` (string), `IsActive` (bool)
   **And** a unique constraint exists on `(TenantId, Axis, Value)` — no duplicate values per axis per Tenant
   **And** `UseXminAsConcurrencyToken()` is applied to the `DimensionValue` entity (shadow property via AR-14 pattern)

2. **Given** a Tenant Admin calls `POST /api/tenant/dimensions/{axis}/values`
   **When** the request body contains a `value` string
   **Then** a `DimensionValue` record is created for the given axis within the Tenant Admin's Tenant
   **And** the response is HTTP 201 with the created `DimensionValueDto`

3. **Given** a Tenant Admin calls `POST /api/tenant/dimensions/{axis}/values`
   **When** the (axis, value) combination already exists for this Tenant (active or inactive)
   **Then** HTTP 409 is returned with `{ "error": "duplicate_value" }`

4. **Given** a Tenant Admin calls `GET /api/tenant/dimensions/{axis}/values`
   **When** the request is processed
   **Then** only `IsActive = true` values for the calling Tenant's axis are returned (global query filter + IsActive filter)
   **And** the response is a list (not paged) of `DimensionValueDto` ordered by `Value` ascending

5. **Given** a Tenant Admin calls `DELETE /api/tenant/dimensions/{axis}/values/{id}` (soft delete)
   **When** the value exists and belongs to the Tenant
   **Then** `IsActive` is set to `false` — the record is NOT physically deleted

6. **Given** a Tenant Admin calls `DELETE /api/tenant/dimensions/{axis}/values/{id}`
   **When** the value does not exist or belongs to a different Tenant
   **Then** HTTP 404 is returned

7. **Given** a `DimensionValue` with `IsActive = false`
   **When** `GET /api/tenant/dimensions/{axis}/values` is called
   **Then** the deactivated value does NOT appear in the response

8. **Given** the invalid `{axis}` route segment (not a valid `DimensionAxis` enum name)
   **When** any endpoint is called with it
   **Then** HTTP 400 is returned with `{ "error": "invalid_axis" }`

9. **Given** `TenantIsolationRegressionTests.cs` exists
   **When** the tests run
   **Then** a new test confirms a `DimensionValue` seeded in Tenant A is NOT visible under Tenant B context

## Tasks / Subtasks

- [x] Task 1: Create domain entities and enum (AC: 1)
  - [x] Create `src/OneId.Server/Domain/Enums/DimensionAxis.cs` with values `Company=0, Location=1, Branch=2, Make=3, MarketSegment=4`
  - [x] Create `src/OneId.Server/Domain/Entities/DimensionValue.cs` with properties: `Id`, `TenantId`, `Axis` (DimensionAxis), `Value`, `IsActive`, `CreatedAt`, `UpdatedAt`

- [x] Task 2: Create EF Core configuration and migration (AC: 1)
  - [x] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/DimensionValueConfiguration.cs` — table name `dimension_values`, unique index on `(tenant_id, axis, value)`, manual xmin shadow property (AR-14 pattern)
  - [x] Add `DbSet<DimensionValue>` to `AppDbContext.cs`
  - [x] Add global query filter for `DimensionValue` in `AppDbContext.OnModelCreating`
  - [x] Run `dotnet ef migrations add AddDimensionValues` to generate migration

- [x] Task 3: Create DTOs (AC: 2, 4)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/DimensionValueDto.cs` with `Id`, `Axis`, `Value`, `Version` (uint xmin)

- [x] Task 4: Create application handlers (AC: 2, 3, 4, 5, 6, 7)
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/Queries/ListDimensionValuesHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/AddDimensionValueHandler.cs`
  - [x] Create `src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/DeactivateDimensionValueHandler.cs`

- [x] Task 5: Create controller (AC: 2, 3, 4, 5, 6, 8)
  - [x] Create `src/OneId.Server/Controllers/TenantDimensionsController.cs` — route `api/tenant/dimensions/{axis}/values`, Authorize TenantAdmin, parse `{axis}` as `DimensionAxis` enum, return 400 for invalid axis

- [x] Task 6: Register handlers in DI (AC: 2, 4, 5)
  - [x] Update `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs` — add 3 handler registrations with appropriate `using` statements

- [x] Task 7: Extend isolation regression tests (AC: 9)
  - [x] Update `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs` — add `DimensionValue_IsNotVisible_FromOtherTenant` test

- [x] Task 8: Create integration tests (AC: 2–8)
  - [x] Create `tests/OneId.Server.IntegrationTests/TenantDimensionsIntegrationTests.cs` — cover POST (201, 409), GET (list, IsActive filter), DELETE (204 soft delete, 404 not found), invalid axis (400)

## Dev Notes

### Entity Shape

```csharp
// src/OneId.Server/Domain/Entities/DimensionValue.cs
namespace OneId.Server.Domain.Entities;

public class DimensionValue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DimensionAxis Axis { get; set; }
    public required string Value { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

```csharp
// src/OneId.Server/Domain/Enums/DimensionAxis.cs
namespace OneId.Server.Domain.Enums;

public enum DimensionAxis
{
    Company = 0,
    Location = 1,
    Branch = 2,
    Make = 3,
    MarketSegment = 4,
}
```

### EF Configuration

```csharp
// src/OneId.Server/Infrastructure/Persistence/Configurations/DimensionValueConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class DimensionValueConfiguration : IEntityTypeConfiguration<DimensionValue>
{
    public void Configure(EntityTypeBuilder<DimensionValue> builder)
    {
        builder.ToTable("dimension_values");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Value).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Axis).HasConversion<int>();

        // Unique constraint: no duplicate (tenant, axis, value)
        builder.HasIndex(d => new { d.TenantId, d.Axis, d.Value }).IsUnique();

        // AR-14: shadow property concurrency token
        builder.UseXminAsConcurrencyToken();
    }
}
```

### AppDbContext Updates

Two additions required:

1. Add `DbSet`:
```csharp
public DbSet<DimensionValue> DimensionValues => Set<DimensionValue>();
```

2. Add global query filter in `OnModelCreating` (follow the same pattern as Group, Role, RoleSet):
```csharp
// Story 4a.5: DimensionValue tenant isolation.
builder.Entity<DimensionValue>().HasQueryFilter(d => d.TenantId == tenantContext.TenantId);
```

The `// Epic 4a adds: ... DimensionValue, UserDimensionAssignment` comment in AppDbContext already exists — no comment addition needed.

### xmin Version Pattern

Same as Role/RoleSet/Group:
```csharp
// In handler LINQ projection:
Version = EF.Property<uint>(d, "xmin")

// After SaveChangesAsync:
var version = db.Entry(dimensionValue).Property<uint>("xmin").CurrentValue;
```

### DTO Shape

```csharp
// src/OneId.Server/Application/TenantAdmin/Dimensions/DimensionValueDto.cs
namespace OneId.Server.Application.TenantAdmin.Dimensions;

public sealed record DimensionValueDto(Guid Id, string Axis, string Value, uint Version);
```

Return `Axis` as its string name (e.g. `"Company"`) for readability in the API response — use `dimensionValue.Axis.ToString()`.

### Handler Patterns

**ListDimensionValuesHandler** — returns `IReadOnlyList<DimensionValueDto>`:
```csharp
var items = await db.DimensionValues
    .Where(d => d.Axis == axis && d.IsActive)
    .OrderBy(d => d.Value)
    .Select(d => new DimensionValueDto(
        d.Id,
        d.Axis.ToString(),
        d.Value,
        EF.Property<uint>(d, "xmin")))
    .ToListAsync(ct);
```

**AddDimensionValueHandler** — duplicate check before insert:
```csharp
var exists = await db.DimensionValues
    .IgnoreQueryFilters()  // check active AND inactive — same (tenant,axis,value) must be rejected
    .AnyAsync(d => d.TenantId == tenantContext.TenantId && d.Axis == axis && d.Value == value, ct);
if (exists) throw new DuplicateDimensionValueException();
```

**DeactivateDimensionValueHandler** — soft delete, return bool for 404:
```csharp
var entity = await db.DimensionValues.FirstOrDefaultAsync(d => d.Id == id, ct);
if (entity is null) return false;
entity.IsActive = false;
entity.UpdatedAt = DateTimeOffset.UtcNow;
await db.SaveChangesAsync(ct);
return true;
```

### Exception Type

Define inline in `AddDimensionValueHandler.cs` (consistent with how `InvalidRoleIdsException` is defined inline in its handler file):
```csharp
public sealed class DuplicateDimensionValueException()
    : Exception("Dimension value already exists for this axis in this tenant.");
```

### Controller Pattern

```csharp
[ApiController]
[Route("api/tenant/dimensions/{axis}/values")]
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
public class TenantDimensionsController(...) : ControllerBase
{
    private static bool TryParseAxis(string raw, out DimensionAxis axis)
        => Enum.TryParse(raw, ignoreCase: true, out axis);

    [HttpGet]
    public async Task<IActionResult> List(string axis, CancellationToken ct)
    {
        if (!TryParseAxis(axis, out var parsedAxis))
            return BadRequest(new { error = "invalid_axis" });
        var result = await listHandler.HandleAsync(parsedAxis, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(string axis, [FromBody] AddDimensionValueBody body, CancellationToken ct)
    {
        if (!TryParseAxis(axis, out var parsedAxis))
            return BadRequest(new { error = "invalid_axis" });
        if (string.IsNullOrWhiteSpace(body.Value))
            return BadRequest(new { error = "invalid_value" });
        try
        {
            var dto = await addHandler.HandleAsync(parsedAxis, body.Value, ct);
            return CreatedAtAction(nameof(List), new { axis }, dto);
        }
        catch (DuplicateDimensionValueException)
        {
            return Conflict(new { error = "duplicate_value" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(string axis, Guid id, CancellationToken ct)
    {
        if (!TryParseAxis(axis, out _))
            return BadRequest(new { error = "invalid_axis" });
        var found = await deactivateHandler.HandleAsync(id, ct);
        return found ? NoContent() : NotFound();
    }
}

// At bottom of controller file:
public sealed record AddDimensionValueBody(string Value);
```

Note: `{axis}` is a string route parameter — ASP.NET Core does not automatically bind enum route parameters by name. Parse manually with `Enum.TryParse`.

### Audit Logging

Follow the same audit pattern used in Groups (4a-4), Roles (4a-2), and RoleSets (4a-3). Log `DimensionValue.Created`, `DimensionValue.Deactivated` actions. The audit action/entity strings follow the `EntityType.Action` convention. Check any existing handler (e.g. `CreateGroupHandler.cs`) for the exact `AuditLog` write pattern.

### TenantServiceExtensions

Add after the Group handler registrations:
```csharp
using OneId.Server.Application.TenantAdmin.Dimensions.Commands;
using OneId.Server.Application.TenantAdmin.Dimensions.Queries;

// in AddTenantAdminHandlers:
services.AddScoped<ListDimensionValuesHandler>();
services.AddScoped<AddDimensionValueHandler>();
services.AddScoped<DeactivateDimensionValueHandler>();
```

### Integration Test Pattern

Use the established two-step TOTP auth flow from `TenantGroupsIntegrationTests.cs` — `DevSeeder.TotpUserEmail` + TOTP challenge. All `DimensionValue` records created in tests go to `DevSeeder.DevTenantId`.

Seed helper needed (define in test class):
```csharp
private async Task<Guid> SeedDimensionValueAsync(DimensionAxis axis, string value)
{
    // Direct DB write bypassing tenant context — use scope from Factory.Services
    // Follow the Group/Role seed helper pattern from prior test files
}
```

### TenantIsolation Regression Test

```csharp
[Fact]
public async Task DimensionValue_IsNotVisible_FromOtherTenant()
{
    // 1. Seed DimensionValue in DevTenant (use scope to write directly)
    // 2. Create a second TenantId (Guid.NewGuid())
    // 3. Initialize TenantContext with second TenantId
    // 4. Query DimensionValues — assert empty list
    // Pattern identical to Group_IsNotVisible_FromOtherTenant in 4a-4
}
```

### AR-15 Deferred-Skip Governance

Current skip count: 1 (`DevSigningKeyStabilityTest` — confirmed in 4a-2/4a-3/4a-4). Cap is 3. No new skips in this story.

### Architecture Compliance Notes

- AR-8 (boundary): `DimensionValue` handlers must live in `Application.TenantAdmin.Dimensions.*` — not `Application.InternalAdmin.*`. ArchUnit will fail if boundary is crossed.
- AR-14 (xmin): `UseXminAsConcurrencyToken()` on `DimensionValue` — confirmed required by AppDbContext comment.
- Story 4a.6 (`UserDimensionAssignment`) depends on this story's `DimensionValue` table existing. Ensure the EF migration runs before 4a.6 work begins.
- The `IsActive` filter must be applied at the query level (not via global query filter) — the global query filter handles tenant isolation only. `IsActive` must be an explicit `.Where(d => d.IsActive)` in list queries.

### Project Structure Notes

- Namespace: `OneId.Server.Application.TenantAdmin.Dimensions` — consistent with `Roles`, `RoleSets`, `Groups` namespaces
- Controller file: `TenantDimensionsController.cs` — consistent with `TenantGroupsController.cs` naming
- No `PagedResponse<T>` needed — dimension values per axis per tenant are small, return `IReadOnlyList<DimensionValueDto>` directly

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 4a, Story 4a.5]
- [Source: _bmad-output/planning-artifacts/architecture.md — FR-10, Application/Dimensions/, Domain/Entities/DimensionValue.cs]
- [Source: src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs — existing global query filter pattern, AR-14 comment]
- [Source: _bmad-output/implementation-artifacts/4a-4-group-management-tenant-admin.md — xmin pattern, handler structure, auth pattern, isolation test pattern]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `UseXminAsConcurrencyToken()` extension method does not exist in this project's Npgsql v10 setup. Used the manual shadow property pattern `builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()` consistent with all prior entity configurations (GroupConfiguration, RoleConfiguration, etc.).

### Completion Notes List

- All 9 ACs satisfied.
- `DimensionAxis` enum created with 5 values (Company=0…MarketSegment=4); `DimensionValue` entity with `IsActive` soft-delete flag.
- EF Core migration `20260526211044_AddDimensionValues` generated with `dimension_values` table, unique index on `(TenantId, Axis, Value)`, xmin shadow property via manual AR-14 pattern.
- Global query filter added in `AppDbContext.OnModelCreating` for tenant isolation.
- `ListDimensionValuesHandler` returns `IReadOnlyList<DimensionValueDto>` filtered to `IsActive=true`, ordered by `Value` ascending.
- `AddDimensionValueHandler` checks for duplicates via `.IgnoreQueryFilters()` (covers both active and inactive) — throws `DuplicateDimensionValueException` on conflict → controller maps to HTTP 409.
- `DeactivateDimensionValueHandler` soft-deletes (sets `IsActive=false`) — returns `false` for not-found → controller maps to HTTP 404. Note: an already-inactive record is invisible via the global query filter, so DELETE on it correctly returns 404 (not a double-delete scenario).
- `TenantDimensionsController` manually parses `{axis}` string via `Enum.TryParse` (case-insensitive) — ASP.NET Core does not auto-bind enum route parameters. Returns 400 with `{ "error": "invalid_axis" }` for all endpoints on invalid axis.
- Audit events logged: `dimension_value.created`, `dimension_value.deactivated` with axis+value payload.
- `DimensionValueIsolationRegressionTests` added to `TenantIsolationRegressionTests.cs` with 2 tests (not-visible-from-other-tenant, visible-from-owning-tenant).
- `TenantDimensionsIntegrationTests` created with 12 tests covering all ACs. Integration tests require Docker (Testcontainers).
- AR-8 (boundary): all handlers in `Application.TenantAdmin.Dimensions.*` namespace.
- AR-15 (skip cap): no new test skips; skip count remains at 1.

### File List

**New files:**
- src/OneId.Server/Domain/Enums/DimensionAxis.cs
- src/OneId.Server/Domain/Entities/DimensionValue.cs
- src/OneId.Server/Infrastructure/Persistence/Configurations/DimensionValueConfiguration.cs
- src/OneId.Server/Infrastructure/Persistence/Migrations/20260526211044_AddDimensionValues.cs (generated)
- src/OneId.Server/Infrastructure/Persistence/Migrations/20260526211044_AddDimensionValues.Designer.cs (generated)
- src/OneId.Server/Application/TenantAdmin/Dimensions/DimensionValueDto.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/Queries/ListDimensionValuesHandler.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/AddDimensionValueHandler.cs
- src/OneId.Server/Application/TenantAdmin/Dimensions/Commands/DeactivateDimensionValueHandler.cs
- src/OneId.Server/Controllers/TenantDimensionsController.cs
- tests/OneId.Server.IntegrationTests/TenantDimensionsIntegrationTests.cs

**Modified files:**
- src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs — added `DbSet<DimensionValue>`, global query filter
- src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs — auto-updated by EF tooling
- src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs — added 3 handler registrations + 2 using statements
- tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs — added DimensionValueIsolationRegressionTests class

## Change Log

- 2026-05-27: Story 4a-5 implemented — DimensionValue entity + EF migration, 3 handlers, TenantDimensionsController, 12 integration tests, 2 isolation regression tests. (Dev Agent)
