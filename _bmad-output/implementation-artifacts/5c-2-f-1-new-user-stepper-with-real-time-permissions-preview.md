# Story 5c-2: F-1 New User Stepper with Real-Time Permissions Preview

Status: review

## Story

As a Tenant Admin,
I want a guided multi-step New User flow that shows me the user's effective permissions before I save,
So that I can verify the user will have the right access level before their account is created.

## Acceptance Criteria

**AC1 — Users list page replaces stub**
Given I navigate to `/tenant/users`
When the page renders
Then a `DataTable` lists all users in the active tenant with columns: Name, Email, Status (badge), Last Login
And a `SeatUsageIndicator` is shown above the table (same component used in internal admin TenantUsersPage)
And a "New User" button is visible in the page header
And if `isSeatLimitReached(used, max)` is true, the "New User" button is disabled with a `Tooltip`: "Seat limit reached — upgrade your license to add users"
And clicking "New User" when NOT at seat limit navigates to `/tenant/users/new`

**AC2 — Stepper structure**
Given I navigate to `/tenant/users/new`
When the stepper renders
Then a vertical stepper sidebar lists: (1) User Details, (2) Group Assignments, (3) Dimension Assignments, (4) Review & Confirm
And the same stepper sidebar HTML/CSS pattern from `TenantProvisioningPage` is reused exactly
And the stepper does NOT use `unstable_useBlocker` — that guard is F-3 only (UX-DR17 scope note)
And `SeatUsageIndicator` renders above the stepper in the page (reads from `useTenant`)
And if at seat limit, the "Create User" button on step 4 is disabled with tooltip: "Seat limit reached"

**AC3 — Step 1: User Details**
Given I am on step 1
When I interact with the form
Then I see: Name (required), Email (required), Status toggle (Active/Inactive, defaults Active)
And validation fires on blur per field + on submit — NOT on every keystroke (UX-DR18)
And required fields show a red asterisk in the label
And a missing/empty Name shows inline error: "Name is required."
And a missing/empty Email shows inline error: "Email is required."
And an email without `@` shows: "Enter a valid email address."
And "Next" validates step 1 before advancing — if errors exist the step does not advance

**AC4 — Step 2: Group Assignments with real-time preview**
Given I am on step 2
When I select or deselect Groups from the checkbox list
Then `EffectivePermissionsPanel` renders in `mode="preview"` with `userId=""` and a `previewPayload` built from selected group IDs (`{ groupIds: selectedGroupIds }`)
And the panel updates in real-time (debounced POST, leveraging the 350ms debounce already in `useEffectivePermissionsPreview`)
And if no groups are selected, the amber "This user will have no permissions" Alert is visible (UX-DR20 — this is rendered by the panel itself when `previewPayload.groupIds` is empty)
And group selection requires no validation — step 2 may be advanced with zero groups selected
And a search input above the group list filters groups by name (client-side, same pattern as existing `PermissionSelect` in TenantRolesPage)

**AC5 — Step 3: Dimension Assignments**
Given I am on step 3
When it renders
Then `DimensionalScopeSummary` from `@/components/shared/DimensionalScopeSummary` is shown
And since dimension reference lists are not available in mock mode, `DimensionalScopeSummary` renders with `restrictions={{}}` (empty — "no restrictions") as a placeholder showing the component is integrated
And a note below the component reads: "Dimension assignments can be configured after user creation."
And step 3 has no form validation — it always advances to step 4 on "Next"

**AC6 — Step 4: Review & Confirm**
Given I am on step 4
When it renders
Then all fields are shown read-only with section headings and an "Edit" button per section that jumps back to that step number
And `EffectivePermissionsPanel` in `mode="preview"` is shown one final time with the complete `previewPayload` (`{ groupIds: selectedGroupIds }`)
And a "Create User" button submits `POST /api/tenant/users` (via `useCreateUser` wrapped with `useFormMutation`)
And on success: toast "User created." fires and the router navigates to `/tenant/users` (the list page)
And server validation errors (e.g. duplicate email) surface as an inline error below the relevant field — not as a toast (handled via `onValidationError` callback)
And "Create User" is disabled with tooltip if at seat limit (UX-DR20)

