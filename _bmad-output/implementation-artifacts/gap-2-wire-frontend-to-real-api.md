# Story gap-2: Wire Frontend Off All Mock Data

**Status:** draft
**Epic:** Phase 8 completion
**Story ID:** gap-2
**Prerequisite:** Story gap-1 complete ✓ — all four backend endpoints live and returning correct responses.

---

## Story

As a Tenant Admin,
I want the Effective Permissions panel, CommandPalette search, and permission-gated UI to work against the real backend,
so that what I see in the app reflects actual data, not fabricated fixture data.

---

## Context

Three areas of the frontend are explicitly on mock data. This story removes every `mockStore` call from production code paths and wires the real API. It also fixes two bugs discovered in the investigation.

**Files with mock data to replace (production paths only — test files are unaffected):**

| File | Mock usage | Real replacement |
|------|-----------|-----------------|
| `src/OneId.Web/src/features/users/api.ts` | `effectivePermissionsLiveOptions` — calls `mockStore.getEffectivePermissions` | `GET /api/tenant/users/{userId}/effective-permissions` |
| `src/OneId.Web/src/features/users/api.ts` | `useEffectivePermissionsPreview` — calls `mockStore.getEffectivePermissionsPreview` | `POST /api/tenant/effective-permissions/preview` |
| `src/OneId.Web/src/queries/hooks/usePermissions.ts:64` | `useCurrentUserPermissions` — returns `Promise.resolve([])` | `GET /api/account/permissions` |
| `src/OneId.Web/src/components/shared/CommandPalette.tsx:96-152` | Entity search — calls `mockStore.getUsers/getGroups/getRoles` | Real API hooks |

**Bugs to fix in this story:**

| Bug | File | Line | Fix |
|-----|------|------|-----|
| `selectedGroupIds` silently dropped on create | `routes/internal/tenants/TenantUsersPage.tsx` | ~97-108 | Call `PUT /api/tenant/groups/{id}/members` for each selected group after user creation |
| `atSeatLimit` hardcoded `false` | `routes/tenant/users/new.tsx` | ~291 | Wire to real seat usage from `useTenants` or a dedicated `useSeatUsage` hook |

---

## Acceptance Criteria

### AC1 — `useCurrentUserPermissions` wired to real API

**Given** `src/OneId.Web/src/queries/hooks/usePermissions.ts`
**When** `useCurrentUserPermissions()` is called
**Then** it calls `GET /api/account/permissions` and returns the `permissions` array from the response
**And** `useHasPermission('od.admin.users.revoke')` returns `{ permitted: true, isLoading: false }` when the current user has that permission in the backend, and `false` otherwise
**And** the query key used is `queryKeys.currentUserPermissions()` (already defined — do not add a new key)
**And** the stub comment `// The SPA cannot call introspection directly — return empty until ...` is removed

**Impact:** `DenyOverrideSheet` "Force Re-authenticate" button now correctly appears/hides based on real permission. All other `useHasPermission` gates become live.

### AC2 — `effectivePermissionsLiveOptions` wired to real API

**Given** `src/OneId.Web/src/features/users/api.ts`
**When** `effectivePermissionsLiveOptions(userId)` is called
**Then** the `queryFn` calls `apiClient.get('api/tenant/users/${userId}/effective-permissions').json<EffectivePermissionsResponse>()`
**And** the import of `mockStore` and `mockDelay` is removed from `api.ts` (if no other usages remain in the file)
**And** the comment `// Effective permissions require a confidential-client introspection call...` is removed
**And** `useEffectivePermissionsLive(userId)` behaves identically to before (returns `{ data, isLoading, isError }`) — no callers change

### AC3 — `useEffectivePermissionsPreview` wired to real API

**Given** `src/OneId.Web/src/features/users/api.ts`
**When** `useEffectivePermissionsPreview(userId, previewPayload)` is called with a non-null `previewPayload`
**Then** it calls `apiClient.post('api/tenant/effective-permissions/preview', { json: previewPayload }).json<EffectivePermissionsResponse>()` after the 350ms debounce
**And** cancel-on-new-request behaviour is preserved (the `AbortController` pattern stays)
**And** `userId` is no longer passed to the API (preview is tenant-scoped, not user-specific) — the function signature still accepts `userId` for compatibility but does not send it in the request body

**Note:** the `previewPayload` in `schemas.ts` includes `groupIds`, `roleSets`, and `overrides` — pass the full object as the POST body unchanged.

