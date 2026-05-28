# Story 5b.1: useFormMutation and useHasPermission Hooks

**Status:** review
**Epic:** 5b — Permission & Override UX
**Story ID:** 5b-1
**Prerequisite:** Epic 4b complete ✓ (introspection payload shape is stable)

---

## Story

As a developer,
I want `useFormMutation` and `useHasPermission` hooks available before any management form or permission-gated UI is built,
So that all write operations produce propagation-honest feedback and all permission gates behave consistently across the application.

---

## Acceptance Criteria

1. **Given** `useFormMutation` wraps TanStack Query `useMutation`
   **When** a mutation succeeds
   **Then** a durable (non-auto-dismiss, `duration: Infinity`) Sonner toast fires with the `success` message from `MutationMessages`
   **And** if `propagationNote: true`, "Changes effective within 5 minutes." is appended to the toast description
   **And** if `forceRevoke: true`, the toast message is overridden to "User must re-authenticate — changes are immediate" (propagationNote ignored)

2. **Given** a mutation fails with a system error (network, 5xx)
   **When** `useFormMutation` handles the error
   **Then** an auto-dismissing toast (`duration: 8000`) fires with the `error` message (string or function result)
   **And** inline form field errors are shown for validation failures (4xx with field-level `errors` map in Problem Details body) — these do NOT produce a toast; `onValidationError` callback is called instead
   **And** `onSuccess` and `onError` TanStack callbacks passed to the hook are called — they are NOT swallowed

3. **Given** `useHasPermission(permissionId)` is implemented
   **When** called in a component
   **Then** it returns `{ permitted: boolean, isLoading: boolean }` derived from the prefetched current-user permissions query
   **And** during `isLoading: true`, interactive elements render as `disabled` — not hidden, not errored
   **And** a vitest test asserts: component using `useHasPermission` renders a disabled button during loading and an enabled button once `permitted: true` resolves

4. **Given** current-user permissions are prefetched in React Router v7 route `loader` on the `internal` and `tenant` layout routes
   **When** the component mounts
   **Then** `isLoading` is `false` on first render — no flash of disabled state on cold load

---

## Tasks

- [x] **Task 1: Install `sonner`**
  - `npm install sonner` in `src/OneId.Web/`
  - Add `<Toaster richColors theme="dark" />` to `src/OneId.Web/src/main.tsx` inside the JSX tree (after `<RouterProvider />` or as sibling inside the same fragment)
  - `sonner` version: latest stable (^2.x at time of writing)

- [x] **Task 2: Add `currentUserPermissions` query key**
  - File: `src/OneId.Web/src/queries/keys.ts`
  - Add: `currentUserPermissions: () => ['currentUserPermissions'] as const`

- [x] **Task 3: Add mock `getCurrentUserPermissions` to mockStore**
  - File: `src/OneId.Web/src/mocks/store.ts`
  - Add method: `getCurrentUserPermissions: (): string[] => state.permissions.map((p) => p.id)`
  - Rationale: in the mock layer the logged-in user is the InternalAdmin who holds all permissions; returning the full catalog matches the dev seeder intent

- [x] **Task 4: Add `getCurrentUserPermissionsOptions` and `useCurrentUserPermissions` to usePermissions.ts**
  - File: `src/OneId.Web/src/queries/hooks/usePermissions.ts`
  - Add TanStack Query v5 `queryOptions` factory:
    ```typescript
    export const getCurrentUserPermissionsOptions = () =>
      queryOptions({
        queryKey: queryKeys.currentUserPermissions(),
        queryFn: async (): Promise<string[]> => {
          await mockDelay()
          return mockStore.getCurrentUserPermissions()
        },
      })
    ```
  - Add hook: `export function useCurrentUserPermissions() { return useQuery(getCurrentUserPermissionsOptions()) }`
  - Import `queryOptions` from `@tanstack/react-query`

- [x] **Task 5: Create `useHasPermission` hook**
  - File: `src/OneId.Web/src/hooks/useHasPermission.ts` (NEW)
  - Signature: `useHasPermission(permissionId: string): { permitted: boolean; isLoading: boolean }`
  - Implementation:
    ```typescript
    import { useCurrentUserPermissions } from '@/queries/hooks/usePermissions'

    export function useHasPermission(permissionId: string): { permitted: boolean; isLoading: boolean } {
      const { data: permissions, isLoading } = useCurrentUserPermissions()
      if (isLoading || !permissions) return { permitted: false, isLoading: true }
      return { permitted: permissions.includes(permissionId), isLoading: false }
    }
    ```

