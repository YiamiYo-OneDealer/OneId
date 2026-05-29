# Story gap-3: User Group Membership and Dimension Assignment Editing

**Status:** draft
**Epic:** Phase 8 completion / 5c.1 completion
**Story ID:** gap-3
**Prerequisite:** Epic 4a complete ✓ — `UserGroup`, `UserDimensionAssignment`, `DimensionValue` all in DB. Backend endpoints exist: `PUT /api/tenant/groups/{id}/members`, `DELETE /api/tenant/groups/{id}/members/{userId}`, `POST /api/tenant/users/{userId}/dimensions`, `DELETE /api/tenant/users/{userId}/dimensions`, `GET /api/tenant/users/{userId}/dimensions`.

---

## Story

As a Tenant Admin,
I want to view and edit a user's group memberships and dimension assignments after their account is created,
so that I can adjust access as their role in the organization changes without recreating the user.

---

## Context

After a user is created, there is currently no UI to:
1. Change which groups the user belongs to
2. Set or edit their dimensional attribute values

The new user stepper (`routes/tenant/users/new.tsx`) handles initial group selection at creation time, but Step 3 (dimension assignments) is a stub that says "Dimension assignments can be configured after user creation" — with no follow-up page to do so.

Clicking a user row in the Users list navigates to `/tenant/users/:userId/permissions` which only shows the `EffectivePermissionsPanel`. A full user detail page is needed.

**Backend endpoints already built (no new backend work in this story):**
- `PUT /api/tenant/groups/{groupId}/members` — body `{ userId }` — adds user to group
- `DELETE /api/tenant/groups/{groupId}/members/{userId}` — removes user from group
- `GET /api/tenant/users/{userId}/dimensions` — returns `{ Company: [...], Location: [...], Branch: [...], Make: [...], MarketSegment: [...] }`
- `POST /api/tenant/users/{userId}/dimensions` — body `{ axis, valueId }` — adds one dimension assignment
- `DELETE /api/tenant/users/{userId}/dimensions` — body `{ axis, valueId }` (or query param) — removes one dimension assignment
- `GET /api/tenant/dimensions` — returns all active dimension values for the tenant, grouped by axis

**Frontend hooks that need to be created** (in `src/OneId.Web/src/queries/hooks/`):

