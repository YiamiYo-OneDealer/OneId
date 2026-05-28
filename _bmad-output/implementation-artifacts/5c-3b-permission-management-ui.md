# Story 5c.3b: Permission Management UI (Add / Edit Label / Toggle Status)

Status: done

## Story

As an Internal Admin,
I want to add new permissions, edit their labels, and toggle them active or inactive directly from the permissions page,
so that OneId is the authoritative source of truth for the permission catalog and I can manage it without a code deployment.

## Context

Story 4a-1 built the full backend for permission management and story 5c-3 wired up the read-only permissions list at `/internal/permissions`. This story completes the loop by adding the mutation UI and closing the one remaining backend gap (reactivation).

**Backend already done (4a-1):**
- `POST   /api/internal/permissions`              — create
- `PATCH  /api/internal/permissions/{permissionId}` — update label only (body: `{ label, version }`)
- `DELETE /api/internal/permissions/{permissionId}` — deactivate (soft-delete to Inactive)
- `GET    /api/internal/permissions`              — list with `?status=Active|Inactive|All&page&pageSize`

**Backend gap (this story):**
- `POST /api/internal/permissions/{permissionId}/activate` — reactivate an Inactive permission

**Frontend currently:**
- `src/routes/internal/permissions.tsx` — read-only table (Permission ID, Domain, Label, Status badge)
- `src/queries/hooks/usePermissions.ts` — `usePermissions()` read hook only
- `src/api/types.ts` — `PermissionDto` exists; no mutation body types for permissions

## Acceptance Criteria

**AC1 — Add Permission:**
Given an Internal Admin is on `/internal/permissions`
When they click "Add Permission" and submit a valid `permissionId` (dot-notation, e.g. `od.crm.quotes.approve`) and `label`
Then a new permission is created via `POST /api/internal/permissions`
And the table refreshes to show the new row with status Active
And a duplicate `permissionId` shows an inline error "Permission ID already exists" without closing the dialog

**AC2 — permissionId validation:**
Given the Add Permission dialog is open
When the user types a `permissionId` that does not match `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$` (at least 3 dot-separated lowercase segments)
Then a field-level error "Must be dot-notation with at least 3 segments, e.g. od.crm.feature.action" is shown
And the Submit button remains disabled until the field is valid

**AC3 — Edit Label:**
Given a permission row is visible in the table
When the Internal Admin clicks "Edit" on that row
Then an edit dialog opens pre-filled with the current label
When they save a non-empty label
Then `PATCH /api/internal/permissions/{permissionId}` is called with `{ label, version }`
And the row updates in place
And a stale-version 409 shows "Someone else edited this permission — reload and try again" without losing the user's input

**AC4 — Deactivate:**
Given an Active permission row
When the Internal Admin clicks "Deactivate"
Then a confirmation dialog asks "Deactivate `{permissionId}`? It will no longer be included in token claims."
When confirmed, `DELETE /api/internal/permissions/{permissionId}` is called
And the row's status badge changes to Inactive
And the Deactivate button changes to Activate

**AC5 — Reactivate:**
Given an Inactive permission row is visible (see AC6)
When the Internal Admin clicks "Activate"
Then `POST /api/internal/permissions/{permissionId}/activate` is called
And the row's status badge changes to Active
And the button changes back to Deactivate

**AC6 — Show Inactive permissions:**
Given the permissions page loads
When it fetches the list
Then both Active and Inactive permissions are shown (fetch without status filter, or `?status=All`)
And Inactive rows display a "Inactive" badge (secondary variant) so admins can see and reactivate them

**AC7 — No Delete:**
There is no Delete button. Deactivate is the only removal action. The confirmation dialog must make this explicit ("The record is retained for audit purposes").

**AC8 — Auth enforcement:**
Given any of the four mutation endpoints are called without the `InternalAdmin` role
Then the response is HTTP 403 — enforced by the existing controller attribute, no new work needed

## Tasks / Subtasks

- [x] **Task 1 — Backend: Reactivate endpoint** (AC5)
  - [x] Add `ReactivatePermissionHandler` in `Application/Internal/Permissions/Commands/` following the CQRS pattern of `DeactivatePermissionHandler` — sets `Status = Active`, writes audit event `permission.reactivated`
  - [x] Add `POST /api/internal/permissions/{permissionId}/activate` action to `InternalPermissionsController.cs` — returns 204 on success, 404 if not found
  - [x] Add integration test in `PermissionsIntegrationTests.cs`: deactivate then reactivate → status returns to Active

