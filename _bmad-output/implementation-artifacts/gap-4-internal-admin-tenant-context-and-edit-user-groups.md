# Story gap-4: Internal Admin Tenant Context Forwarding + Complete EditUserDialog

**Status:** review
**Epic:** Phase 8 completion
**Story ID:** gap-4
**Prerequisite:** gap-3 complete ✓ — `useUserGroups`, `useAddGroupMember`, `useRemoveGroupMember` hooks exist and are exported from the barrel.

---

## Story

As an Internal Admin,
I want the tenant management pages (Users, Groups, Roles, Role Sets) to call the real API scoped to the selected tenant,
and I want to be able to edit a user's group memberships from the Edit User dialog,
so that I can fully administer any tenant's authorization structure without going to the database.

---

## Context

Two stacked problems prevent Internal Admin tenant management from working end-to-end:

**Problem 1 — Backend: Internal Admin JWT has no `tid` claim**

All tenant-scoped controllers (`TenantUsersController`, `TenantGroupsController`, `TenantRolesController`, `TenantRoleSetsController`) require `Roles = "TenantAdmin"` and rely on `TenantContext` being initialized from the JWT `tid` claim by `TenantContextMiddleware`. An Internal Admin's JWT:
- Has no `tid` claim → `TenantContext.TenantId` throws `InvalidOperationException` → 500 on every tenant-scoped call
- Has `InternalAdmin` role, not `TenantAdmin` → 403 from controller authorization

The `TenantContextLayout` (at `/internal/tenants/:tenantId/`) already stores the selected tenant ID in the Zustand `tenantStore.activeTenantId`. This value just isn't forwarded to the backend.

**Problem 2 — Frontend: EditUserDialog is missing group assignment**

The `EditUserDialog` in `TenantUsersPage.tsx` (lines 190–265) only patches `displayName` and `email`. The `GroupSelectList` component is defined in the same file but was never wired into the edit dialog. The story spec (5c-1c) required groups + status toggle; the implementation dropped both.

---

## Acceptance Criteria

### AC1 — `TenantContextMiddleware` accepts `X-Tenant-Id` for Internal Admin

**Given** an Internal Admin sends a request to any `api/tenant/...` endpoint
**When** the request includes an `X-Tenant-Id` header with a valid non-empty GUID
**And** the caller's JWT has the `InternalAdmin` role
**Then** `TenantContext` is initialized from the header value (not the missing `tid` claim)
**And** subsequent EF queries see the correct tenant isolation
**And** if the `X-Tenant-Id` header is absent or invalid for an `InternalAdmin` caller, `TenantContext` is NOT initialized (endpoint proceeds but handlers will throw if they access `TenantContext.TenantId` — same behavior as today for an unauthenticated call to a tenant endpoint)
**And** the existing path for Tenant Admin (JWT `tid` claim) is UNCHANGED

### AC2 — Tenant controllers accept `InternalAdmin` role

**Given** any of the four tenant CRUD controllers
**When** the caller has `InternalAdmin` role (regardless of `TenantAdmin` role)
**Then** the request passes authorization
**Affected controllers:**
- `TenantUsersController` — `Roles = "TenantAdmin,InternalAdmin"`
- `TenantGroupsController` — `Roles = "TenantAdmin,InternalAdmin"`
- `TenantRolesController` — `Roles = "TenantAdmin,InternalAdmin"`
- `TenantRoleSetsController` — `Roles = "TenantAdmin,InternalAdmin"`

### AC3 — `apiClient` forwards active tenant ID as header

**Given** the Zustand `tenantStore.activeTenantId` is non-null (set by `TenantContextLayout` when Internal Admin navigates to `/internal/tenants/:tenantId/`)
**When** `apiClient` makes any request
**Then** the request includes `X-Tenant-Id: {activeTenantId}` header
**And** when `activeTenantId` is `null` (Tenant Admin flow — store is never set), no `X-Tenant-Id` header is sent
**And** Tenant Admin API calls continue to work unchanged (they use JWT `tid` claim as before)

### AC4 — Internal Admin can list and CRUD groups, roles, role sets