- [x] **Task 6: Prefetch current-user permissions in route loaders**
  - File: `src/OneId.Web/src/routes/index.tsx`
  - Import `queryClient` from `@/lib/query-client` and `getCurrentUserPermissionsOptions` from `@/queries/hooks/usePermissions`
  - Add `loader` to the `internal` route and `tenant` route:
    ```typescript
    loader: async () => {
      await queryClient.ensureQueryData(getCurrentUserPermissionsOptions())
      return null
    }
    ```
  - Both the `internal` path and the `tenant` path get this loader
  - Return type is `null` — React Router v7 accepts any serializable value from a loader

- [x] **Task 7: Create `useFormMutation` hook**
  - File: `src/OneId.Web/src/hooks/useFormMutation.ts` (NEW)
  - Full interface:
    ```typescript
    import { useMutation, UseMutationOptions } from '@tanstack/react-query'
    import { toast } from 'sonner'
    import { HTTPError } from 'ky'

    type MutationMessages<TData = unknown, TVariables = unknown> = {
      success: string | ((data: TData, variables: TVariables) => string)
      error: string | ((err: unknown) => string)
      propagationNote?: boolean
      forceRevoke?: boolean
    }

    type UseFormMutationOptions<TData, TError, TVariables, TContext> =
      Omit<UseMutationOptions<TData, TError, TVariables, TContext>, 'onSuccess' | 'onError'> & {
        messages: MutationMessages<TData, TVariables>
        onSuccess?: (data: TData, variables: TVariables, context: TContext | undefined) => void
        onError?: (error: TError, variables: TVariables, context: TContext | undefined) => void
        onValidationError?: (errors: Record<string, string[]>) => void
      }
    ```
  - Toast logic on success:
    - If `forceRevoke` → `toast.success("User must re-authenticate — changes are immediate", { duration: Infinity })`
    - Else if `propagationNote` → `toast.success(successMsg, { description: "Changes effective within 5 minutes.", duration: Infinity })`
    - Else → `toast.success(successMsg, { duration: Infinity })`
  - Error logic: extract `err.response?.json()` when `err instanceof HTTPError && err.response.status >= 400 && err.response.status < 500`; if body has `errors` map call `onValidationError(body.errors)` with NO toast; otherwise fire `toast.error(errorMsg, { duration: 8000 })`
  - Both `onSuccess` and `onError` from options are called AFTER the internal toast handlers
  - `onValidationError` is optional — if not provided, 4xx validation failures still do not produce a toast (they are silently swallowed unless the caller handles them)

- [x] **Task 8: Write vitest tests for `useHasPermission`**
  - File: `src/OneId.Web/src/hooks/useHasPermission.test.tsx` (NEW)
  - Use `@testing-library/react` + vitest
  - Test 1: component renders disabled button while `isLoading: true` (mock the query to return `{ isLoading: true, data: undefined }`)
  - Test 2: component renders enabled button when `permitted: true` (mock query returns `{ isLoading: false, data: ['od.admin.users.view'] }`)
  - Test 3: component renders disabled button when `permitted: false` (user lacks the permission)
  - Wrap the test component in a `QueryClientProvider` using a fresh `QueryClient` per test
  - Mock strategy: use `vi.mock('@/queries/hooks/usePermissions', ...)` to control `useCurrentUserPermissions` return value

- [x] **Task 9: Write vitest tests for `useFormMutation`**
  - File: `src/OneId.Web/src/hooks/useFormMutation.test.tsx` (NEW)
  - Mock `sonner` via `vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))`
  - Test 1: success — `toast.success` called with correct message; `duration: Infinity`
  - Test 2: success with `propagationNote: true` — description is "Changes effective within 5 minutes."
  - Test 3: success with `forceRevoke: true` — message is "User must re-authenticate — changes are immediate"
  - Test 4: system error (mock an `HTTPError` with status 500) — `toast.error` called with `duration: 8000`
  - Test 5: validation error (mock `HTTPError` with status 422 and body `{ errors: { name: ['required'] } }`) — `onValidationError` called with `{ name: ['required'] }`, `toast.error` NOT called

---

## Dev Context & Guardrails

### Existing Patterns — Do NOT Reinvent