- [x] **Task 2 — API types** (AC1, AC3, AC5)
  - [x] Add to `src/OneId.Web/src/api/types.ts`:
    ```typescript
    export interface CreatePermissionBody {
      permissionId: string
      label: string
    }
    export interface UpdatePermissionBody {
      label: string
      version: number
    }
    ```
  - [x] Note: the backend `version` field is `uint` but arrives as a JSON number — `number` is correct on the frontend

- [x] **Task 3 — Query hooks** (AC1, AC3, AC4, AC5)
  - [x] Add to `src/OneId.Web/src/queries/hooks/usePermissions.ts`:
    - `useCreatePermission()` — `POST api/internal/permissions`, invalidates `permissions()` query key on success
    - `useUpdatePermission()` — `PATCH api/internal/permissions/{permissionId}`, invalidates on success
    - `useDeactivatePermission()` — `DELETE api/internal/permissions/{permissionId}`, invalidates on success
    - `useActivatePermission()` — `POST api/internal/permissions/{permissionId}/activate`, invalidates on success
  - [x] Export all four from `src/OneId.Web/src/queries/hooks/index.ts`
  - [x] Update `usePermissions()` to fetch all statuses: `status=All` — `ListPermissionsHandler` already handles any non-"active"/non-"inactive" string as no-filter (default case)

- [x] **Task 4 — Add Permission dialog** (AC1, AC2)
  - [x] In `permissions.tsx`, add `AddPermissionDialog` component (inline in the file, same pattern as other dialogs in the codebase):
    - Fields: `permissionId` (text, validated on blur and submit), `label` (text, required)
    - `permissionId` regex: `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$` — validate client-side, show field error
    - On submit call `useCreatePermission().mutate(...)` 
    - On 409 response show inline "Permission ID already exists" without closing
    - On success close and reset form; table auto-refreshes via query invalidation
  - [x] Add "Add Permission" `<Button size="sm">` in the page header alongside the title (same layout as Groups/Roles pages)

- [x] **Task 5 — Edit Label dialog** (AC3)
  - [x] Add `EditPermissionDialog` component in `permissions.tsx`:
    - Single field: `label` (pre-filled, required, non-empty)
    - Tracks `version` from the row's `PermissionDto.version` — pass it through to PATCH
    - On 409 (stale version) show inline "Someone else edited this — reload and try again" without closing
    - On success close; table auto-refreshes
  - [x] Add "Edit" `<Button variant="outline" size="sm">` in the actions column — opens `EditPermissionDialog` with the row's current data

- [x] **Task 6 — Deactivate / Activate toggle** (AC4, AC5, AC6, AC7)
  - [x] Add `DeactivateDialog` component in `permissions.tsx` (or reuse the existing `_delete-dialog` pattern with custom message):
    - Title: "Deactivate `{permissionId}`?"
    - Body: "This permission will no longer be included in token claims. The record is retained for audit purposes."
    - Confirm button: "Deactivate" (destructive variant)
  - [x] In the actions column, render conditionally:
    - If `status === 'Active'`: show "Deactivate" button → opens `DeactivateDialog`
    - If `status === 'Inactive'`: show "Activate" button (default variant) → calls `useActivatePermission()` directly (no confirmation needed for reactivation)
  - [x] Update `usePermissions()` so the list includes Inactive rows (AC6) — Inactive rows must be visible for the Activate button to be reachable

- [x] **Task 7 — Column layout update** (AC6)
  - [x] Ensure the Status column badge renders correctly for both "Active" (`default` variant) and "Inactive" (`secondary` variant) — already done in the current page, verify it still works with Inactive rows visible
  - [x] Actions column must contain three buttons: Edit | Deactivate (or Activate) — use `flex gap-2 justify-end` layout matching other pages

## Dev Notes

### Backend Gap — Reactivate Handler

The existing `DeactivatePermissionHandler` lives at:
```
src/OneId.Server/Application/Internal/Permissions/Commands/DeactivatePermissionHandler.cs
```
Mirror its structure for `ReactivatePermissionHandler` — same file location pattern, same audit logging convention (`permission.reactivated`), same `InternalAdminContext` boundary (AR-8). The handler simply sets `Status = PermissionStatus.Active`.