**Given** AC1–AC3 are implemented
**When** an Internal Admin navigates to `/internal/tenants/:tenantId/groups` (or `/roles`, `/role-sets`)
**Then** the page shows real data from the backend for the selected tenant
**And** Create / Edit / Delete operations in the dialogs (from story 5c-1b) persist to the real backend
**And** creating a group with roles/role sets assigned works end-to-end

### AC5 — EditUserDialog includes group assignment

**Given** `TenantUsersPage.tsx` → `EditUserDialog`
**When** an Internal Admin clicks "Edit" on a user row
**Then** the dialog shows three fields: Display Name, Email, and Groups
**And** the Groups field uses `GroupSelectList` (already in the file) pre-populated with all tenant groups
**And** the user's current group memberships are pre-checked (loaded via `useUserGroups(tenantId, user.id)`)
**And** while current groups are loading, the Groups section shows a "Loading…" text and the Save button is disabled

### AC6 — EditUserDialog saves group changes

**Given** an Internal Admin edits a user and changes group selections
**When** they click "Save"
**Then** `PATCH /api/tenant/users/{userId}` is called if displayName or email changed
**And** for each newly selected group: `PUT /api/tenant/groups/{groupId}/members` is called with `{ userId }`
**And** for each de-selected group: `DELETE /api/tenant/groups/{groupId}/members/{userId}` is called
**And** add/remove calls use `Promise.allSettled` — partial failures show "Changes saved but some group assignments failed. Please check and retry." without closing the dialog
**And** on full success the dialog closes and the user list refetches
**And** the Save button is disabled during any pending operation

### AC7 — EditUserDialog includes status toggle

**Given** the EditUserDialog is open
**When** it renders
**Then** an Active / Inactive status toggle (same button-pair pattern as the CreateUserDialog in `TenantProvisioningPage.tsx`) is shown below the Email field
**And** it is initialised from `user.isActive`
**And** when "Save" is clicked, if `isActive` changed it is included in the PATCH body alongside other changed fields

### AC8 — Existing Tenant Admin flows unaffected

**Given** a Tenant Admin user (JWT has `tid` claim, `tenantStore.activeTenantId` is `null`)
**When** they use any Tenant Admin page
**Then** all API calls continue to work exactly as before — no regression
**And** no `X-Tenant-Id` header is sent (confirming the guard on `activeTenantId` is working)

### AC9 — Existing tests remain green

**Given** `npm test -- --run` is executed after all changes
**When** all vitest tests run
**Then** all previously passing tests continue to pass
**And** no new test files are required (acceptable POC trade-off)

---

## Out of Scope

- Dimension assignments from the Internal Admin Edit User dialog (Tenant Admin handles this via the user detail page from gap-3)
- Direct role assignment to users (roles are assigned to groups; users get roles through group membership — by design)
- Per-tenant dimension reference list management from the Internal Admin (already exists in `TenantDetailPage` overview section)
- Adding `InternalAdmin` to `TenantDimensionsController` or `TenantUserDimensionsController` — not needed for the pages being fixed in this story

---

## Tasks / Subtasks

- [x] T1 (AC1) Backend: Update `TenantContextMiddleware` — accept `X-Tenant-Id` header for `InternalAdmin`
- [x] T2 (AC2) Backend: Update `TenantUsersController` — `Roles = "TenantAdmin,InternalAdmin"`
- [x] T3 (AC2) Backend: Update `TenantGroupsController` — `Roles = "TenantAdmin,InternalAdmin"`
- [x] T4 (AC2) Backend: Update `TenantRolesController` — `Roles = "TenantAdmin,InternalAdmin"`
- [x] T5 (AC2) Backend: Update `TenantRoleSetsController` — `Roles = "TenantAdmin,InternalAdmin"`
- [x] T6 (AC3) Frontend: Update `apiClient.ts` — send `X-Tenant-Id` header from `tenantStore`
- [x] T7 (AC5–AC7) Frontend: Update `EditUserDialog` in `TenantUsersPage.tsx` — groups + status
- [x] T8 (AC9) Run `npm test -- --run` and fix any regressions

---

## Dev Notes

### CRITICAL: Read These Files Before Implementing

