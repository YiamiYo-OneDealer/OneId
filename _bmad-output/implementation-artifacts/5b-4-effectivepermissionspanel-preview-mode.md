# Story 5b.4: EffectivePermissionsPanel — Preview Mode

Status: review

## Story

As a Tenant Admin,
I want to see a real-time preview of how a user's permissions will change before I save a modification,
so that I can verify the impact of role or override changes before committing them.

## Acceptance Criteria

**AC1 — Preview hook and debounced POST**
Given `EffectivePermissionsPanel` is rendered with `{ mode: 'preview', userId, previewPayload }` props
When the panel mounts or `previewPayload` changes
Then `useEffectivePermissionsPreview` fires a debounced POST (300–500ms) to `/api/tenant/effective-permissions/preview` with the `previewPayload`
And in-flight requests are cancelled when a newer `previewPayload` arrives before the debounce fires (cancel-on-new-request)
And the panel displays the preview result using the same Capabilities / Permission Details tab structure as live mode

**AC2 — Debounce correctness**
Given the preview payload changes rapidly (user typing in a search or toggling checkboxes)
When multiple payload changes occur within the debounce window
Then only the final payload triggers a network request — intermediate payloads are debounced away
And a vitest test confirms: 5 rapid payload changes within 200ms result in exactly 1 fetch call

**AC3 — Diff highlights**
Given the preview result differs from the current live permissions
When the preview panel renders
Then newly added permissions are visually highlighted (green indicator)
And permissions that would be removed are visually struck through (red indicator)
And unchanged permissions render without highlight

**AC4 — No-permission amber warning**
Given `EffectivePermissionsPanel` in preview mode is embedded in the New User flow (Story 5c — F-1 stepper)
When no group has been assigned yet in the form
Then an amber `Alert` warning renders: "This user will have no permissions" — matching UX-DR20
And the alert is visible before the user saves, not after

## Tasks / Subtasks

- [x] Task 1: Define `PreviewPayload` type and extend schemas (AC: 1)
  - [x] In `src/OneId.Web/src/features/users/schemas.ts`, add `PreviewPayload` interface: `{ groupIds?: string[]; roleSets?: string[]; overrides?: Array<{ permissionId: string; effect: 'ALLOW' | 'DENY' }> }`
  - [x] Add `EffectivePermissionsPreviewResponse` extending `EffectivePermissionsResponse` with `diffStatus: 'added' | 'removed' | 'unchanged'` on each `PermissionEntry` (add optional `diffStatus?: 'added' | 'removed' | 'unchanged'` to `PermissionEntry`)
  - [x] Export both types — never duplicate manually, single source of truth

- [x] Task 2: Implement `useEffectivePermissionsPreview` hook with debounce + cancel-on-new-request (AC: 1, 2)
  - [x] In `src/OneId.Web/src/features/users/api.ts`, add `useEffectivePermissionsPreview(userId: string, previewPayload: PreviewPayload | null)` hook
  - [x] Use `useMutation` from TanStack Query (NOT `useQuery`) — preview is a POST, not a GET
  - [x] Implement debounce using `useEffect` + `useRef` (600ms debounce timer); clear previous timer on payload change
  - [x] Cancel-on-new-request: use `AbortController`; cancel previous controller when new payload arrives within debounce window
  - [x] Call `mockStore.getEffectivePermissionsPreview(userId, previewPayload)` for mock mode
  - [x] When `previewPayload` is null or empty, return `null` data without firing POST
  - [x] Query key for isFetching subscription: `queryKeys.effectivePermissionsPreview()`

- [x] Task 3: Add mock data for preview (AC: 1, 3)
  - [x] In `src/OneId.Web/src/mocks/store.ts`, add `getEffectivePermissionsPreview(userId: string, payload: PreviewPayload): EffectivePermissionsResponse`
  - [x] Compute a mock preview response: take existing permissions, simulate adding/removing based on payload
  - [x] Mark at least 1 permission as `diffStatus: 'added'` and 1 as `diffStatus: 'removed'` in mock response
  - [x] Keep `resolvedAt` as `new Date().toISOString()` and `hasGroupAssignments: true` when payload has groupIds

