# Story 4b.1: User-Level Permission Override Data Model

**Status:** done
**Epic:** 4b — Token Evaluation & Overrides
**Story ID:** 4b-1
**Prerequisite:** Epic 4a complete ✓ (Permission catalog, Roles, RoleSets, Groups, Dimensions all stable)

---

## Story

As a Tenant Admin,
I want to set ALLOW or DENY overrides on individual Permissions for specific Users,
So that I can handle exceptions to the standard Group-based authorization without restructuring Roles.

---

## Acceptance Criteria

1. **Given** the `UserPermissionOverride` table is introduced via EF Core migration
   **When** the migration runs
   **Then** the table has columns: `Id`, `TenantId`, `UserId`, `PermissionId`, `OverrideType` (enum: `Allow`, `Deny`), `Reason` (required string), `ExpiresAt` (nullable datetime), `CreatedAt`, `CreatedByUserId`
   **And** a unique constraint exists on `(TenantId, UserId, PermissionId)` — one override per permission per user per tenant
   **And** `UseXminAsConcurrencyToken()` is applied to the `UserPermissionOverride` entity

2. **Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/overrides`
   **When** the request body contains `permissionId`, `overrideType`, `reason`, and optional `expiresAt`
   **Then** a `UserPermissionOverride` record is created
   **And** `permissionId` must reference an active permission in the global catalog — inactive or non-existent permission IDs return HTTP 422
   **And** `reason` is required — an empty or missing reason returns HTTP 422

3. **Given** a Tenant Admin calls `GET /api/tenant/users/{userId}/overrides`
   **When** the request is processed
   **Then** all override records for the user are returned — including expired ones (expired records are retained for audit trail, AR-11)
   **And** each record includes an `isExpired` boolean computed from `ExpiresAt` vs current UTC time

4. **Given** a Tenant Admin calls `DELETE /api/tenant/users/{userId}/overrides/{overrideId}`
   **When** the request is processed
   **Then** the override record is physically deleted
   **And** `TenantIsolationRegressionTests.cs` is extended: `UserPermissionOverride` records for User A in Tenant A are NOT visible under Tenant B's context

5. **Given** the override read path queries `UserPermissionOverride`
   **When** filtering active overrides for evaluation
   **Then** the EF Core query applies `WHERE ExpiresAt IS NULL OR ExpiresAt > NOW()` — expired records are automatically excluded from evaluation at read time with no background sweeper (AR-11)

---

## Tasks / Subtasks

- [x] **Task 1: Add `PermissionOverrideType` enum** (AC: 1)
  - [ ] Create `src/OneId.Server/Domain/Enums/PermissionOverrideType.cs` with values `Allow = 0`, `Deny = 1`

- [x] **Task 2: Create `UserPermissionOverride` entity** (AC: 1)
  - [ ] Create `src/OneId.Server/Domain/Entities/UserPermissionOverride.cs`
  - [ ] Fields: `Guid Id`, `Guid TenantId`, `Guid UserId`, `string PermissionId`, `PermissionOverrideType OverrideType`, `string Reason`, `DateTimeOffset? ExpiresAt`, `DateTimeOffset CreatedAt`, `Guid CreatedByUserId`
  - [ ] Note: `PermissionId` is a **string** (matching `Permission.PermissionId` e.g. `"od.crm.read"`), not a Guid FK to `Permission.Id`

- [x] **Task 3: Create `UserPermissionOverrideConfiguration`** (AC: 1)
  - [ ] Create `src/OneId.Server/Infrastructure/Persistence/Configurations/UserPermissionOverrideConfiguration.cs`
  - [ ] Implement `IEntityTypeConfiguration<UserPermissionOverride>`
  - [ ] Apply unique index on `(TenantId, UserId, PermissionId)`
  - [ ] `Reason` column: `.IsRequired().HasMaxLength(500)`
  - [ ] `UseXminAsConcurrencyToken()` on the entity
  - [ ] Do NOT add `deleted_at` — overrides are physically deleted (AC: 4)

- [x] **Task 4: Register `UserPermissionOverride` in `AppDbContext`** (AC: 1)
  - [ ] Add `public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();` to `AppDbContext.cs`
  - [ ] Add tenant isolation query filter: `builder.Entity<UserPermissionOverride>().HasQueryFilter(o => o.TenantId == tenantContext.TenantId);`
  - [ ] Add comment: `// Story 4b.1: UserPermissionOverride tenant isolation`