**Files to modify:**
- `src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs` — T1
- `src/OneId.Server/Controllers/TenantUsersController.cs` — T2
- `src/OneId.Server/Controllers/TenantGroupsController.cs` — T3
- `src/OneId.Server/Controllers/TenantRolesController.cs` — T4
- `src/OneId.Server/Controllers/TenantRoleSetsController.cs` — T5
- `src/OneId.Web/src/lib/api-client.ts` — T6
- `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx` — T7

---

### T1 — TenantContextMiddleware change

Current file: `src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs`

The existing logic initializes tenant context from the JWT `tid` claim. Add an `else if` branch after it:

```csharp
var tidClaim = context.User?.FindFirst("tid")?.Value;
if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId) && tenantId != Guid.Empty)
{
    tenantContext.Initialize(tenantId);
}
else if (context.User?.IsInRole("InternalAdmin") == true)
{
    var headerValue = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    if (headerValue is not null && Guid.TryParse(headerValue, out var headerTenantId) && headerTenantId != Guid.Empty)
    {
        tenantContext.Initialize(headerTenantId);
    }
}
await next(context);
```

**Why `else if`:** An Internal Admin should never have both a `tid` claim AND the `X-Tenant-Id` header (they're different user types). The `else if` prevents double-initialization which would throw from `TenantContext.Initialize`. The JWT path always takes priority.

**Security note:** Only `InternalAdmin` role can use the header. Tenant Admins (who do have `tid`) cannot inject a different tenant via the header — the `if` branch fires first and they never reach the `else if`.

---

### T2–T5 — Controller `[Authorize]` attribute change

Four controllers each have:
```csharp
[Authorize(
    AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
    Roles = "TenantAdmin")]
```

Change `Roles` to:
```csharp
    Roles = "TenantAdmin,InternalAdmin")]
```

Do this for all four: `TenantUsersController`, `TenantGroupsController`, `TenantRolesController`, `TenantRoleSetsController`. The attribute is on the class, not individual methods — one line change per file.

---

### T6 — apiClient.ts change

Current file: `src/OneId.Web/src/lib/api-client.ts`

Add a static import at the top of the file (alongside the existing `useAuthStore` import):
```typescript
import { useTenantStore } from '@/store/tenant-store'
```

Then in the `beforeRequest` hook array, add the header **after** the existing `Authorization` setter:
```typescript
hooks: {
  beforeRequest: [
    ({ request }) => {
      const { accessToken } = useAuthStore.getState()
      if (accessToken) {
        request.headers.set('Authorization', `Bearer ${accessToken}`)
      }
      const { activeTenantId } = useTenantStore.getState()
      if (activeTenantId) {
        request.headers.set('X-Tenant-Id', activeTenantId)
      }
    },
  ],
  // ... afterResponse unchanged
```

**Why `.getState()` works outside React:** Zustand stores expose `.getState()` as a static method — it reads the current store state without subscribing. This is the same pattern already used for `useAuthStore.getState()` in the `doRefresh` function in the same file.

**Safety:** `activeTenantId` is only set by `TenantContextLayout` (Internal Admin tenant detail pages). For Tenant Admin, it is always `null` — the `TenantAdminLayout` reads but never writes `activeTenantId`. No Tenant Admin call will include this header.

---

### T7 — EditUserDialog rewrite

Current `EditUserDialog` (lines 190–265 of `TenantUsersPage.tsx`) only has `displayName` and `email`. Replace it entirely.

**New props interface:**
```typescript
function EditUserDialog({
  user,
  tenantId,
  groups,
  onClose,
  updateUser,
}: {
  user: UserDto
  tenantId: string
  groups: GroupDto[]
  onClose: () => void
  updateUser: ReturnType<typeof useUpdateUser>
})
```

**New hooks inside the component:**
```typescript
const { data: userGroupData, isLoading: groupsLoading } = useUserGroups(tenantId, user.id)
const addGroupMember = useAddGroupMember(tenantId)
const removeGroupMember = useRemoveGroupMember(tenantId)
```

**State:**
```typescript
const [displayName, setDisplayName] = useState(user.displayName ?? '')
const [email, setEmail] = useState(user.email)
const [isActive, setIsActive] = useState(user.isActive)
const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>([])
const [emailError, setEmailError] = useState('')
const [saveError, setSaveError] = useState<string | null>(null)
const [isSubmitting, setIsSubmitting] = useState(false)
const initialGroupIdsRef = useRef<string[]>([])
```

**Load current groups via useEffect:**
```typescript
useEffect(() => {
  if (userGroupData) {
    const ids = userGroupData.map((g) => g.id)
    setSelectedGroupIds(ids)
    initialGroupIdsRef.current = ids
  }
}, [userGroupData])
```

**handleSubmit — async, computes delta:**
```typescript
const handleSubmit = async () => {
  if (!validateEmail()) return
  setSaveError(null)
  setIsSubmitting(true)
  try {
    // Patch user fields only if something changed
    const nameChanged = displayName.trim() !== (user.displayName ?? '')
    const emailChanged = email.trim() !== user.email
    const statusChanged = isActive !== user.isActive
    if (nameChanged || emailChanged || statusChanged) {
      await updateUser.mutateAsync({
        userId: user.id,
        patch: {
          ...(nameChanged && { displayName: displayName.trim() || null }),
          ...(emailChanged && { email: email.trim() }),
          ...(statusChanged && { isActive }),
        },
      })
    }

    // Compute group delta
    const original = new Set(initialGroupIdsRef.current)
    const current = new Set(selectedGroupIds)
    const toAdd = [...current].filter((id) => !original.has(id))
    const toRemove = [...original].filter((id) => !current.has(id))

    if (toAdd.length > 0 || toRemove.length > 0) {
      const results = await Promise.allSettled([
        ...toAdd.map((groupId) => addGroupMember.mutateAsync({ groupId, userId: user.id })),
        ...toRemove.map((groupId) => removeGroupMember.mutateAsync({ groupId, userId: user.id })),
      ])
      const failed = results.filter((r) => r.status === 'rejected')
      if (failed.length > 0) {
        setSaveError('Changes saved but some group assignments failed. Please check and retry.')
        return
      }
    }

    onClose()
  } catch {
    setSaveError('Failed to save changes. Please try again.')
  } finally {
    setIsSubmitting(false)
  }
}
```

**isPending guard — combine all pending states:**
```typescript
const anyPending = isSubmitting || updateUser.isPending || addGroupMember.isPending || removeGroupMember.isPending
```

**Groups section in JSX:**
```tsx
<div className="space-y-1">
  <Label>Groups</Label>
  {groupsLoading ? (
    <p className="text-sm text-muted-foreground">Loading…</p>
  ) : (
    <GroupSelectList
      groups={groups}
      selected={selectedGroupIds}
      onChange={setSelectedGroupIds}
      idPrefix="edit-user-grp"
    />
  )}
</div>
```

**Status toggle in JSX (below Email, above Groups):**
```tsx
<div className="space-y-1">
  <Label>Status</Label>
  <div className="flex gap-2">
    <Button
      type="button"
      variant={isActive ? 'default' : 'outline'}
      size="sm"
      disabled={anyPending}
      onClick={() => setIsActive(true)}
    >
      Active
    </Button>
    <Button
      type="button"
      variant={!isActive ? 'default' : 'outline'}
      size="sm"
      disabled={anyPending}
      onClick={() => setIsActive(false)}
    >
      Inactive
    </Button>
  </div>
</div>
```

**onOpenChange guard:**
```tsx
onOpenChange={(open) => { if (!open && !anyPending) onClose() }}
```

**Footer buttons:**
```tsx
<Button variant="outline" onClick={onClose} disabled={anyPending}>Cancel</Button>
<Button onClick={handleSubmit} disabled={anyPending || groupsLoading}>
  {isSubmitting ? 'Saving…' : 'Save'}
</Button>
```

**Update call site** (in `TenantUsersPage` JSX, currently lines 361–368):
```tsx
{editUser && (
  <EditUserDialog
    user={editUser}
    tenantId={tenantId}
    groups={groups}
    onClose={() => setEditUser(null)}
    updateUser={updateUser}
  />
)}
```

**Imports to add to `TenantUsersPage.tsx`:**
```typescript
import { useRef } from 'react'  // add to existing 'react' import
import { useUserGroups, useAddGroupMember, useRemoveGroupMember } from '@/queries/hooks'
```

These hooks are already exported from `@/queries/hooks/index.ts` (added in gap-3). Do NOT redefine them.

**Import `useRef`:** add it to the existing `import { useState } from 'react'` destructure: `import { useState, useRef, useEffect } from 'react'`.

---

### Key Patterns to Follow

- `apiClient.ts` — reads Zustand store state via `.getState()` without hooks; same pattern as `useAuthStore.getState()` already in that file
- `TenantContextMiddleware.cs` — the `Initialize()` call throws if called twice; `else if` ensures it only runs when `tid` is absent
- `EditUserDialog` — conditionally rendered (`{editUser && …}`) so it unmounts on close; `initialGroupIdsRef` is a ref (not state) so it doesn't cause re-renders and holds the original value even after `selectedGroupIds` mutates
- `Promise.allSettled` — used consistently with `CreateUserDialog` pattern; partial failures show error without reversing the already-saved user field patch
- Group select in edit dialog uses `idPrefix="edit-user-grp"` to avoid DOM ID collisions with `CreateUserDialog`'s `GroupSelectList` (which uses default `"user-grp"` prefix) — both can be mounted simultaneously

---

### Architecture Context

- `apiClient` is a `ky` singleton with auth/refresh interceptors at `src/OneId.Web/src/lib/api-client.ts`
- Tenant routing is JWT-based (`tid` claim) for Tenant Admin; this story adds header-based routing for Internal Admin as an alternative path
- `AppDbContext` applies `HasQueryFilter` on all tenant entities — once `TenantContext` is initialized (from either source), all EF queries are automatically tenant-scoped
- `TenantContextLayout` at `src/OneId.Web/src/routes/internal/tenants/_layout.tsx` sets `activeTenantId` on mount and clears it on unmount — the header is always present for Internal Admin tenant pages and absent elsewhere

---

## Dev Agent Record

### Implementation Plan

Implement T1–T8 in order: backend first (T1–T5), then frontend (T6–T7), then tests (T8).

### Completion Notes

- T1: `TenantContextMiddleware` extended with `else if` branch — reads `X-Tenant-Id` header only when caller has `InternalAdmin` role and no `tid` JWT claim. `else if` prevents double-initialization.
- T2–T5: `Roles` attribute changed from `"TenantAdmin"` to `"TenantAdmin,InternalAdmin"` on all four tenant CRUD controllers. One-line change per file.
- T6: `useTenantStore` imported in `api-client.ts`; `X-Tenant-Id` header added in `beforeRequest` hook when `activeTenantId` is non-null. Tenant Admin flows unaffected (`activeTenantId` is always `null` in that context).
- T7: `EditUserDialog` fully rewritten — added `tenantId`, `groups` props; `useUserGroups`/`useAddGroupMember`/`useRemoveGroupMember` hooks inside dialog; status toggle (Active/Inactive); group multi-select pre-populated from current memberships; async `handleSubmit` with delta-based add/remove using `Promise.allSettled`; `initialGroupIdsRef` captures original state for diff; `anyPending` guard covers all mutation states. Call site updated to pass `tenantId` and `groups`.
- T8: `npm test -- --run` — 128 passed, 11 pre-existing failures (same set as gap-3: `useTenants.test.ts` (5), `index.test.tsx` (3), `TenantProvisioningPage.test.tsx` (2), `new.test.tsx` (1)). Zero new failures. `dotnet build` — 0 errors, 0 warnings.

### File List

**Backend:**
- `src/OneId.Server/Infrastructure/Middleware/TenantContextMiddleware.cs` — updated (T1)
- `src/OneId.Server/Controllers/TenantUsersController.cs` — updated (T2)
- `src/OneId.Server/Controllers/TenantGroupsController.cs` — updated (T3)
- `src/OneId.Server/Controllers/TenantRolesController.cs` — updated (T4)
- `src/OneId.Server/Controllers/TenantRoleSetsController.cs` — updated (T5)

**Frontend:**
- `src/OneId.Web/src/lib/api-client.ts` — updated (T6)
- `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx` — updated (T7)

---

## Change Log

- 2026-05-30 — Story created: Internal Admin tenant context forwarding + EditUserDialog group assignment
- 2026-05-30 — Implementation complete: T1–T8 all done, 128 tests passing, 0 new failures, backend build clean