- **Mutations today** — all existing mutation hooks in `queries/hooks/` (e.g., `useCreateRole`, `useUpdateUser`) call `useMutation` directly with `onSuccess: () => queryClient.invalidateQueries(...)` and NO toast. This story adds `useFormMutation` as a wrapper for all future mutations. **Do not touch existing hooks in this story** — they will be migrated story by story.
- **Query key factory** — always `queryKeys.X()` from `src/OneId.Web/src/queries/keys.ts`. Never inline raw string arrays.
- **Mock layer** — all query `queryFn` implementations in `queries/hooks/` use `mockStore` and `mockDelay()` from `@/mocks/store`. Continue this pattern.
- **`ky` `apiClient`** — single instance in `src/OneId.Web/src/lib/api-client.ts` with auth header injection and 401 → refresh logic. When the real backend integration happens, replace `mockStore` calls with `apiClient` calls — but not in this story.
- **`useActiveTenant`** — exists in `hooks/useActiveTenant.ts`, reads `tenantId` from URL params. Pattern to follow for new hooks in the `hooks/` directory.

### File Structure Rules

- `hooks/` — framework-agnostic hooks that don't do data fetching directly (they MAY call query hooks from `queries/hooks/`)
- `queries/hooks/` — TanStack Query hooks only (`useQuery`, `useMutation`, `queryOptions`)
- `useFormMutation` belongs in `hooks/` because it wraps `useMutation` with application-level logic (toasts), not raw data fetching
- `useHasPermission` belongs in `hooks/` because it's a derived permission check, not a raw query

### Architecture Mandates for This Story

- `useHasPermission` MUST return `{ permitted: boolean; isLoading: boolean }` — exact shape from architecture doc. Gate on `!isLoading && permitted` in components.
- Permissions MUST be prefetched in route loaders so `isLoading` is `false` on first render — preventing disabled-button flash. This is the whole point of the loader prefetch.
- `<Toaster />` goes in `main.tsx` — exactly one Toaster instance for the entire app. Do NOT put it in a layout component.
- `toast.success` with `duration: Infinity` = durable/non-auto-dismiss. `toast.error` with `duration: 8000` = 8-second auto-dismiss. These are requirements, not suggestions.
- `propagationNote` reflects the 5-minute introspection cache TTL (architectural constant — the same note appears in `MutationFeedback.tsx` design spec in architecture). Use exactly: "Changes effective within 5 minutes."
- `forceRevoke` is the "Force Re-authenticate" flow — user is immediately kicked out. The message "User must re-authenticate — changes are immediate" must be exact.

### Problem Details Error Shape (backend contract)

Validation errors from the backend follow RFC 9457 Problem Details:
```json
{
  "type": "https://oneid.onedealer.com/errors/validation",
  "title": "Validation failed",
  "status": 422,
  "errors": { "name": ["Name is required."] }
}
```
The `errors` field is `Record<string, string[]>`. A 4xx response has `errors` on the body → call `onValidationError`. A 5xx or network error → `toast.error`. A 4xx WITHOUT an `errors` field (e.g., 404, 409 without field errors) → `toast.error` as well.

### Detecting HTTPError from ky

```typescript
import { HTTPError } from 'ky'

// In onError handler:
if (err instanceof HTTPError) {
  const isClientError = err.response.status >= 400 && err.response.status < 500
  if (isClientError) {
    const body = await err.response.json().catch(() => null)
    if (body && typeof body === 'object' && 'errors' in body) {
      onValidationError?.(body.errors as Record<string, string[]>)
      onError?.(err as TError, variables, context)
      return
    }
  }
}
// Fall through to toast.error
```

Note: `err.response.json()` returns a Promise — the `onError` TanStack callback can be `async`. This is valid in TanStack Query v5.

### `queryOptions` from TanStack Query v5

TanStack Query v5 exports `queryOptions` helper for type-safe query option factories:
```typescript
import { queryOptions } from '@tanstack/react-query'
```
Use it in Task 4. This is the v5 idiomatic pattern (replaces the older factory function approach used before v5).

### Route Loader Integration

React Router v7 `createBrowserRouter` supports `loader` alongside `element`. The `queryClient` is already a singleton exported from `src/OneId.Web/src/lib/query-client.ts` — import directly. Loaders must return a value (use `null`). Example:

```typescript
{
  path: 'internal',
  element: <InternalLayout />,
  loader: async () => {
    await queryClient.ensureQueryData(getCurrentUserPermissionsOptions())
    return null
  },
  children: [...]
}
```

`ensureQueryData` returns the data if already cached, or fetches it — idempotent and safe to call on every navigation.

### Testing Setup

