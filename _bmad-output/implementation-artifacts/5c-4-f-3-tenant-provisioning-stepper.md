# Story 5c-4: F-3 Tenant Provisioning Stepper

Status: done

## Story

As an Internal Admin,
I want a guided multi-step Tenant provisioning flow with an unsaved-changes guard,
So that I can configure a new Tenant completely in one session without losing partial work on accidental navigation.

## Acceptance Criteria

**Given** the Tenant provisioning flow is opened from the Tenants page
**When** it renders
**Then** a vertical stepper presents: (1) Tenant Details, (2) License Configuration, (3) Initial Tenant Admin, (4) Review & Confirm
**And** "Next" validates the current section before advancing
**And** the Review step shows all sections read-only with "Edit" links

**Given** the user has entered data on any step and attempts to navigate away (browser back, sidebar link, "← All Tenants")
**When** `unstable_useBlocker` fires
**Then** a confirmation Dialog appears: "You have unsaved changes. Leaving will discard this new tenant. Continue?"
**And** the blocker fires only for user-entered content — step navigation between stepper sections does NOT trigger it
**And** a code comment documents: `// unstable_useBlocker — API unstable in RR7 minor versions, does not intercept tab close. Scope: F-3 only.`

**Given** step 2 (License Configuration)
**When** the Internal Admin fills in `maxSeats`
**Then** a live preview shows "This tenant will allow up to N active users."
**And** leaving `maxSeats` blank is valid — submitting without a value creates the Tenant with no seat limit

**Given** the final "Create Tenant" submit on the Review step
**When** the form is submitted
**Then** `useCreateTenant().mutateAsync` fires with `{ name, status, seatUsage: { used: 0, max } }`
**And** if Initial Tenant Admin was NOT skipped, `mockStore.createUser` is called with the entered name/email, `status: 'active'`, `groupIds: []`, `lastLogin: null`, and the new `tenantId`
**And** on success the user is navigated to `/internal/tenants/${newTenant.id}`
**And** on error a message "Failed to create tenant. Please try again." appears above the Create button

**Given** step 1 (Tenant Details)
**When** the user clicks "Next" with an empty name field
**Then** an inline error "Tenant name is required." appears under the name input
**And** the stepper does NOT advance

**Given** step 3 (Initial Tenant Admin)
**When** the user checks "Skip for now — designate Tenant Admin later"
**Then** the name and email fields are hidden
**And** "Next" advances without validation errors

**Given** the Tenants list page (`/internal/tenants`)
**When** it renders
**Then** a "New Tenant" button appears in the page header that navigates to `/internal/tenants/new`

**Given** `npm run build`, `npm run lint`, `npm test`
**Then** all pass with no new errors

## Tasks / Subtasks

- [x] Add `TenantProvisioningPage` component as new file (AC: stepper, validation, blocker, submit)
- [x] Register route `tenants/new` before `tenants/:tenantId` in `routes/index.tsx` (AC: routing)
- [x] Add "New Tenant" button to `TenantListPage.tsx` (AC: entry point)
- [x] Write component tests in `TenantProvisioningPage.test.tsx` (AC: validation, step nav, review)
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: build clean)

## Dev Notes

### Files to create / modify

| File | Action |
|------|--------|
| `src/OneId.Web/src/routes/internal/tenants/TenantProvisioningPage.tsx` | **NEW** |
| `src/OneId.Web/src/routes/internal/tenants/TenantProvisioningPage.test.tsx` | **NEW** |
| `src/OneId.Web/src/routes/index.tsx` | **MODIFY** — add route + import |
| `src/OneId.Web/src/routes/internal/tenants/TenantListPage.tsx` | **MODIFY** — add New Tenant button |

---

### Route registration (routes/index.tsx)

Add the import and route BEFORE the `tenants/:tenantId` dynamic segment so `new` is matched as a static path first (React Router resolves static segments over params but placing it first is explicit and safe):

```diff
+import { TenantProvisioningPage } from './internal/tenants/TenantProvisioningPage'
 ...
 { path: 'tenants', element: <TenantListPage /> },
+{ path: 'tenants/new', element: <TenantProvisioningPage /> },
 {
   path: 'tenants/:tenantId',
```

---

### TenantListPage.tsx — add "New Tenant" button

The current header is:
```tsx
<div className="flex items-center justify-between">
  <h1 className="text-2xl font-semibold text-foreground">Tenants</h1>
</div>
```