**AC7 — Form validation (UX-DR18 compliance)**
Given any step in the stepper
When any field is validated
Then validation fires on blur per field + on submit — never on every keystroke
And "Next" / "Create User" button is NEVER pre-disabled based on field completion — only at the seat-limit gate

**AC8 — Mock store: seatUsage increment on create**
Given `mockStore.createUser` is called
When the new user is created
Then `state.tenants` entry for the user's `tenantId` has `seatUsage.used` incremented by 1
And `useCreateUser` invalidates `queryKeys.tenant(tenantId)` after success (so `SeatUsageIndicator` re-renders correctly)

**AC9 — Build clean**
`npm run build`, `npm run lint`, `npm test` all pass with no new errors.

---

## Tasks / Subtasks

- [x] Task 1: Extend mock store and useCreateUser (AC: 8)
  - [x] In `src/OneId.Web/src/mocks/store.ts`, update `createUser` to find the tenant by `data.tenantId` and increment `tenant.seatUsage.used` by 1 if the tenant exists
  - [x] In `src/OneId.Web/src/queries/hooks/useUsers.ts`, add `queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })` to `useCreateUser`'s `onSuccess` callback
  - [x] Also invalidate `queryKeys.users(tenantId)` (already present — keep it)

- [x] Task 2: Create Tenant Users List page (AC: 1)
  - [x] Create `src/OneId.Web/src/routes/tenant/users/index.tsx` exporting `TenantUsersListPage`
  - [x] Use `useTenantStore((s) => s.activeTenantId)` to get `tenantId`
  - [x] Use `useUsers(tenantId)` for the user list
  - [x] Use `useTenant(tenantId)` (from `@/queries/hooks/useTenants`) to get `seatUsage`
  - [x] Render `SeatUsageIndicator` from `@/components/shared/SeatUsageIndicator` above the table
  - [x] Render a `DataTable` (from `@/components/shared/DataTable`) with columns: Name, Email, Status badge, Last Login (format as "Never" when null, otherwise format the date string)
  - [x] "New User" button: when `isSeatLimitReached(used, max)` is true, wrap in `TooltipProvider`/`Tooltip`/`TooltipTrigger`/`TooltipContent` and disable the button; otherwise, render `<Button onClick={() => navigate('/tenant/users/new')}>New User</Button>`
  - [x] Show `EmptyState` variant `"empty"` when users list is empty (no users yet)
  - [x] Show `EmptyState` variant `"error"` when the query errors

- [x] Task 3: Create New User Stepper page (AC: 2–7)
  - [x] Create `src/OneId.Web/src/routes/tenant/users/new.tsx` exporting `NewUserPage`
  - [x] Copy the `STEPS` + vertical stepper sidebar HTML exactly from `TenantProvisioningPage` — only the labels differ: `['User Details', 'Group Assignments', 'Dimension Assignments', 'Review & Confirm']`
  - [x] Stepper state: `currentStep: 1|2|3|4`
  - [x] Form state at top level: `name`, `nameError`, `email`, `emailError`, `status: 'active'|'inactive'` (default `'active'`), `selectedGroupIds: string[]`
  - [x] `validateStep1()`: returns `true` if both name and email are non-empty and email contains `@`; sets inline errors otherwise; follows the pattern in `TenantProvisioningPage.handleNext`
  - [x] `handleNext()`: validates current step before advancing; step 2 and 3 always advance (no required validation)
  - [x] Step 1 component: fields for Name, Email, Status toggle (Active/Inactive buttons) — same pattern as `StepTenantDetails` in TenantProvisioningPage
  - [x] Step 2 component: group checkbox list with search input; `EffectivePermissionsPanel` in preview mode embedded below the list
    - [x] Use `useGroups(tenantId)` for the group list
    - [x] Group list: checkbox per group (reuse Radix `Checkbox` from `@/components/ui/checkbox`); use `<div>` not `<label>` wrapper (a11y fix from 5c-1b review — use `id`/`htmlFor` pattern)
    - [x] Search input filters groups by name (client-side, `toLowerCase().includes(search.toLowerCase())`)
    - [x] `EffectivePermissionsPanel` props: `mode="preview"`, `userId="__preview__"`, `previewPayload={{ groupIds: selectedGroupIds }}`
    - [x] The amber "no permissions" alert is handled inside the panel itself — no duplicate rendering needed
  - [x] Step 3 component: render `DimensionalScopeSummary` from `@/components/shared/DimensionalScopeSummary` with `restrictions={{}}` and a `<p className="text-sm text-muted-foreground mt-2">Dimension assignments can be configured after user creation.</p>`
  - [x] Step 4 (Review) component: read-only display of all fields; "Edit" button per section calls `setCurrentStep(n)`; `EffectivePermissionsPanel mode="preview"` with complete payload; "Create User" button