- `vitest-axe` is already installed — for 5b-2's accessibility tests. Not needed for this story's tests.
- Test setup: `src/OneId.Web/src/test-setup.ts` (check imports needed for `@testing-library/jest-dom`)
- Wrap render calls in `QueryClientProvider` with a fresh `new QueryClient({ defaultOptions: { queries: { retry: false } } })` per test to prevent cross-test cache contamination
- Use `vi.mock` for sonner and for the permissions hook — isolate unit tests from real query infrastructure
- For `useFormMutation` tests, use `renderHook` from `@testing-library/react` to call the hook and `act` to trigger the mutation

### Git — Recent Work Context

Recent epics completed: 4b (permission evaluation pipeline, enriched introspection), 4a (permission catalog, role/role-set/group/user management). Frontend work up to now: 5a (design system, routing, nav components), mock data layer (m-1), 5c-1/1b/1c (management pages and CRUD forms). The frontend has never used a toast library — this story is the first introduction of `sonner`.

### What Does NOT Exist Yet (Don't Assume)

- No `features/` directory — project uses `queries/hooks/` for data hooks and `components/shared/` for shared components. Architecture doc shows a future `features/` structure, but it hasn't been adopted. Keep new files in `hooks/` and `queries/hooks/`.
- No `react-hook-form` or `zod` installed — this story does NOT add them. `onValidationError` callback is a plain function `(errors: Record<string, string[]>) => void`.
- No `MutationFeedback.tsx` component — the architecture mentions it but it hasn't been built. This story replaces it with the `useFormMutation` hook pattern.
- No existing toast infrastructure — `sonner` is not installed yet.
- No `useHasPermission` hook exists — `hooks/` currently only has `useActiveTenant.ts`, `useSidebarState.ts`, `useTheme.ts`.

### `Permissions` Constants Reference (backend `Permissions.cs`)

The current user permissions mock (`getCurrentUserPermissions`) returns all permission IDs. Here is the complete set as of this story for test fixture reference:

| Constant Name | Permission ID |
|---|---|
| AdminTenantsView | `od.admin.tenants.view` |
| AdminTenantsCreate | `od.admin.tenants.create` |
| AdminTenantsUpdate | `od.admin.tenants.update` |
| AdminTenantsSuspend | `od.admin.tenants.suspend` |
| AdminPermissionsView | `od.admin.permissions.view` |
| AdminPermissionsCreate | `od.admin.permissions.create` |
| AdminPermissionsUpdate | `od.admin.permissions.update` |
| AdminPermissionsDeactivate | `od.admin.permissions.deactivate` |
| AdminLicensesView | `od.admin.licenses.view` |
| AdminLicensesCreate | `od.admin.licenses.create` |
| AdminLicensesUpdate | `od.admin.licenses.update` |
| AdminIdpView | `od.admin.idp.view` |
| AdminIdpConfigure | `od.admin.idp.configure` |
| AdminUsersView | `od.admin.users.view` |
| AdminUsersCreate | `od.admin.users.create` |
| AdminUsersUpdate | `od.admin.users.update` |
| AdminUsersDeactivate | `od.admin.users.deactivate` |
| AdminUsersRevoke | `od.admin.users.revoke` |
| AdminRolesView | `od.admin.roles.view` |
| AdminRolesCreate | `od.admin.roles.create` |
| AdminRolesUpdate | `od.admin.roles.update` |
| AdminRolesDelete | `od.admin.roles.delete` |
| AdminRoleSetsView | `od.admin.rolesets.view` |
| AdminRoleSetsCreate | `od.admin.rolesets.create` |
| AdminRoleSetsUpdate | `od.admin.rolesets.update` |
| AdminRoleSetsDelete | `od.admin.rolesets.delete` |
| AdminGroupsView | `od.admin.groups.view` |
| AdminGroupsCreate | `od.admin.groups.create` |
| AdminGroupsUpdate | `od.admin.groups.update` |
| AdminGroupsDelete | `od.admin.groups.delete` |
| AdminGroupsMembersManage | `od.admin.groups.members.manage` |
| AdminDimensionsView | `od.admin.dimensions.view` |
| AdminDimensionsAssign | `od.admin.dimensions.assign` |
| AdminAuditView | `od.admin.audit.view` |
| CrmRead | `od.crm.read` |
| CrmWrite | `od.crm.write` |
| CrmInvoiceCreate | `od.crm.invoice.create` |
| CrmInvoiceApprove | `od.crm.invoice.approve` |
| FinanceRead | `od.finance.read` |
| FinanceWrite | `od.finance.write` |
| FinanceApprove | `od.finance.approve` |

Story 5b-2 will use this table to build `PERMISSION_GROUPS` and `PERMISSION_LABELS` — your `getCurrentUserPermissions` mock must include ALL of these so the 5b-2 registry sync test passes.