- [x] **Task 5: Run EF Core migration** (AC: 1)
  - [ ] `dotnet ef migrations add AddUserPermissionOverride --project src/OneId.Server`
  - [ ] Verify generated migration adds table, unique index, and no soft-delete column

- [x] **Task 6: Create `UserOverrideDto`** (AC: 2, 3)
  - [ ] Create `src/OneId.Server/Application/TenantAdmin/UserOverrides/UserOverrideDto.cs`
  - [ ] Fields: `Guid Id`, `string PermissionId`, `string OverrideType` (`"Allow"`/`"Deny"`), `string Reason`, `DateTimeOffset? ExpiresAt`, `DateTimeOffset CreatedAt`, `bool IsExpired`
  - [ ] `IsExpired` is computed: `ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow`

- [x] **Task 7: Create query handler `ListUserOverridesHandler`** (AC: 3)
  - [ ] Create `src/OneId.Server/Application/TenantAdmin/UserOverrides/Queries/ListUserOverridesHandler.cs`
  - [ ] Returns ALL overrides for `userId` (including expired) — do NOT filter by expiry here; expiry is shown via `isExpired` flag
  - [ ] `userId` must belong to caller's tenant (enforced by global query filter on `User` lookup — return 404 if not found)

- [x] **Task 8: Create command handlers** (AC: 2, 4)
  - [ ] Create `src/OneId.Server/Application/TenantAdmin/UserOverrides/Commands/CreateUserOverrideHandler.cs`
    - [ ] Validate `permissionId` references an **active** `Permission` record (`Status == PermissionStatus.Active`) — return HTTP 422 if not
    - [ ] Validate `reason` is non-empty — return HTTP 422 if missing/empty
    - [ ] On duplicate `(TenantId, UserId, PermissionId)` — return HTTP 409
    - [ ] Audit: `IAuditService.AppendAsync` with `Action: "user_override.created"`
  - [ ] Create `src/OneId.Server/Application/TenantAdmin/UserOverrides/Commands/DeleteUserOverrideHandler.cs`
    - [ ] Physical delete (no soft-delete)
    - [ ] 404 if `overrideId` not found or belongs to different tenant
    - [ ] Audit: `IAuditService.AppendAsync` with `Action: "user_override.deleted"`

- [x] **Task 9: Create `TenantUserOverridesController`** (AC: 2, 3, 4)
  - [ ] Create `src/OneId.Server/Controllers/TenantUserOverridesController.cs`
  - [ ] Route: `api/tenant/users/{userId}/overrides`
  - [ ] `[Authorize(Roles = "TenantAdmin")]` on controller
  - [ ] `POST /` → 201 with `UserOverrideDto`
  - [ ] `GET /` → 200 with `IEnumerable<UserOverrideDto>` (no pagination needed — override lists will be small)
  - [ ] `DELETE /{overrideId}` → 204

- [x] **Task 10: Register handlers in DI** (AC: 2–4)
  - [ ] Update `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs`
  - [ ] Add: `services.AddScoped<ListUserOverridesHandler>();`, `services.AddScoped<CreateUserOverrideHandler>();`, `services.AddScoped<DeleteUserOverrideHandler>();`

- [x] **Task 11: Integration tests** (AC: 2–5)
  - [ ] Create `tests/OneId.Server.IntegrationTests/UserOverrideIntegrationTests.cs`
  - [ ] Test: `POST` creates record → `GET` returns it with `isExpired=false`
  - [ ] Test: `POST` with inactive permission ID → HTTP 422
  - [ ] Test: `POST` with empty reason → HTTP 422
  - [ ] Test: Duplicate `POST` same `(userId, permissionId)` → HTTP 409
  - [ ] Test: `POST` with `expiresAt` in the past → record created → `GET` returns `isExpired=true`
  - [ ] Test: `DELETE` → 204 → `GET` returns empty list
  - [ ] Test: All endpoints return 403 without TenantAdmin role