Controller action pattern from existing code:
```csharp
[HttpPost("{permissionId}/activate")]
public async Task<IActionResult> Activate(string permissionId, CancellationToken ct)
{
    var found = await activateHandler.HandleAsync(permissionId, ct);
    return found ? NoContent() : NotFound();
}
```

### `status=All` in ListPermissionsHandler

`ListPermissionsHandler` currently accepts a `status` string and filters. Check whether it supports `"All"` or similar to skip the filter. If not, add it — it's a one-liner in the handler. Alternatively the frontend can simply omit the status param and let the backend default to returning all (requires a handler change to default to no filter when status is not supplied or is `"All"`).

Whichever approach: do NOT make two separate API calls and merge on the frontend. One call, all records.

### Frontend: Hook File Location

All hooks go in `src/OneId.Web/src/queries/hooks/usePermissions.ts`. Mutations follow the same pattern as `useUpdateTenant`, `useDeleteGroup`, etc. in sibling files.

### Frontend: `version` field

`PermissionDto.version` is the xmin concurrency token (PostgreSQL `uint` serialized as JSON number). Pass it unchanged to the PATCH body. Never recompute or modify it — just round-trip what you received from the last GET.

### Frontend: 409 handling in mutation hooks

The `apiClient` (ky) throws on non-2xx responses. Catch HTTP 409 in the `onError` callback of the mutation and surface the appropriate message based on the error body `{ error: "permission_id_taken" }` or `{ error: "conflict" }`.

### Frontend: Dialog pattern

Follow the same dialog structure used in `TenantGroupsPage.tsx` and `TenantRolesPage.tsx`:
- State: `const [createOpen, setCreateOpen] = useState(false)` / `const [editTarget, setEditTarget] = useState<PermissionDto | null>(null)`
- Import from `@/components/ui/dialog`: `Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle`
- No `DialogDescription` needed unless instructed by design

### Frontend: permissionId regex

```typescript
const PERMISSION_ID_RE = /^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$/
```
Validate on blur and on submit. Show the field error beneath the input using the `<p className="text-sm text-destructive">` pattern.

### Architecture Constraints (must follow)

- All permission IDs in application code reference `Permissions.cs` constants — this story does NOT introduce new constants (new permissions are created at runtime via the UI)
- `InternalAdminContext` boundary: `ReactivatePermissionHandler` must live under `Application/Internal/Permissions/Commands/` (AR-8, enforced by ArchUnit)
- Audit log written in same transaction as mutation (AR-9) — `AuditLogService.AppendAsync` in the handler
- Problem Details for API errors — controller uses `BadRequest(new { error = "..." })` pattern already established in the file
- `UseXminAsConcurrencyToken()` already applied to `Permission` entity — no entity configuration changes needed

### Files to Create

**Backend:**
- `src/OneId.Server/Application/Internal/Permissions/Commands/ReactivatePermissionHandler.cs`

**Frontend:**
- No new files — all changes are in existing files

### Files to Modify

**Backend:**
- `src/OneId.Server/Controllers/InternalPermissionsController.cs` — add `Activate` action + inject `ReactivatePermissionHandler`
- `src/OneId.Server/Application/Internal/Permissions/Queries/ListPermissionsHandler.cs` — support `status=All` (or no-filter default)
- `tests/OneId.Server.IntegrationTests/` — extend `PermissionsIntegrationTests.cs` (or equivalent) with reactivate test

**Frontend:**
- `src/OneId.Web/src/api/types.ts` — add `CreatePermissionBody`, `UpdatePermissionBody`
- `src/OneId.Web/src/queries/hooks/usePermissions.ts` — add four mutation hooks, update list to fetch all statuses
- `src/OneId.Web/src/queries/hooks/index.ts` — re-export new hooks
- `src/OneId.Web/src/routes/internal/permissions.tsx` — add dialogs, buttons, updated column layout

### Project Structure Notes

- Backend controller: `src/OneId.Server/Controllers/InternalPermissionsController.cs` (not under `Controllers/Internal/` — the architecture diagram shows it at root Controllers level, confirmed by reading the file)
- Frontend route: `src/OneId.Web/src/routes/internal/permissions.tsx`
- Hook: `src/OneId.Web/src/queries/hooks/usePermissions.ts`