---

## Files Changed

| File | Action | Notes |
|---|---|---|
| `src/OneId.Web/package.json` | UPDATE | Add `sonner` dependency |
| `src/OneId.Web/src/main.tsx` | UPDATE | Add `<Toaster richColors theme="dark" />` |
| `src/OneId.Web/src/queries/keys.ts` | UPDATE | Add `currentUserPermissions` key |
| `src/OneId.Web/src/mocks/store.ts` | UPDATE | Add `getCurrentUserPermissions()` |
| `src/OneId.Web/src/queries/hooks/usePermissions.ts` | UPDATE | Add `getCurrentUserPermissionsOptions` + `useCurrentUserPermissions` |
| `src/OneId.Web/src/queries/hooks/index.ts` | UPDATE | Already re-exports from `usePermissions` — no change needed if new exports are added to the same file |
| `src/OneId.Web/src/routes/index.tsx` | UPDATE | Add `loader` to `internal` and `tenant` routes |
| `src/OneId.Web/src/hooks/useHasPermission.ts` | NEW | |
| `src/OneId.Web/src/hooks/useFormMutation.ts` | NEW | |
| `src/OneId.Web/src/hooks/useHasPermission.test.tsx` | NEW | |
| `src/OneId.Web/src/hooks/useFormMutation.test.tsx` | NEW | |

---

## Dev Notes

**Implementation completed 2026-05-28.**

- `sonner` v2.x installed. `<Toaster richColors theme="dark" />` placed as last child inside `QueryClientProvider` in `main.tsx` — single instance for the whole app.
- `queryOptions` from `@tanstack/react-query` v5 used for the `getCurrentUserPermissionsOptions` factory — idiomatic v5 pattern, type-safe.
- `useHasPermission` is intentionally thin: delegates to `useCurrentUserPermissions`, returns `{ permitted: false, isLoading: true }` whenever data is absent (loading or undefined). Consumers gate on `!isLoading && permitted`.
- Route loaders on both `internal` and `tenant` routes call `queryClient.ensureQueryData(getCurrentUserPermissionsOptions())` — idempotent, no double-fetch on repeated navigation. `queryClient` imported as singleton from `lib/query-client.ts`.
- `useFormMutation.onError` is `async` (TanStack Query v5 supports it) — needed to `await err.response.json()` for the validation error body inspection.
- 422 + `errors` field on body → `onValidationError` callback, no toast. Any other error (5xx, network, 4xx without `errors`) → `toast.error` with 8 s duration.
- `forceRevoke` takes precedence over `propagationNote` in the toast logic (if/else ordering).
- Existing mutation hooks in `queries/hooks/` were intentionally not touched — migration is per-story.
- `mocks/store.ts` `getCurrentUserPermissions` returns all permission IDs from the fixture (full catalog). This covers all 40 `od.*` constants that 5b-2 will need for its sync test.

---

## Completion Checklist

- [x] `sonner` installed and `<Toaster />` renders in app
- [x] `queryKeys.currentUserPermissions()` added
- [x] `mockStore.getCurrentUserPermissions()` returns all 40 permission IDs
- [x] `getCurrentUserPermissionsOptions` exported from `queries/hooks/usePermissions.ts`
- [x] `useCurrentUserPermissions` exported from `queries/hooks/usePermissions.ts`
- [x] Route loaders on `internal` and `tenant` routes call `ensureQueryData(getCurrentUserPermissionsOptions())`
- [x] `useHasPermission.ts` created in `hooks/`; exact return type `{ permitted: boolean; isLoading: boolean }`
- [x] `useFormMutation.ts` created in `hooks/`; handles success (durable toast), system errors (8s toast), validation errors (onValidationError, no toast)
- [x] `forceRevoke` overrides `propagationNote` message correctly
- [x] `onSuccess` / `onError` TanStack callbacks still called after internal toast handling
- [x] `useHasPermission.test.tsx` passes: loading → disabled; permitted → enabled; not permitted → disabled
- [x] `useFormMutation.test.tsx` passes: all 5 test cases
- [x] `npm run test` passes (62/62 tests green)
- [x] `npm run build` passes (no TypeScript errors)

## Change Log

- **2026-05-28** — Story implemented. Installed `sonner`, created `useFormMutation` and `useHasPermission` hooks, added `currentUserPermissions` query infrastructure, prefetch route loaders on internal/tenant layouts. 9 tasks complete, 8 new tests added (62 total passing, build clean).