Change to (add Link import from react-router):
```tsx
<div className="flex items-center justify-between">
  <h1 className="text-2xl font-semibold text-foreground">Tenants</h1>
  <Button size="sm" asChild>
    <Link to="/internal/tenants/new">New Tenant</Link>
  </Button>
</div>
```

Add `import { Link } from 'react-router'` at the top (alongside existing `import { Link }` — it's already imported in TenantListPage.tsx via the column cell definition, check if it needs to be added or is already there).

---

### TenantProvisioningPage.tsx — complete implementation

#### Imports needed

```typescript
import { useState } from 'react'
import { useNavigate, Link, unstable_useBlocker } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useCreateTenant } from '@/queries/hooks'
import { mockStore, mockDelay } from '@/mocks/store'
import { queryKeys } from '@/queries/keys'
import { cn } from '@/lib/utils'
import type { TenantStatus } from '@/mocks/types'
```

#### Steps constant

```typescript
const STEPS = [
  { id: 1 as const, label: 'Tenant Details' },
  { id: 2 as const, label: 'License Configuration' },
  { id: 3 as const, label: 'Initial Tenant Admin' },
  { id: 4 as const, label: 'Review & Confirm' },
] as const
```

#### State structure

```typescript
// Navigation
const [currentStep, setCurrentStep] = useState<1 | 2 | 3 | 4>(1)
const [isDirty, setIsDirty] = useState(false)
const [submitError, setSubmitError] = useState<string | null>(null)

// Step 1: Tenant Details
const [tenantName, setTenantName] = useState('')
const [tenantNameError, setTenantNameError] = useState('')
const [tenantStatus, setTenantStatus] = useState<TenantStatus>('active')

// Step 2: License Configuration
const [maxSeats, setMaxSeats] = useState('')
const [maxSeatsError, setMaxSeatsError] = useState('')

// Step 3: Initial Tenant Admin
const [adminName, setAdminName] = useState('')
const [adminNameError, setAdminNameError] = useState('')
const [adminEmail, setAdminEmail] = useState('')
const [adminEmailError, setAdminEmailError] = useState('')
const [skipAdmin, setSkipAdmin] = useState(false)
```

#### `unstable_useBlocker` usage

```typescript
// unstable_useBlocker — API unstable in RR7 minor versions, does not intercept tab close. Scope: F-3 only.
const blocker = unstable_useBlocker(isDirty)
```

`isDirty` starts as `false`. Set to `true` on any `onChange` handler. Set back to `false` after successful submit (before `navigate()`). Step navigation (setCurrentStep) does NOT touch `isDirty` — the blocker only fires on actual route navigation, so step transitions never trigger it.

#### Validation functions

```typescript
const validateStep1 = () => {
  if (!tenantName.trim()) { setTenantNameError('Tenant name is required.'); return false }
  setTenantNameError('')
  return true
}

const validateStep2 = () => {
  const trimmed = maxSeats.trim()
  if (trimmed && (!/^\d+$/.test(trimmed) || parseInt(trimmed, 10) < 1)) {
    setMaxSeatsError('Enter a positive number, or leave blank for no seat limit.')
    return false
  }
  setMaxSeatsError('')
  return true
}

const validateStep3 = () => {
  if (skipAdmin) return true
  let ok = true
  if (!adminName.trim()) { setAdminNameError('Name is required.'); ok = false }
  else setAdminNameError('')
  if (!adminEmail.trim()) { setAdminEmailError('Email is required.'); ok = false }
  else if (!adminEmail.includes('@')) { setAdminEmailError('Enter a valid email address.'); ok = false }
  else setAdminEmailError('')
  return ok
}
```

#### Navigation handlers

```typescript
const handleNext = () => {
  if (currentStep === 1 && !validateStep1()) return
  if (currentStep === 2 && !validateStep2()) return
  if (currentStep === 3 && !validateStep3()) return
  setCurrentStep((s) => (s < 4 ? ((s + 1) as 1 | 2 | 3 | 4) : s))
}

const handleBack = () => {
  setCurrentStep((s) => (s > 1 ? ((s - 1) as 1 | 2 | 3 | 4) : s))
}
```

#### Submit handler

The admin user creation uses `mockStore.createUser` directly (not a hook) because the `tenantId` is only known after the tenant is created, which makes hook-based pre-wiring impossible. This is a demo-only pattern.

```typescript
const handleSubmit = async () => {
  setSubmitError(null)
  try {
    const newTenant = await createTenant.mutateAsync({
      name: tenantName.trim(),
      status: tenantStatus,
      seatUsage: { used: 0, max: maxSeats.trim() ? parseInt(maxSeats.trim(), 10) : null },
    })
    if (!skipAdmin && adminName.trim() && adminEmail.trim()) {
      await mockDelay(200)
      mockStore.createUser({
        tenantId: newTenant.id,
        name: adminName.trim(),
        email: adminEmail.trim(),
        status: 'active',
        groupIds: [],
        lastLogin: null,
      })
      queryClient.invalidateQueries({ queryKey: queryKeys.users(newTenant.id) })
    }
    setIsDirty(false)
    navigate(`/internal/tenants/${newTenant.id}`)
  } catch {
    setSubmitError('Failed to create tenant. Please try again.')
  }
}
```

#### JSX structure

The page renders two columns: a vertical stepper indicator on the left, step content on the right.

```tsx
return (
  <div className="space-y-6">
    {/* Page header */}
    <div className="flex items-center gap-4">
      <Link to="/internal/tenants" className="text-sm text-muted-foreground hover:text-foreground">
        ← All Tenants
      </Link>
      <h1 className="text-xl font-semibold text-foreground">New Tenant</h1>
    </div>

    <div className="flex gap-8">
      {/* Vertical stepper indicator */}
      <ol className="min-w-[180px] space-y-4">
        {STEPS.map((step) => (
          <li key={step.id} className="flex items-center gap-3">
            <span
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-full text-sm font-medium',
                currentStep === step.id
                  ? 'bg-primary text-background'
                  : currentStep > step.id
                    ? 'bg-primary/20 text-primary'
                    : 'bg-card text-muted-foreground',
              )}
            >
              {step.id}
            </span>
            <span
              className={cn(
                'text-sm',
                currentStep === step.id
                  ? 'font-medium text-foreground'
                  : 'text-muted-foreground',
              )}
            >
              {step.label}
            </span>
          </li>
        ))}
      </ol>

      {/* Step content */}
      <div className="flex-1 space-y-6">
        {currentStep === 1 && <StepTenantDetails ... />}
        {currentStep === 2 && <StepLicense ... />}
        {currentStep === 3 && <StepInitialAdmin ... />}
        {currentStep === 4 && <StepReview ... />}

        {submitError && <p className="text-sm text-destructive">{submitError}</p>}

        <div className="flex justify-between">
          {currentStep > 1 ? (
            <Button variant="outline" onClick={handleBack} disabled={isSubmitting}>
              Back
            </Button>
          ) : (
            <div />
          )}
          {currentStep < 4 ? (
            <Button onClick={handleNext}>Next</Button>
          ) : (
            <Button onClick={handleSubmit} disabled={isSubmitting}>
              {isSubmitting ? 'Creating…' : 'Create Tenant'}
            </Button>
          )}
        </div>
      </div>
    </div>

    {/* Unsaved-changes guard — blocker.state driven */}
    <Dialog open={blocker.state === 'blocked'} onOpenChange={() => {}}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>Discard changes?</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          You have unsaved changes. Leaving will discard this new tenant. Continue?
        </p>
        <DialogFooter>
          <Button variant="outline" onClick={() => blocker.reset?.()}>Stay</Button>
          <Button variant="destructive" onClick={() => blocker.proceed?.()}>Leave</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
)
```

#### Step sub-components

Implement each step as an **inline function component** within the same file (same pattern as `CreateUserDialog` / `EditUserDialog` in TenantUsersPage.tsx). Do NOT split into separate files.

**Step 1 — Tenant Details:**
- Name input (`id="tenant-name"`, `placeholder="e.g. Acme Corp"`, required, `onBlur={validateStep1}`)
- Status toggle: two `<Button>` elements (Active / Suspended), same pattern as status toggle in `TenantUsersPage.tsx`
- `onChange` calls `markDirty()`

**Step 2 — License Configuration:**
- Max seats input (`id="max-seats"`, `type="number"`, `min={1}`, `placeholder="e.g. 50"`)
- Live preview paragraph: when `maxSeats.trim()` is non-empty and `maxSeatsError` is empty, show `"This tenant will allow up to ${maxSeats.trim()} active users."`. Otherwise show `"This tenant will have no seat limit."`.
- `onChange` calls `markDirty()`

**Step 3 — Initial Tenant Admin:**
- `<input type="checkbox">` (native, not Checkbox component — no group ID collision risk) for "Skip for now — designate Tenant Admin later". Wrap in `<label>` for clickability.
- When `!skipAdmin`: admin name + admin email inputs (ids: `admin-name`, `admin-email`), with inline validation
- `onChange` calls `markDirty()` (on name/email inputs)
- Checking `skipAdmin` does NOT call `markDirty()` — it doesn't represent content that would be lost

**Step 4 — Review & Confirm:**
- Three `<section>` cards, each with a heading and "Edit" (`<Button variant="ghost" size="sm">`) that calls `setCurrentStep(n)` for the corresponding step.
- Section 1 (Tenant Details): shows name and status
- Section 2 (License Configuration): shows max seats or "Unlimited"
- Section 3 (Initial Tenant Admin): shows name + email, or "Will be designated later" if `skipAdmin`
- All text is read-only (no inputs)
- `submitError` paragraph and the button row render below the review sections

---

### ESLint design-token rule (do not break)

Only semantic tokens in className:
- Use `bg-primary`, `text-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-destructive`
- Do NOT use raw Tailwind (`text-indigo-500`, `bg-zinc-800`, etc.)

The stepper indicator uses `bg-primary/20 text-primary` for completed steps — this uses semantic token with opacity modifier, which is allowed.

---

### Test file: TenantProvisioningPage.test.tsx

Use `createMemoryRouter` + `RouterProvider` (same as `GlobalNav.test.tsx`) wrapping a `QueryClientProvider`.

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TenantProvisioningPage } from './TenantProvisioningPage'

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter(
    [{ path: '*', element: <TenantProvisioningPage /> }],
    { initialEntries: ['/internal/tenants/new'] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}
```

**Tests to write:**

1. Renders step 1 (Tenant Details) by default — `screen.getByText('Tenant Details')` is present and `screen.getByLabelText('Name *')` is visible
2. Step 1 validation: clicking "Next" with empty name shows "Tenant name is required." and stays on step 1
3. Step 1 advance: fill in name, click "Next" → License Configuration step renders
4. Back navigation: advance to step 2, click "Back" → step 1 renders
5. Step 2 live preview: type `5` → preview shows "This tenant will allow up to 5 active users."
6. Step 2 blank maxSeats: no input → preview shows "This tenant will have no seat limit."
7. Step 2 invalid maxSeats: type `abc` → "Next" shows error, does not advance
8. Step 3 skip admin: check "Skip for now" → name/email fields hidden, "Next" advances
9. Step 3 validation: with skip unchecked, click "Next" with empty fields → errors shown
10. Review step content: fill all steps → step 4 shows entered name, status, maxSeats, admin name/email
11. Review "Edit" links: clicking "Edit" on Tenant Details card → goes back to step 1

**Note on `unstable_useBlocker` in tests:** The blocker requires real route navigation to fire. In `createMemoryRouter` unit tests, there is no real navigation away from the page, so `blocker.state` will always be `'unblocked'` in these tests. The blocker dialog is not tested in unit tests — it is a demo-observable behavior. Do NOT attempt to test the blocker dialog by faking router navigation in these tests; it will result in fragile test setup.

---

### Known constraints (acceptable for demo)

- `mockStore.createUser` is called directly (not via hook) for admin user creation because the `tenantId` is only known at submit time. This is the correct approach for mock scope; real API uses sequential requests or a single composite POST.
- The `unstable_useBlocker` does not intercept tab/window close — documented in the required code comment.
- Email validation uses `@`-presence only — pre-existing pattern from 5c-1b/5c-1c, deferred to Phase 2.
- "Skip admin" stores no data about the skipped admin — no partial-save or draft recovery. Acceptable for mock demo.

---

### References

- Epic 5c story 5c-4 AC: `_bmad-output/planning-artifacts/epics.md` lines 2066–2096
- UX-DR17 stepper pattern: `_bmad-output/planning-artifacts/ux-design-specification.md` line 133, 960–962
- F-3 UX flow diagram: `_bmad-output/planning-artifacts/ux-design-specification.md` lines 518–557
- `useCreateTenant`: `src/OneId.Web/src/queries/hooks/useTenants.ts` — `mutateAsync(Omit<Tenant, 'id' | 'createdAt'>)`
- `Tenant` type: `src/OneId.Web/src/mocks/types.ts` — `{ id, name, status, seatUsage: { used, max }, createdAt }`
- Route registration: `src/OneId.Web/src/routes/index.tsx`
- Pattern reference for inline sub-components: `TenantUsersPage.tsx` (`CreateUserDialog` / `EditUserDialog`)
- Pattern reference for component test wrapper: `GlobalNav.test.tsx`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `unstable_useBlocker` not exported in react-router 7.15.1 (stabilized as `useBlocker`). Used `useBlocker` and preserved required code comment verbatim.
- JSDOM sanitizes non-numeric values to `''` on `type="number"` inputs — fixed test to use `'0'` (valid number, fails `>= 1` check).
- Stepper labels ("Tenant Details", "Review & Confirm") appear on all steps — fixed step 4 `waitFor` to await unique "Create Tenant" button.

### Completion Notes List

- Created `TenantProvisioningPage` with 4-step vertical stepper: Tenant Details → License Configuration → Initial Tenant Admin → Review & Confirm.
- Per-step validation with inline errors; "Next" blocked until step validates.
- Navigation guard via `useBlocker(isDirty)` with Dialog confirm; fires only on real route navigation, not step transitions.
- Submit: `useCreateTenant().mutateAsync` then optional `mockStore.createUser` for admin user, then navigate to new tenant.
- Review step shows all entered data read-only with "Edit" jump links.
- Route `tenants/new` registered before `tenants/:tenantId` dynamic segment.
- "New Tenant" button added to TenantListPage header.
- 11 component tests: validation, step navigation, live preview, skip admin, review content, edit links.
- `npm run build`, `npm run lint` (new files clean), `npm test` (49/49 pass) all green.

### File List

- `src/OneId.Web/src/routes/internal/tenants/TenantProvisioningPage.tsx` (new)
- `src/OneId.Web/src/routes/internal/tenants/TenantProvisioningPage.test.tsx` (new)
- `src/OneId.Web/src/routes/index.tsx` (modified — import + route)
- `src/OneId.Web/src/routes/internal/tenants/TenantListPage.tsx` (modified — New Tenant button)

### Review Findings

- [x] [Review][Patch] Dialog `onOpenChange={() => {}}` traps keyboard users — wired `onOpenChange` to `blocker.reset?.()` [TenantProvisioningPage.tsx:451]
- [x] [Review][Patch] StepLicense preview shows misleading live preview for invalid values (e.g. `0`) because `maxSeatsError` is stale — preview now computes validity inline via `Number.isInteger` [TenantProvisioningPage.tsx:94-95]
- [x] [Review][Patch] `validateStep2` regex `^\d+$` rejects browser-valid `type="number"` inputs like `1e2` and `1.0` — replaced with `Number.isInteger(Number(trimmed))` check [TenantProvisioningPage.tsx:284]
- [x] [Review][Patch] Status toggle buttons missing `aria-pressed` — added `aria-pressed` to Active/Suspended buttons [TenantProvisioningPage.tsx:60-75]
- [x] [Review][Patch] Missing component tests for submit happy path (navigate to new tenant URL) and error path (`submitError` message renders) — 2 tests added, 51/51 pass [TenantProvisioningPage.test.tsx]
- [x] [Review][Patch] `setIsDirty(false)` + `navigate()` batching race — blocker fires on successful submit because React hasn't re-rendered yet; fixed with `flushSync(() => setIsDirty(false))` [TenantProvisioningPage.tsx:handleSubmit]
- [x] [Review][Defer] Admin creation misleading error: if `mockStore.createUser` throws after tenant is created, error says "Failed to create tenant" when tenant was created — demo scope, pre-existing mock pattern [TenantProvisioningPage.tsx:322-332] — deferred, pre-existing
- [x] [Review][Defer] Email validation accepts `@`, `foo@`, `@bar` — pre-existing pattern from 5c-1b, deferred to Phase 2 in dev notes [TenantProvisioningPage.tsx:298] — deferred, pre-existing
- [x] [Review][Defer] `blocker.reset?.()` / `blocker.proceed?.()` optional chaining silently no-ops if RR7 omits these methods — unstable API caveat, code comment documents the risk [TenantProvisioningPage.tsx:460-461] — deferred, pre-existing
- [x] [Review][Defer] Route order fragility: no guard prevents `tenants/:tenantId` being listed before `tenants/new` during maintenance [routes/index.tsx:36] — deferred, pre-existing
- [x] [Review][Defer] `parseInt` silently produces imprecise integer for seat counts exceeding `Number.MAX_SAFE_INTEGER` — demo scope [TenantProvisioningPage.tsx:284] — deferred, pre-existing
- [x] [Review][Defer] Tests use `fireEvent` throughout; blur-triggered validation paths not exercised — pre-existing test pattern across codebase [TenantProvisioningPage.test.tsx] — deferred, pre-existing

## Change Log

- 2026-05-24: Implemented F-3 Tenant Provisioning Stepper — 4-step guided form with validation, unsaved-changes guard, live seat preview, skip-admin option, and review step. Added entry point from Tenants list page.