- [x] **Task 12: Extend `TenantIsolationRegressionTests`** (AC: 4)
  - [ ] In `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs`, add test:
    `UserPermissionOverride_IsNotVisible_FromOtherTenant`
  - [ ] Seed a `UserPermissionOverride` under Tenant A, verify it is not returned via `GET /api/tenant/users/{userId}/overrides` under Tenant B context

---

## Dev Notes

### Entity: `UserPermissionOverride`

```csharp
// src/OneId.Server/Domain/Entities/UserPermissionOverride.cs
namespace OneId.Server.Domain.Entities;

public class UserPermissionOverride
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public required string PermissionId { get; set; }   // e.g. "od.crm.read" — string, not FK Guid
    public PermissionOverrideType OverrideType { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
```

### Why `PermissionId` is a string, not a Guid FK

`Permission.PermissionId` (string like `"od.crm.read"`) is the stable identity used throughout the system — `Permission.Id` (Guid) is the internal DB row key. The override stores the string ID so the evaluation pipeline can do a simple `WHERE PermissionId IN (...)` against the override table without a join. This is consistent with how `RolePermission` is structured.

### No `deleted_at` on this entity

`UserPermissionOverride` uses physical delete (AC: 4). The audit trail is preserved via `AuditLog` entries, not soft-deletion. This is intentional and differs from other tenant-scoped entities. Do NOT add `deleted_at`.

### EF Core configuration pattern (follow existing examples)

Look at `src/OneId.Server/Infrastructure/Persistence/Configurations/UserDimensionAssignmentConfiguration.cs` for a recent junction/override entity configuration — same pattern applies here.

### Expiry enforcement

- **Read path for UI** (`ListUserOverridesHandler`): Return ALL records, compute `isExpired` in DTO — expired records are shown to the admin (audit trail).
- **Read path for evaluation** (Story 4b-2, `IPermissionEvaluator`): Apply `WHERE ExpiresAt IS NULL OR ExpiresAt > NOW()` filter. This story only needs to ensure the data model supports it — actual evaluation is implemented in 4b-2.

### AppDbContext changes

Add the DbSet and query filter following the existing pattern. The `OnModelCreating` comment block already has a placeholder note for Story 4b.1 (`// Epic 4b adds: UserPermissionOverride`).

### Existing patterns to follow

- Controller: mirror `TenantUsersController.cs` structure — same auth pattern, same Problem Details error shapes
- Handler: mirror `CreateUserHandler.cs` — `ITenantContext` injected, no `TenantId` as method param
- Audit: call `IAuditService.AppendAsync` in same transaction as DB write (see `CreateUserHandler.cs` for example)
- 422 validation: use FluentValidation inline or return `TypedResults.UnprocessableEntity(new { error = "..." })`

---

## Completion Note

Story ready-for-dev. All dependent entities (Permission, User, Group, etc.) are stable from Epic 4a. No external API calls or new npm packages required. This is purely a backend data model + CRUD story.

---

## File List