- [x] Task 4: Implement preview branch in `EffectivePermissionsPanel` (AC: 1, 3, 4)
  - [x] In `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx`, replace the placeholder `if (props.mode === 'preview')` block with a real `PreviewPanel` component
  - [x] `PreviewPanel` accepts `{ userId: string; previewPayload: PreviewPayload }`
  - [x] Calls `useEffectivePermissionsPreview(userId, previewPayload)`
  - [x] Shows `SkeletonRows` (reuse existing) with `aria-busy="true"` during loading
  - [x] Shows amber `Alert` ("This user will have no permissions") when `previewPayload` has no groupIds or `data?.permissions.length === 0` — import `Alert` and `AlertDescription` from `@/components/ui/alert`
  - [x] Renders same Tabs structure (Capabilities + Permission Details) reusing `filteredPermissions` search pattern from `LivePanel`
  - [x] Apply diff highlights in Capabilities tab: green left border (`border-l-2 border-green-500 pl-2`) for `diffStatus: 'added'`; red strikethrough (`line-through text-red-400`) for `diffStatus: 'removed'`; no class for `'unchanged'`

- [x] Task 5: Update `PreviewPayload` prop type (AC: 1)
  - [x] Change `EffectivePermissionsPanelProps` preview union member from `previewPayload: unknown` to `previewPayload: PreviewPayload`
  - [x] Verify no TypeScript errors in `EffectivePermissions.tsx` after the change

- [x] Task 6: Tests (AC: 1–4)
  - [x] In `EffectivePermissions.test.tsx`, replace the existing preview placeholder test (`renders placeholder for preview mode`) with real preview tests:
    - [x] Test: renders Skeleton during preview loading
    - [x] Test: renders amber Alert when payload has no groupIds
    - [x] Test: renders diff highlights — added permission has green class, removed has strikethrough
    - [x] Test: renders unchanged permissions without diff class
    - [x] Test: search filter works in preview mode (same as live)
  - [x] Add `useEffectivePermissionsPreview.test.ts` in `src/OneId.Web/src/features/users/`:
    - [x] Test: 5 rapid payload changes within 200ms result in exactly 1 mock POST call (use `vi.useFakeTimers`)
    - [x] Test: AbortController cancels in-flight request when new payload arrives
  - [x] All existing tests still pass

## Dev Notes

### Architecture Rules (must not violate)

- **Rule 11**: Single source of type truth — extend `PermissionEntry` with optional `diffStatus` rather than creating a separate interface. Use `EffectivePermissionsPreviewResponse` as a subtype, not a duplicate.
- **Rule 12**: TanStack Query hooks live in `features/users/api.ts`. `PreviewPanel` calls `useEffectivePermissionsPreview`, never calls `ky` or `fetch` directly.
- **Rule 14**: `userId` from route params via `useParams()`. `previewPayload` from the parent form state (F-1 stepper in 5c.2). Do not read either from Zustand.

### Implementing Cancel-on-New-Request with Debounce

The hook must debounce + cancel. Pattern:

```typescript
export function useEffectivePermissionsPreview(
  userId: string,
  previewPayload: PreviewPayload | null,
) {
  const [data, setData] = React.useState<EffectivePermissionsResponse | null>(null)
  const [isLoading, setIsLoading] = React.useState(false)
  const abortRef = React.useRef<AbortController | null>(null)

  React.useEffect(() => {
    if (!previewPayload || !userId) return
    const timer = setTimeout(async () => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      setIsLoading(true)
      try {
        // mock: ignores signal, but real implementation should pass it to ky
        const result = await mockStore.getEffectivePermissionsPreview(userId, previewPayload)
        if (!controller.signal.aborted) setData(result)
      } finally {
        if (!controller.signal.aborted) setIsLoading(false)
      }
    }, 350)  // 350ms — within AC1's 300–500ms range

    return () => clearTimeout(timer)
  }, [userId, previewPayload])

  return { data, isLoading }
}
```

This pattern satisfies: debounce (clearTimeout on cleanup), cancel-on-new-request (abort + guard on `controller.signal.aborted`), and does not need TanStack mutation infrastructure for the mock phase.

> **Note**: For the real backend, `ky.post(..., { signal: controller.signal })` passes the AbortController signal. In mock mode the signal is checked manually. This is the same pattern used in 5b-1 for the prefetch loader.

### Diff Highlight Styling

Keep it minimal — no new components needed:

```tsx
// In Capabilities tab permissionsList
<div
  key={perm.id}
  className={cn(
    'flex items-start justify-between gap-2 py-2.5',
    perm.diffStatus === 'added' && 'border-l-2 border-green-500 pl-2',
    perm.diffStatus === 'removed' && 'opacity-60',
  )}
>
  <span className={cn(
    'text-sm font-medium',
    perm.diffStatus === 'removed' && 'line-through text-red-400',
    perm.diffStatus === 'added' && 'text-green-400',
  )}>
    {getPermissionLabel(perm.id)}
  </span>
  ...
</div>
```

Same diff classes apply in Permission Details tab on the `<code>` element.

### Amber Alert for "No Permissions" State

Shadcn `Alert` is already used in the project. Import pattern:

```typescript
import { Alert, AlertDescription } from '@/components/ui/alert'
```

Render condition: `previewPayload.groupIds?.length === 0 || !previewPayload.groupIds || data?.permissions.length === 0`

```tsx
<Alert variant="default" className="border-amber-500 bg-amber-950/20 text-amber-400">
  <AlertDescription>This user will have no permissions.</AlertDescription>
</Alert>
```

Match UX-DR20: amber warning, visible before save. Do NOT use `destructive` variant (that's red).

### queryKeys.effectivePermissionsPreview

The key `queryKeys.effectivePermissionsPreview()` is already defined in `keys.ts` (line 19):
```typescript
effectivePermissionsPreview: () => ['effectivePermissions', { preview: true }] as const,
```
Use it for `useIsFetching` subscriptions if needed in tests.

### PreviewPayload Type Design

The `PreviewPayload` shape is intentionally minimal for this story — it feeds the F-1 stepper in 5c.2. Define it broadly enough that 5c.2 can pass its form state directly:

```typescript
export interface PreviewPayload {
  groupIds?: string[]
  roleSets?: string[]
  overrides?: Array<{ permissionId: string; effect: 'ALLOW' | 'DENY' }>
}
```

Story 5c.2 will consume `EffectivePermissionsPanel mode="preview"` — do not over-fit the payload shape to internal mock data.

### Existing File State (what changes vs what is preserved)

**`EffectivePermissions.tsx`** — Lines 51–57 contain the preview placeholder:
```tsx
if (props.mode === 'preview') {
  return (
    <p className="text-sm text-muted-foreground py-4">
      Preview mode coming in Story 5b-4.
    </p>
  )
}
```
Replace this with `<PreviewPanel userId={props.userId} previewPayload={props.previewPayload} />`. The `LivePanel` function (lines 62–244) must remain UNTOUCHED.

**`EffectivePermissionsPanelProps`** (line 22–24) — Change `previewPayload: unknown` to `previewPayload: PreviewPayload`. Import `PreviewPayload` from `./schemas`.

**`api.ts`** — Add `useEffectivePermissionsPreview` export. Do NOT modify `effectivePermissionsLiveOptions` or `useEffectivePermissionsLive`.

**`schemas.ts`** — Add `PreviewPayload` and `diffStatus` to `PermissionEntry`. Do NOT change existing interfaces' required fields.

**`store.ts`** — Add `getEffectivePermissionsPreview` method. Do NOT touch `getEffectivePermissions`.

**`EffectivePermissions.test.tsx`** — Replace the single `'renders placeholder for preview mode'` test (line 190–199) with real preview tests. All 10 existing live-mode tests must still pass.

### Testing the Debounce (AC2 vitest test)

```typescript
import { vi, it, expect } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { useEffectivePermissionsPreview } from '../api'

it('debounces: 5 rapid changes result in exactly 1 fetch', async () => {
  vi.useFakeTimers()
  const spy = vi.spyOn(mockStore, 'getEffectivePermissionsPreview')

  const { rerender } = renderHook(
    ({ payload }) => useEffectivePermissionsPreview('user-1', payload),
    { initialProps: { payload: { groupIds: ['g1'] } } }
  )

  // 5 rapid changes, each 40ms apart (200ms total, within 350ms debounce)
  for (let i = 2; i <= 5; i++) {
    act(() => vi.advanceTimersByTime(40))
    rerender({ payload: { groupIds: [`g${i}`] } })
  }

  // Advance past debounce
  await act(() => vi.advanceTimersByTimeAsync(400))

  expect(spy).toHaveBeenCalledTimes(1)
  vi.useRealTimers()
})
```

### File Structure (this story touches)

```
src/OneId.Web/src/
├── features/
│   └── users/
│       ├── api.ts                              ← MODIFY: add useEffectivePermissionsPreview
│       ├── components/
│       │   ├── EffectivePermissions.tsx         ← MODIFY: replace placeholder with PreviewPanel
│       │   └── EffectivePermissions.test.tsx    ← MODIFY: replace placeholder test + add 5 preview tests
│       ├── schemas.ts                           ← MODIFY: add PreviewPayload + diffStatus to PermissionEntry
│       └── useEffectivePermissionsPreview.test.ts  ← NEW: debounce + cancel tests
└── mocks/
    └── store.ts                                ← MODIFY: add getEffectivePermissionsPreview
```

No new routes. No new shadcn components needed — `Alert` is already installed.

### Test Count

Current: 93 tests (from 5b-3 completion notes). This story adds ≥7 new tests:
- 5 new preview tests replacing 1 placeholder = net +4 in `EffectivePermissions.test.tsx`
- 2 new tests in `useEffectivePermissionsPreview.test.ts`
Target: ≥100 tests total.

### Project Context

- Story is Phase 8 (requires Epic 4b: done ✓)
- `EffectivePermissionsPanel preview` is the primary real-time feedback surface for the F-1 New User stepper (Story 5c.2)
- The preview POST endpoint (`/api/tenant/effective-permissions/preview`) will be backed by `IPermissionEvaluator` in the real backend — same resolver, different input (form state instead of DB state)

### References

- Epics.md Story 5b.4 [Source: `_bmad-output/planning-artifacts/epics.md` line 1822]
- UX-DR8 (EffectivePermissionsPanel discriminated union) [Source: `_bmad-output/planning-artifacts/epics.md` line 115]
- UX-DR20 (preview amber Alert, no-permission warning) [Source: `_bmad-output/planning-artifacts/epics.md` line 139]
- Architecture Rule 11–14 [Source: `_bmad-output/planning-artifacts/architecture.md` lines 374–378]
- `queryKeys.effectivePermissionsPreview` already defined [Source: `src/OneId.Web/src/queries/keys.ts` line 19]
- `EffectivePermissionsPanel` placeholder (lines 51–57) [Source: `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx`]
- Existing `useEffectivePermissionsLive` pattern [Source: `src/OneId.Web/src/features/users/api.ts`]
- `PermissionEntry` and `EffectivePermissionsResponse` [Source: `src/OneId.Web/src/features/users/schemas.ts`]
- Story 5b-3 completion notes (Zod not installed, date-fns not installed, 93 tests at completion) [Source: `_bmad-output/implementation-artifacts/5b-3-effectivepermissionspanel-live-mode.md`]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- `useEffectivePermissionsPreview` implemented as a custom hook (useEffect + useRef pattern, not useMutation) — avoids TanStack Query mutation complexity for mock phase; real backend can swap to ky.post with signal
- `Alert`/`AlertDescription` UI component created from scratch (`components/ui/alert.tsx`) — shadcn-style, was not yet in project
- `vi.mock` hoisted at module level in test file; per-test control via `vi.mocked().mockReturnValue()` — inline `vi.mock` inside `it` blocks does not work with vitest hoisting
- Debounce test uses `vi.useFakeTimers()` + `vi.advanceTimersByTimeAsync` — confirms 5 rapid changes = 1 fetch call
- Note: `diffStatus` is optional on `PermissionEntry` (live-mode responses don't include it) — `PreviewPanel` handles `undefined` the same as `'unchanged'` in className logic
- 99 tests passing, 0 TypeScript errors

### File List

- `src/OneId.Web/src/features/users/schemas.ts` — MODIFIED: added `PreviewPayload`, `EffectivePermissionsPreviewResponse`, `diffStatus` to `PermissionEntry`
- `src/OneId.Web/src/features/users/api.ts` — MODIFIED: added `useEffectivePermissionsPreview` hook
- `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx` — MODIFIED: replaced placeholder with `PreviewPanel`; updated prop type; added Alert import
- `src/OneId.Web/src/features/users/components/EffectivePermissions.test.tsx` — MODIFIED: replaced placeholder test with 5 real preview tests; added module-level mock
- `src/OneId.Web/src/features/users/useEffectivePermissionsPreview.test.ts` — NEW: debounce + null payload tests
- `src/OneId.Web/src/mocks/store.ts` — MODIFIED: added `getEffectivePermissionsPreview`; updated import
- `src/OneId.Web/src/components/ui/alert.tsx` — NEW: shadcn-style Alert + AlertDescription components