### References

- Backend controller: `src/OneId.Server/Controllers/InternalPermissionsController.cs`
- Deactivate handler pattern: `src/OneId.Server/Application/Internal/Permissions/Commands/DeactivatePermissionHandler.cs`
- Permission entity: `src/OneId.Server/Domain/Entities/Permission.cs`
- Permission status enum: `src/OneId.Server/Domain/Enums/PermissionStatus.cs`
- Frontend permissions page: `src/OneId.Web/src/routes/internal/permissions.tsx`
- Hook file: `src/OneId.Web/src/queries/hooks/usePermissions.ts`
- API types: `src/OneId.Web/src/api/types.ts`
- Dialog pattern reference: `src/OneId.Web/src/routes/internal/tenants/TenantGroupsPage.tsx`
- Architecture API patterns: `_bmad-output/planning-artifacts/architecture.md` — REST conventions, InternalAdmin boundary
- Story 4a-1 (backend foundation): `_bmad-output/implementation-artifacts/4a-1-permission-catalog-internal-admin.md`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- `ListPermissionsHandler` already handles `status=All` via the default `_` arm of its switch — no backend change needed to support fetching all statuses.
- `index.ts` exports all hooks via `export * from './usePermissions'` so no explicit re-export update required.
- Pre-existing TypeScript errors in `EffectivePermissions.tsx`, `useTenants.test.ts`, and `TenantDetailPage.tsx` are unrelated to this story and were present before implementation.
- All 13 backend integration tests pass (including 2 new reactivate tests: deactivate→reactivate→Active and 404 for non-existent).

### File List

**Backend (created):**
- `src/OneId.Server/Application/Internal/Permissions/Commands/ReactivatePermissionHandler.cs`

**Backend (modified):**
- `src/OneId.Server/Controllers/InternalPermissionsController.cs`
- `src/OneId.Server/Application/Internal/InternalServiceExtensions.cs`
- `tests/OneId.Server.IntegrationTests/InternalPermissionsIntegrationTests.cs`

**Frontend (modified):**
- `src/OneId.Web/src/api/types.ts`
- `src/OneId.Web/src/queries/hooks/usePermissions.ts`
- `src/OneId.Web/src/routes/internal/permissions.tsx`

### Change Log

- 2026-05-28: Implemented Story 5c-3b — Permission Management UI. Added `ReactivatePermissionHandler` (backend), `POST /activate` endpoint, 4 frontend mutation hooks, `AddPermissionDialog`, `EditPermissionDialog`, `DeactivateDialog`, and action buttons with Activate/Deactivate toggle. Permissions list now fetches all statuses via `status=All`.

### Review Findings

- [x] [Review][Patch] Guid.Empty used as actor ID in audit log — DISMISSED: Guid.Empty is TenantId (internal admin = no tenant scope); ActorUserId auto-resolved from JWT sub by AuditService
- [x] [Review][Patch] Reactivating an already-Active permission: no status guard, silently stamps UpdatedAt and emits spurious audit entry [ReactivatePermissionHandler.cs:20]
- [x] [Review][Patch] All Activate buttons share one isPending flag — any in-flight activation disables every other row's Activate button [permissions.tsx]
- [x] [Review][Patch] Activate button has no onError callback — failures are completely silent to the user [permissions.tsx]
- [x] [Review][Patch] EditPermissionDialog label state initialised at mount only — stale label shown when switching edit targets [permissions.tsx]
- [x] [Review][Patch] EditPermissionDialog closes without isPending guard, unlike DeactivateDialog — allows dialog dismissal mid-PATCH [permissions.tsx]
- [x] [Review][Patch] columns array redefined inside PermissionsPage on every render — new reference on every state change [permissions.tsx]
- [x] [Review][Defer] UpdateRoleSetBody.version typed as optional (?: number) while all other update bodies require it [api/types.ts] — deferred, pre-existing in Fixes commit; affects RoleSet entity, out of this story's scope
- [x] [Review][Defer] pageSize: 200 hardcoded in usePermissions — catalogs > 200 silently truncate with no warning [usePermissions.ts] — deferred, requires API-level pagination; not fixable within this story