| Hook | Calls |
|------|-------|
| `useUserGroups(tenantId, userId)` | Groups the user currently belongs to — derive from `useGroups` filtered by membership, OR add a `GET /api/tenant/users/{userId}/groups` endpoint if needed |
| `useAddGroupMember(tenantId)` | `PUT api/tenant/groups/{groupId}/members` |
| `useRemoveGroupMember(tenantId)` | `DELETE api/tenant/groups/{groupId}/members/{userId}` |
| `useUserDimensions(tenantId, userId)` | `GET api/tenant/users/{userId}/dimensions` |
| `useSetUserDimension(tenantId, userId)` | `POST api/tenant/users/{userId}/dimensions` |
| `useRemoveUserDimension(tenantId, userId)` | `DELETE api/tenant/users/{userId}/dimensions` |
| `useDimensionValues(tenantId)` | `GET api/tenant/dimensions` (tenant's reference lists) |

**Note on `useUserGroups`:** The existing `GET /api/tenant/groups` endpoint returns all groups. There is no endpoint that returns only the groups a specific user belongs to. Two options:
- Option A: Call `GET /api/tenant/groups` (full list) and filter client-side by checking if the user's ID appears in each group's `memberIds` field — only works if `GroupDto` includes `memberIds`. Check `GroupDto` in `src/OneId.Web/src/api/types.ts` before implementing.
- Option B: Add `GET /api/tenant/users/{userId}/groups` backend endpoint returning group summaries for that user.

**Implement Option A if `GroupDto` includes member data. Implement Option B (add the backend endpoint) if it does not.** The story AC below uses Option B phrasing for clarity — adjust as needed.

---

## Acceptance Criteria

### AC1 — User detail page extended with tabs

**Given** `src/OneId.Web/src/routes/tenant/users/$userId/` directory
**When** a Tenant Admin clicks a user row in the Users list
**Then** they navigate to `/tenant/users/:userId` (or the existing permissions route `/tenant/users/:userId/permissions`)
**And** the page renders three tabs: **Permissions** (existing `EffectivePermissionsPanel`), **Groups**, **Dimensions**
**And** the tabs use the existing `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent` components from `src/OneId.Web/src/components/ui/tabs.tsx`
**And** the page header shows the user's display name and email (loaded via `useUser(tenantId, userId)`)
**And** deep links to each tab work: `/tenant/users/:userId` defaults to Permissions tab; a `?tab=groups` or `?tab=dimensions` query param activates the corresponding tab

### AC2 — Groups tab: view current memberships

**Given** the Groups tab is active on a user detail page
**When** it renders
**Then** it shows all groups the user currently belongs to, each as a row with the group name
**And** each row has a "Remove" button that calls `DELETE /api/tenant/groups/{groupId}/members/{userId}` via `useRemoveGroupMember`
**And** confirming removal uses the Low-tier pattern (no confirm dialog — per UX-DR19 "removing user from group" is Low tier)
**And** if the user belongs to no groups, `EmptyState` renders with copy "No group memberships" and a "Add to group" CTA
**And** `DataTable` (or a simple list) shows Skeleton rows while loading

### AC3 — Groups tab: add to group

**Given** the Groups tab on a user detail page
**When** a Tenant Admin clicks "Add to group"
**Then** a combobox or modal opens listing groups the user is NOT already a member of (all tenant groups minus current memberships)
**And** selecting a group calls `PUT /api/tenant/groups/{groupId}/members` with `{ userId }` via `useAddGroupMember`
**And** on success, the group appears immediately in the membership list (optimistic update or invalidation)
**And** adding a user to a group they're already in is idempotent on the backend (HTTP 200) — the UI prevents double-add by not showing already-joined groups in the picker
**And** after any group change, `queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })` fires to refresh the Permissions tab

### AC4 — Dimensions tab: view current assignments

**Given** the Dimensions tab is active on a user detail page
**When** it renders
**Then** it shows all five axes (Company, Location, Branch, Make, MarketSegment) regardless of whether any value is assigned
**And** each axis displays the currently assigned values as removable badge chips
**And** axes with no assignments show a "No values assigned" placeholder
**And** data is loaded via `useUserDimensions(tenantId, userId)` calling `GET /api/tenant/users/{userId}/dimensions`

### AC5 — Dimensions tab: assign and remove values

**Given** a Tenant Admin views an axis in the Dimensions tab
**When** they click "Add value" next to an axis
**Then** a combobox opens showing only active dimension values for that axis from the tenant's reference lists (`useDimensionValues(tenantId)`, filtered to the current axis)
**And** selecting a value calls `POST /api/tenant/users/{userId}/dimensions` with `{ axis, valueId }` via `useSetUserDimension`
**And** values already assigned to the user are excluded from the picker (cannot double-assign)
**And** clicking the × on an assigned value chip calls `DELETE /api/tenant/users/{userId}/dimensions` with the appropriate axis/valueId via `useRemoveUserDimension`
**And** after any dimension change, `queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })` fires

### AC6 — New user stepper Step 3 — real dimension assignment

**Given** `src/OneId.Web/src/routes/tenant/users/new.tsx` Step 3 (Dimension Assignments)
**When** a Tenant Admin reaches Step 3 during new user creation
**Then** the stub text "Dimension assignments can be configured after user creation." is replaced with a functional dimension assignment UI matching AC4/AC5 above
**And** the UI operates on local state (no API call yet — the user doesn't exist yet)
**And** on submit (Step 4 "Create User"), after creating the user with `createUser.mutateAsync`, a `POST /api/tenant/users/{userId}/dimensions` call is made for each selected dimension value (similar to how group assignments are handled with `apiClient.put`)
**And** if any dimension assignment call fails, a toast shows "User created but some dimension assignments failed. Check the user's profile." — the user is still created
**And** `DimensionalScopeSummary` continues to render in Step 3 (alongside the assignment UI) — it updates live as values are selected

### AC7 — New hooks are exported from the hooks index

**Given** `src/OneId.Web/src/queries/hooks/index.ts` (or the hooks barrel file)
**When** the new hooks are added
**Then** all new hooks are exported from the barrel: `useUserGroups`, `useAddGroupMember`, `useRemoveGroupMember`, `useUserDimensions`, `useSetUserDimension`, `useRemoveUserDimension`, `useDimensionValues`
**And** the new hooks use real `apiClient` calls — no `mockStore` usage

### AC8 — Query key coverage

**Given** `src/OneId.Web/src/queries/keys.ts`
**When** the new query keys are needed
**Then** the following keys are added if not already present:
- `queryKeys.userGroups(tenantId, userId)` — for the user's current group memberships
- `queryKeys.userDimensions(tenantId, userId)` — for the user's dimension assignments
- `queryKeys.dimensionValues(tenantId)` — for the tenant's reference lists
**And** all new query hooks use these keys (not ad-hoc inline keys)

### AC9 — Backend: `GET /api/tenant/users/{userId}/groups` (if needed per context note)

**Given** `GroupDto` in `src/OneId.Web/src/api/types.ts` does NOT include member data
**When** this story is implemented
**Then** a new backend endpoint `GET /api/tenant/users/{userId}/groups` is added to `TenantUsersController`
**And** it returns `{ items: GroupDto[] }` — the groups this user belongs to, scoped to the Tenant Admin's tenant
**And** it returns `404` if `userId` is not in the Tenant Admin's tenant
**And** it requires `TenantAdmin` role
**And** if `GroupDto` already includes member data (check `TenantGroupsController.cs` `GetGroupHandler` response), skip this backend endpoint and filter client-side

### AC10 — Existing tests remain green

**Given** `npm test -- --run` is executed after all changes
**When** all vitest tests run
**Then** all existing tests pass — new test files are not required in this story (acceptable POC trade-off)
**And** if `new.tsx` tests break due to Step 3 changes, update those tests to reflect the new dimension UI

---

## Out of Scope

- Bulk group assignment for multiple users at once
- Dimension value reference list management (adding/removing available values from the per-tenant lists) — that is handled in the Internal Admin's tenant detail pages
- Permission override creation/deletion from the user detail page (handled by DenyOverrideSheet in the Permissions tab, which already exists once gap-1 and gap-2 are done)
- Seat usage enforcement (Phase 6)