### AC4 — CommandPalette entity search wired to real API

**Given** `src/OneId.Web/src/components/shared/CommandPalette.tsx`
**When** a user types in the CommandPalette and a search action runs
**Then** the Users search calls `GET api/tenant/users?search={query}&pageSize=10` (or equivalent) and returns the first 10 matching users
**And** the Groups search calls `GET api/tenant/groups?search={query}&pageSize=10` (or equivalent)
**And** the Roles search calls `GET api/tenant/roles?search={query}&pageSize=10` (or equivalent)
**And** the imports of `mockStore`, `mockDelay`, and `from '@/mocks/types'` are removed from `CommandPalette.tsx`
**And** when no `tenantId` is active (Internal Admin at root `/`), user/group/role search is disabled for those actions — they show "Select a tenant first"

**Implementation note:** The existing backend list endpoints (`GET api/tenant/users`, `GET api/tenant/groups`, `GET api/tenant/roles`) accept `pageSize` and `page` query params. If they do not accept a `search` query param, pass `pageSize: 10` and filter client-side from the first page. Do not add a search param to the backend endpoints in this story — that is a separate enhancement. Fetching the first 10 and client-filtering is acceptable for POC.

### AC5 — Fix: Internal admin `CreateUserDialog` group assignment

**Given** `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx`
**When** an Internal Admin creates a user with one or more groups selected in the `CreateUserDialog`
**Then** after `createUser.mutateAsync` succeeds, a `PUT /api/tenant/groups/{groupId}/members` call is made for each selected group ID (matching the pattern in `routes/tenant/users/new.tsx:331-336`)
**And** if any member-add call fails, the dialog shows an error: "User created but some group assignments failed. Please check the user's group memberships."
**And** on full success, the dialog closes and `queryClient.invalidateQueries` fires for the users list
**And** if `selectedGroupIds` is empty, no member-add calls are made (existing behaviour)

### AC6 — Fix: `atSeatLimit` hardcoded false in new user stepper

**Given** `src/OneId.Web/src/routes/tenant/users/new.tsx`
**When** the stepper renders
**Then** `atSeatLimit` is computed from real data: it is `true` when `tenant.seatUsage.used >= tenant.seatUsage.max` AND `tenant.seatUsage.max !== null`
**And** the tenant data is fetched via `useTenantStore` (which already caches the resolved tenant object) — no new API call is needed if `seatUsage` is already in the Tenant DTO
**And** if `seatUsage` is not in the Tenant DTO, read it from `GET /api/tenant/users` `totalCount` response field against the license `maxSeats` (acceptable fallback)
**And** when `atSeatLimit` is `true`, the "Create User" button on step 4 is disabled with tooltip "Seat limit reached. Contact your administrator to expand your license." (behaviour already implemented, just previously unreachable)

### AC7 — No mock data imports remain in production files

**Given** all production files in `src/OneId.Web/src/` (excluding `*.test.*` and `mocks/` directory itself)
**When** a grep for `from '@/mocks/store'` or `from '@/mocks/types'` runs
**Then** zero matches are found outside of test files and the `mocks/` directory
**And** the `mocks/store.ts` and `mocks/fixtures.ts` files remain in place — they are still used by tests
**And** this is verified by a CI check (add a lint rule or note in the PR checklist — no automated test is required in this story)

### AC8 — Existing tests remain green

**Given** `npm test -- --run` is executed after all changes
**When** all 33+ vitest tests run
**Then** all tests pass — tests that mock `mockStore` methods continue to work since those files are unchanged
**And** the `useEffectivePermissionsPreview.test.ts` debounce test continues to pass — the `vi.spyOn(mockStore, 'getEffectivePermissionsPreview')` pattern in that test file must be updated to spy on `apiClient.post` or the test must be rewritten to use `vi.mock('@/lib/api-client')`
**And** `EffectivePermissions.test.tsx` passes — it seeds a `QueryClient` with pre-loaded data, bypassing the fetch entirely, so it is not affected by the endpoint change

---

## Out of Scope

- Adding a `search` query param to backend list endpoints (CommandPalette uses client-side filtering)
- Seat usage display / SeatUsageIndicator in the users list header (depends on Phase 6 licensing)
- EffectivePermissionsPanel diff highlighting between live and preview (visual enhancement, existing component handles it)