**New files:**
- `src/OneId.Server/Domain/Enums/PermissionOverrideType.cs`
- `src/OneId.Server/Domain/Entities/UserPermissionOverride.cs`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserPermissionOverrideConfiguration.cs`
- `src/OneId.Server/Application/TenantAdmin/UserOverrides/UserOverrideDto.cs`
- `src/OneId.Server/Application/TenantAdmin/UserOverrides/Queries/ListUserOverridesHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/UserOverrides/Commands/CreateUserOverrideHandler.cs`
- `src/OneId.Server/Application/TenantAdmin/UserOverrides/Commands/DeleteUserOverrideHandler.cs`
- `src/OneId.Server/Controllers/TenantUserOverridesController.cs`
- `tests/OneId.Server.IntegrationTests/UserOverrideIntegrationTests.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/<timestamp>_AddUserPermissionOverride.cs` (generated)

**Modified files:**
- `src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs`
- `src/OneId.Server/Application/TenantAdmin/TenantServiceExtensions.cs`
- `tests/OneId.Server.IntegrationTests/TenantIsolationRegressionTests.cs`

---

## Change Log

- **2026-05-27:** Implemented story 4b-1 — UserPermissionOverride data model, CRUD API, 10 integration tests (9 new + 1 isolation), EF migration. All 10 new tests pass; 0 regressions introduced.

---

## Review Findings

- [x] [Review][Defer] **F01: ExpiresAt in past accepted at creation** — intentional; permissive by design to support testing and backdating scenarios; document in dev notes — No validation that `request.ExpiresAt > UtcNow`. A caller can create an immediately-expired (always-inert) override with no error. The `ExpiredDenyOverrideIntegrationTest` deliberately does this for testing. Decision: should we reject past ExpiresAt with 422, or keep it permissive for test/backdating purposes? [`TenantUserOverridesController.cs`, `CreateUserOverrideHandler.cs:49`]
- [x] [Review][Patch] **F04: `CreatedByUserId` hardcoded as `Guid.Empty`** — Actor identity is lost from every audit record. `actorSub` is assigned `tenantContext.GetType().Name` (a class name string) and never used. Should be resolved from the calling user's JWT `sub` claim via `ITenantContext` or `IHttpContextAccessor`. [`CreateUserOverrideHandler.cs:201,213`]
- [x] [Review][Patch] **F05: Concurrent POST race — `DbUpdateException` not caught → 500** — Two simultaneous POSTs for the same `(userId, permissionId)` both pass the `AnyAsync` duplicate check, then the second hits the DB unique index and throws `DbUpdateException`. Not caught → 500. Should catch and return 409. [`CreateUserOverrideHandler.cs:34-36`, `TenantUserOverridesController.cs`]
- [x] [Review][Patch] **F06: `CreateOverrideBody.Reason` typed `string?` with null-forgiving `!` downstream** — `Reason` is declared nullable in the request record but the `CreateUserOverrideRequest` propagates it with `body.Reason!`. Change `Reason` to `string` + `[Required]` so model binding rejects a missing field with 400 rather than relying solely on the manual null check. [`TenantUserOverridesController.cs:404`]
- [x] [Review][Defer] **F10: No FK constraints on `UserPermissionOverride.UserId` / `PermissionId`** [`20260527104530_AddUserPermissionOverride.cs`] — deferred, intentional design (matches other tenant-scoped entities; PermissionId is a string reference not a Guid FK)
- [x] [Review][Defer] **F13: Soft-deleted users — overrides still evaluated at introspection time** [`PermissionEvaluator.cs`] — deferred, out of scope for epic 4b; user lifecycle and token revocation is a separate concern
- [x] [Review][Defer] **F15: Permission ID case sensitivity — duplicate overrides possible with differently-cased IDs** [`UserPermissionOverrideConfiguration.cs`] — deferred, pre-existing design characteristic of the permission catalog; canonical casing is enforced at seeding time

---

## Dev Agent Record

**Implementation notes:**
- `PermissionOverrideType` enum: `Allow = 0`, `Deny = 1`
- Entity uses physical delete (no `deleted_at`) — audit trail via `AuditLog`
- `PermissionId` stored as string (not FK Guid) to match `Permission.PermissionId` stable ID; enables direct lookup in evaluation pipeline without join
- xmin concurrency token registered via shadow property in configuration (Npgsql v10 pattern, same as `UserDimensionAssignmentConfiguration`)
- `Reason` validated via `string.IsNullOrWhiteSpace` in controller (not `[Required]` attribute) to return 422 rather than 400 from model binding
- All 10 new integration tests pass; pre-existing 6 failures unchanged (pre-existing, unrelated to this story)