- [x] Task 4: Implement useFormMutation-based submit in NewUserPage (AC: 6)
  - [x] In `new.tsx`, use `useFormMutation` from `@/hooks/useFormMutation` wrapping `useCreateUser(tenantId)`:
    ```typescript
    const createUser = useFormMutation({
      ...useCreateUser(tenantId),  // NOTE: useCreateUser returns a UseMutationResult — spread its options
      messages: {
        success: 'User created.',
        error: 'Failed to create user.',
      },
      onSuccess: () => navigate('/tenant/users'),
      onValidationError: (errors) => {
        if (errors['email']) setEmailError(errors['email'][0])
        if (errors['name']) setNameError(errors['name'][0])
        setCurrentStep(1)  // jump back to step 1 where the errors are
      },
    })
    ```
  - [x] IMPORTANT: `useFormMutation` wraps `useMutation` itself. Do NOT double-wrap. Create a dedicated `useCreateUserForm(tenantId)` that uses `useFormMutation` directly:
    ```typescript
    export function useCreateUserForm(tenantId: string) {
      const queryClient = useQueryClient()
      return useFormMutation({
        mutationFn: async (data: Omit<User, 'id' | 'createdAt'>) => {
          await mockDelay(200)
          return mockStore.createUser(data)
        },
        messages: {
          success: 'User created.',
          error: 'Failed to create user.',
        },
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
          queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
        },
      })
    }
    ```
    Export `useCreateUserForm` from `useUsers.ts` (keep existing `useCreateUser` unchanged — it's used by internal admin).
  - [x] In `NewUserPage`, call `useCreateUserForm(tenantId)` and pass `onSuccess: () => navigate('/tenant/users')` and `onValidationError` via the hook's options — but since `useFormMutation` bakes these in at hook level, pass navigate via closure in `useUsers.ts` is tricky. Simplest: use `useCreateUser(tenantId)` plain mutation in component, handle toast manually with `toast` from `sonner`, and call `navigate` in `onSuccess`:
    ```typescript
    const createUser = useCreateUser(tenantId)
    const handleSubmit = async () => {
      createUser.mutate(
        { tenantId, name, email, status, groupIds: selectedGroupIds, lastLogin: null },
        {
          onSuccess: () => {
            toast.success('User created.')
            navigate('/tenant/users')
          },
          onError: () => toast.error('Failed to create user.'),
        },
      )
    }
    ```
    Use this simpler pattern — it's consistent with how `TenantProvisioningPage` handles submission (it uses direct mutation callbacks rather than `useFormMutation`).

- [x] Task 5: Update router (AC: 1, 2)
  - [x] In `src/OneId.Web/src/routes/index.tsx`:
    - [x] Import `TenantUsersListPage` from `'./tenant/users/index'`
    - [x] Import `NewUserPage` from `'./tenant/users/new'`
    - [x] Remove the `StubPage` import for `users` (keep StubPage for groups/roles/role-sets)
    - [x] Replace `{ path: 'users', element: <StubPage title="Users" /> }` with `{ path: 'users', element: <TenantUsersListPage /> }`
    - [x] Add `{ path: 'users/new', element: <NewUserPage /> }` before the `users/:userId/permissions` route

- [x] Task 6: Tests (AC: 1–8)
  - [x] Create `src/OneId.Web/src/routes/tenant/users/index.test.tsx`:
    - [x] Test: renders user list from mock store
    - [x] Test: shows EmptyState when no users
    - [x] Test: disables "New User" button and shows tooltip when at seat limit
    - [x] Test: "New User" button navigates to `/tenant/users/new` when not at seat limit
  - [x] Create `src/OneId.Web/src/routes/tenant/users/new.test.tsx`:
    - [x] Test: renders stepper with 4 steps visible
    - [x] Test: "Next" on step 1 with empty name shows inline error and does not advance
    - [x] Test: "Next" on step 1 with valid fields advances to step 2
    - [x] Test: step 2 renders group checkbox list and EffectivePermissionsPanel
    - [x] Test: "Create User" on step 4 calls createUser mutate with correct payload
  - [x] All 126 existing tests still pass (135 total — 9 new tests added)

---

## Dev Notes

### CRITICAL: Read Before Starting

**Project state entering this story:**
- Test count: 126 passing (from 5b-6 completion)
- TypeScript errors: 0
- Build: clean
- All Epic 5b stories done; all Epic 4b stories done

**`/tenant/users` is currently a `StubPage`** (router line 75). This story replaces it with a real page. The `UserPermissionsPage` at `/tenant/users/:userId/permissions` already exists and must NOT be touched.

**Dimension assignment (Step 3) — mock limitation**: The `User` type does not have a `dimensionAssignments` field. The real backend PUT endpoint (`/api/tenant/users/{userId}/dimensions`) was built in 4a-6 as a separate API call. For this Phase 8 mock story, Step 3 shows `DimensionalScopeSummary` with `restrictions={{}}` as a placeholder — no data is collected or persisted for dimensions. This is the correct scoped behavior for mock mode. The epics require `DimensionalScopeSummary` is "integrated" (present in the step), not that dimensions are persisted.

**`useFormMutation` vs plain mutation**: Do NOT use `useFormMutation` for the create-user submit. Follow the simpler `TenantProvisioningPage` pattern: plain `useMutation` (via `useCreateUser`) with `mutate()` callbacks for `onSuccess`/`onError`. The `useFormMutation` hook is designed for edit flows where the mutation is shared; the stepper is a one-shot create flow.

**`useCreateUser` currently does NOT increment `seatUsage.used`** — Task 1 fixes this. Without it, `SeatUsageIndicator` on the list page would show stale data after creating a user.

### Stepper HTML Pattern (copy from TenantProvisioningPage)

```tsx
const STEPS = [
  { id: 1 as const, label: 'User Details' },
  { id: 2 as const, label: 'Group Assignments' },
  { id: 3 as const, label: 'Dimension Assignments' },
  { id: 4 as const, label: 'Review & Confirm' },
] as const

// In JSX:
<div className="flex gap-8">
  {/* Vertical stepper sidebar */}
  <ol className="min-w-[180px] space-y-4">
    {STEPS.map((step) => (
      <li key={step.id} className="flex items-center gap-3">
        <span className={cn(
          'flex h-7 w-7 items-center justify-center rounded-full text-sm font-medium',
          currentStep === step.id ? 'bg-primary text-background'
            : currentStep > step.id ? 'bg-primary/20 text-primary'
            : 'bg-card text-muted-foreground',
        )}>
          {step.id}
        </span>
        <span className={cn(
          'text-sm',
          currentStep === step.id ? 'font-medium text-foreground' : 'text-muted-foreground',
        )}>
          {step.label}
        </span>
      </li>
    ))}
  </ol>
  {/* Step content — flex-1 */}
  <div className="flex-1 space-y-6">
    {currentStep === 1 && <StepUserDetails ... />}
    {currentStep === 2 && <StepGroupAssignments ... />}
    {currentStep === 3 && <StepDimensionAssignments />}
    {currentStep === 4 && <StepReview ... />}
    <div className="flex justify-between">
      {currentStep > 1 ? <Button variant="outline" onClick={handleBack}>Back</Button> : <div />}
      {currentStep < 4
        ? <Button onClick={handleNext}>Next</Button>
        : <Button onClick={handleSubmit} disabled={isSubmitting || atSeatLimit}>Create User</Button>}
    </div>
  </div>
</div>
```

**Key difference from F-3**: No `useBlocker`. Do NOT add `unstable_useBlocker` — UX-DR17 explicitly scopes the blocker to F-3 (Tenant provisioning) only.

### Step 2 Layout with EffectivePermissionsPanel

```tsx
function StepGroupAssignments({
  tenantId, selectedGroupIds, onToggleGroup,
}: {
  tenantId: string
  selectedGroupIds: string[]
  onToggleGroup: (groupId: string) => void
}) {
  const [search, setSearch] = useState('')
  const { data: groups = [] } = useGroups(tenantId)
  const filtered = groups.filter((g) => g.name.toLowerCase().includes(search.toLowerCase()))

  return (
    <div className="flex gap-6">
      {/* Left: group selector */}
      <div className="w-64 space-y-2">
        <Input placeholder="Search groups…" value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="space-y-1 max-h-64 overflow-y-auto">
          {filtered.map((group) => (
            <div key={group.id} className="flex items-center gap-2 py-1">
              <Checkbox
                id={`group-${group.id}`}
                checked={selectedGroupIds.includes(group.id)}
                onCheckedChange={() => onToggleGroup(group.id)}
              />
              <label htmlFor={`group-${group.id}`} className="text-sm cursor-pointer">
                {group.name}
              </label>
            </div>
          ))}
        </div>
      </div>
      {/* Right: live preview */}
      <div className="flex-1">
        <p className="text-sm font-medium text-foreground mb-2">Permission Preview</p>
        <EffectivePermissionsPanel
          mode="preview"
          userId=""
          previewPayload={{ groupIds: selectedGroupIds }}
        />
      </div>
    </div>
  )
}
```

Note: `userId=""` is intentional — the user does not exist yet. `useEffectivePermissionsPreview` must handle empty/falsy userId gracefully (it already guards `if (!previewPayload || !userId) return` — which means it returns null data when userId is empty). Update the hook guard: change `if (!previewPayload || !userId)` to `if (!previewPayload)` so it works with an empty userId in mock mode. Verify this does not break existing tests.

Wait — reconsider. The hook checking `!userId` was intentional for live mode. For preview mode with new user creation, userId genuinely is empty. The simplest fix: pass `userId="__preview__"` (a sentinel) instead of `""`. The mock store's `getEffectivePermissionsPreview` receives this string but in mock mode it doesn't need a real userId — it builds permissions from payload. Check `store.ts`'s `getEffectivePermissionsPreview` implementation and adjust the sentinel if needed. If the hook's `!userId` guard would block it, pass `"__preview__"` as userId.

### SeatUsageIndicator Integration

```tsx
// In TenantUsersListPage and NewUserPage:
import { SeatUsageIndicator, isSeatLimitReached } from '@/components/shared/SeatUsageIndicator'
import { useTenant } from '@/queries/hooks/useTenants'

const { data: tenant } = useTenant(tenantId)
const { used, max } = tenant?.seatUsage ?? { used: 0, max: null }
const atSeatLimit = isSeatLimitReached(used, max)
```

There is NO `useSeatUsage` hook. Use `useTenant` which already returns the full tenant including `seatUsage`.

### Seat Limit Tooltip Pattern (reuse from DenyOverrideBadge/TenantUsersPage)

```tsx
{atSeatLimit ? (
  <TooltipProvider>
    <Tooltip>
      <TooltipTrigger asChild>
        <span tabIndex={0}>
          <Button disabled>New User</Button>
        </span>
      </TooltipTrigger>
      <TooltipContent>Seat limit reached — upgrade your license to add users</TooltipContent>
    </Tooltip>
  </TooltipProvider>
) : (
  <Button onClick={() => navigate('/tenant/users/new')}>New User</Button>
)}
```

The `<span tabIndex={0}>` wrapper is needed because a disabled button swallows pointer events and Radix Tooltip cannot attach to it directly.

### mock store createUser extension (Task 1)

```typescript
// In store.ts, update createUser:
createUser: (data: Omit<User, 'id' | 'createdAt'>): User => {
  const user: User = { ...data, id: `user-${Date.now()}`, createdAt: new Date().toISOString() }
  state.users.push(user)
  // Increment seat usage for this tenant
  const tenant = state.tenants.find((t) => t.id === data.tenantId)
  if (tenant) tenant.seatUsage.used += 1
  return user
},
```

### Router Update Pattern

```typescript
// src/routes/index.tsx additions:
import { TenantUsersListPage } from './tenant/users/index'
import { NewUserPage } from './tenant/users/new'

// In tenant children array, replace:
{ path: 'users', element: <StubPage title="Users" /> },
// With:
{ path: 'users', element: <TenantUsersListPage /> },
{ path: 'users/new', element: <NewUserPage /> },
// Keep existing (unchanged):
{
  path: 'users/:userId/permissions',
  element: <UserPermissionsPage />,
  loader: async ({ params }) => { ... },
},
```

The `StubPage` import line can remain if it's used for groups/roles/role-sets — do not remove the import entirely.

### File Structure

```
src/OneId.Web/src/
├── routes/
│   └── tenant/
│       └── users/
│           ├── index.tsx              ← NEW: TenantUsersListPage
│           └── new.tsx                ← NEW: NewUserPage (4-step stepper)
├── routes/index.tsx                   ← MODIFY: add new routes, import new pages
├── queries/hooks/useUsers.ts          ← MODIFY: invalidate tenant query in useCreateUser
└── mocks/store.ts                     ← MODIFY: increment seatUsage.used in createUser
```

No new shadcn components needed. All required components exist:
- `Button`, `Input`, `Label`, `Dialog`, `Tooltip/TooltipProvider/TooltipTrigger/TooltipContent` — already installed
- `Checkbox` — installed in 5c-1b
- `Alert`, `AlertDescription` — installed in 5b-4
- `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent` — used by EffectivePermissionsPanel

### Existing Components to Import

| Import | Source |
|--------|--------|
| `DataTable` | `@/components/shared/DataTable` |
| `EmptyState` | `@/components/shared/EmptyState` |
| `SeatUsageIndicator`, `isSeatLimitReached` | `@/components/shared/SeatUsageIndicator` |
| `DimensionalScopeSummary` | `@/components/shared/DimensionalScopeSummary` |
| `EffectivePermissionsPanel` | `@/features/users/components/EffectivePermissions` |
| `useGroups` | `@/queries/hooks` |
| `useUsers`, `useCreateUser` | `@/queries/hooks` |
| `useTenant` | `@/queries/hooks/useTenants` |
| `useTenantStore` | `@/store/tenant-store` |
| `Checkbox` | `@/components/ui/checkbox` |
| `toast` | `sonner` |

### ESLint Design-Token Rule (pre-existing lint constraint)

Only semantic tokens in JSX `className`. Valid: `bg-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`, `text-destructive`. Raw Tailwind color classes like `text-red-500` will fail lint — use `text-destructive` instead.

Exception: `EffectivePermissions.tsx` uses `text-green-400`, `border-green-500`, `text-red-400` for diff highlights — those were accepted in 5b-4. The `SeatUsageIndicator` uses `text-amber-400`, `text-red-400` — also pre-existing exceptions. Do not add new raw color classes in new files; use semantic tokens.

### Test Count Target

Baseline: 126 tests. This story adds ~9 new tests (4 for list page + 5 for stepper). Target: ≥ 135 tests.

### References

- Epics.md Story 5c.2 [Source: `_bmad-output/planning-artifacts/epics.md` line 1989]
- UX-DR17 (stepper shape, no-blocker for F-1) [Source: `_bmad-output/planning-artifacts/epics.md` line 133]
- UX-DR18 (validation on blur) [Source: `_bmad-output/planning-artifacts/epics.md` line 135]
- UX-DR20 (F-1 preview, amber alert, seat check at list level) [Source: `_bmad-output/planning-artifacts/epics.md` line 139]
- TenantProvisioningPage (stepper HTML pattern) [Source: `src/OneId.Web/src/routes/internal/tenants/TenantProvisioningPage.tsx` lines 355–470]
- PreviewPayload, EffectivePermissionsPanel [Source: `src/OneId.Web/src/features/users/schemas.ts` + `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx`]
- DimensionalScopeSummary [Source: `src/OneId.Web/src/components/shared/DimensionalScopeSummary.tsx`]
- SeatUsageIndicator, isSeatLimitReached [Source: `src/OneId.Web/src/components/shared/SeatUsageIndicator.tsx`]
- useCreateUser, useUsers [Source: `src/OneId.Web/src/queries/hooks/useUsers.ts`]
- Router config [Source: `src/OneId.Web/src/routes/index.tsx` lines 75–88]
- 5b-4 completion notes (useEffectivePermissionsPreview userId guard) [Source: `_bmad-output/implementation-artifacts/5b-4-effectivepermissionspanel-preview-mode.md`]
- 5b-6 completion notes (test count: 126) [Source: `_bmad-output/implementation-artifacts/5b-6-dimensionalscopesummary-component.md`]
- 5c-1b review findings (a11y: Radix Checkbox with div not label wrapper; disabled button tooltip pattern) [Source: `_bmad-output/implementation-artifacts/5c-1b-tenant-management-crud-forms.md`]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Used `userId="__preview__"` sentinel (not `""`) for EffectivePermissionsPanel in preview mode — the hook guards `!userId` which would block empty string. Mock store's `getEffectivePermissionsPreview` works with any userId string in mock mode.
- `activeTenantId` is `string | null` from the tenant store — resolved with `?? ''` pattern (hooks handle empty string via `enabled: !!tenantId`).
- Pre-existing `TS6133` lint errors (unused `React` import) in 3 files were exposed by tsc incremental rebuild cache invalidation — fixed by removing unused imports from `DenyOverrideBadge.tsx`, `DenyOverrideSheet.test.tsx`, `EffectivePermissions.test.tsx`.
- Stepper HTML/CSS pattern copied exactly from `TenantProvisioningPage` — no `unstable_useBlocker` (F-3 only per UX-DR17).
- Submit uses plain `useCreateUser.mutate()` with inline callbacks (toast + navigate) following TenantProvisioningPage pattern — no `useFormMutation` double-wrap.
- Test count: 135 passing (was 126, added 9 new tests).

### File List

**New files:**
- `src/OneId.Web/src/routes/tenant/users/index.tsx` — TenantUsersListPage (users list with DataTable, SeatUsageIndicator, New User button)
- `src/OneId.Web/src/routes/tenant/users/new.tsx` — NewUserPage (4-step stepper with group preview)
- `src/OneId.Web/src/routes/tenant/users/index.test.tsx` — 4 tests for TenantUsersListPage
- `src/OneId.Web/src/routes/tenant/users/new.test.tsx` — 5 tests for NewUserPage

**Modified files:**
- `src/OneId.Web/src/routes/index.tsx` — replaced StubPage at /tenant/users, added /tenant/users/new route
- `src/OneId.Web/src/queries/hooks/useUsers.ts` — `useCreateUser` now invalidates `queryKeys.tenant(tenantId)` on success
- `src/OneId.Web/src/mocks/store.ts` — `createUser` increments `seatUsage.used` for the tenant; typed `permissions` array in `getEffectivePermissionsPreview`
- `src/OneId.Web/src/components/shared/DenyOverrideBadge.tsx` — removed unused `import * as React from 'react'`
- `src/OneId.Web/src/components/shared/DenyOverrideSheet.test.tsx` — removed unused `import * as React from 'react'`
- `src/OneId.Web/src/features/users/components/EffectivePermissions.test.tsx` — removed unused `import * as React from 'react'`

## Change Log

- 2026-05-28: Story 5c-2 implemented — TenantUsersListPage + NewUserPage (4-step stepper), 9 new tests, 135 total passing. (Dev Agent)
