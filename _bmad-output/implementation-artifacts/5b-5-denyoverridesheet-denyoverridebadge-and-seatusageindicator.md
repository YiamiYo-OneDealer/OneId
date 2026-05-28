# Story 5b.5: DenyOverrideSheet, DenyOverrideBadge, and SeatUsageIndicator

Status: review

## Story

As a Tenant Admin,
I want to review and remove DENY overrides inline and see seat usage at a glance in the Users section header,
so that I can resolve authorization exceptions and monitor capacity without leaving the current context.

## Acceptance Criteria

**AC1 — DenyOverrideBadge opens DenyOverrideSheet**
Given `DenyOverrideBadge` renders inline next to a permission in `EffectivePermissionsPanel`
When a user clicks the badge
Then `DenyOverrideSheet` opens as a side sheet showing: override type, reason, applied-by user, date applied, and optional expiry date
And the badge has `aria-label="DENY override on [permission label] — click to review"`
And `DenyOverrideBadge` vitest-axe test passes: `expect(container).toHaveNoViolations()`

**AC2 — Force Re-authenticate when permitted**
Given `DenyOverrideSheet` is open
When the Tenant Admin has the `od.admin.users.revoke` permission
Then "Force Re-authenticate" button is visible and enabled
And clicking it calls `POST /api/tenant/users/{userId}/tokens/revoke` and fires the revocation toast: "User must re-authenticate — changes are immediate"

**AC3 — Force Re-authenticate hidden when not permitted**
Given the Tenant Admin does NOT have `od.admin.users.revoke`
When `DenyOverrideSheet` renders
Then "Force Re-authenticate" button is hidden entirely (tier never has access = hidden, per UX-DR15)
And this is distinct from partial access: if the permission check returns `isLoading`, the button is disabled — never shown-then-failed on click

**AC4 — Remove Override destructive action**
Given "Remove Override" destructive button is clicked in `DenyOverrideSheet`
When the Tenant Admin confirms the Medium-tier confirmation Dialog ("Remove this DENY override?")
Then `DELETE /api/tenant/users/{userId}/overrides/{overrideId}` fires
And the sheet closes, the `DenyOverrideBadge` disappears from the panel, and a propagation-honest toast fires: "Changes effective within 5 minutes."

**AC5 — SeatUsageIndicator normal/warning/limit states**
Given `SeatUsageIndicator` renders in the Users section header
When seat usage is below 80%
Then the indicator shows "N of M seats used" in zinc-400 with no icon
And at ≥80% the color changes to amber-400 with a warning icon
And at 100% the color changes to red-400 with an alert icon AND the "Create User" primary CTA is disabled with tooltip: "Seat limit reached. Contact your administrator to expand your license."
And the label is screen-reader safe: "42 of 50 seats used" — NOT "42/50" (split numbers break screen reader announcement)
And `SeatUsageIndicator` vitest-axe test passes for all three states (normal, warning, limit-reached)

## Tasks / Subtasks

- [x] Task 1: Add `DenyOverride` data model and mock methods (AC: 1, 4)
  - [x] In `src/OneId.Web/src/features/users/schemas.ts`, add `DenyOverride` interface
  - [x] In `src/OneId.Web/src/mocks/store.ts`, add `overrides` to state + `getDenyOverridesForUser`, `deleteOverride`, `revokeUserTokens` methods

- [x] Task 2: Add query keys for overrides (AC: 1, 4)
  - [x] In `src/OneId.Web/src/queries/keys.ts`, add `userOverrides` key

- [x] Task 3: Add API hooks for overrides and revocation (AC: 2, 4)
  - [x] In `src/OneId.Web/src/features/users/api.ts`, add `useUserOverrides`, `useDeleteOverride`, `useRevokeUserTokens`

- [x] Task 4: Implement `DenyOverrideSheet` component (AC: 1–4)
  - [x] Created `src/OneId.Web/src/components/shared/DenyOverrideSheet.tsx`
  - [x] Shows override details, Force Re-authenticate (permission-gated), Remove Override (with confirmation dialog)
  - [x] `useHasPermission` three-state: hidden when `!permitted`, disabled when `isLoading`, enabled when `permitted`

- [x] Task 5: Wire `DenyOverrideBadge` → `DenyOverrideSheet` in `EffectivePermissionsPanel` (AC: 1)
  - [x] `LivePanel` now calls `useUserOverrides`, has `selectedOverride` state, passes `onReview` to both badge usages
  - [x] `<DenyOverrideSheet>` rendered at bottom of `LivePanel`
  - [x] `PreviewPanel` untouched

- [x] Task 6: Implement `SeatUsageIndicator` component (AC: 5)
  - [x] Created `src/OneId.Web/src/components/shared/SeatUsageIndicator.tsx`
  - [x] Three states: normal (zinc), warning (amber + TriangleAlert), limit (red + AlertCircle)
  - [x] `isSeatLimitReached` helper exported

- [x] Task 7: Integrate `SeatUsageIndicator` into the internal admin `TenantUsersPage` (AC: 5)
  - [x] `TenantUsersPage` now shows `SeatUsageIndicator` in header
  - [x] "Create User" CTA disabled with tooltip when seat limit reached

- [x] Task 8: Tests (AC: 1–5)
  - [x] `DenyOverrideSheet.test.tsx`: 6 tests (details render, no-reason fallback, hidden/disabled Force Re-auth, confirm dialog, axe)
  - [x] `SeatUsageIndicator.test.tsx`: 9 tests (3 state tests, unlimited, aria-label, 2 helper tests, 3 axe states)
  - [x] All 5 existing `DenyOverrideBadge.test.tsx` tests still pass

## Dev Notes

### Architecture Rules (must not violate)

- **Rule 11**: Single source of type truth — `DenyOverride` interface belongs in `features/users/schemas.ts` (not mocks). Mock store imports from there.
- **Rule 12**: TanStack Query hooks live in `features/users/api.ts`. `DenyOverrideSheet` calls `useUserOverrides`, `useDeleteOverride`, `useRevokeUserTokens` — never calls `ky` or `fetch` directly.
- **Rule 14**: `userId` from route params or props. `tenantId` from `useTenantStore`. Do not read either from Zustand in components that don't own the page context.
- **UX-DR15**: Permission-gated buttons follow three-state rule: hidden (tier never has access), disabled (partial/loading), or visible+enabled. "Force Re-authenticate" is hidden when `!permitted` (Tenant Admins without `od.admin.users.revoke` simply never see it). It is disabled only during `isLoading`.

### DenyOverrideSheet Pattern (mirrors AuditEventSheet)

```tsx
// src/OneId.Web/src/components/shared/DenyOverrideSheet.tsx
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet'
import { Dialog, DialogContent, DialogHeader, DialogTitle,
         DialogDescription, DialogFooter } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { useHasPermission } from '@/hooks/useHasPermission'
import { useDeleteOverride, useRevokeUserTokens } from '@/features/users/api'
import { useFormMutation } from '@/hooks/useFormMutation'
import type { DenyOverride } from '@/features/users/schemas'

export function DenyOverrideSheet({
  userId, tenantId, override, onClose,
}: {
  userId: string
  tenantId: string
  override: DenyOverride | null
  onClose: () => void
}) {
  const [confirmOpen, setConfirmOpen] = React.useState(false)
  const { permitted, isLoading: permLoading } = useHasPermission('od.admin.users.revoke')
  const deleteOverride = useDeleteOverride(tenantId, userId)
  const revokeTokens = useRevokeUserTokens(userId)

  const revokeAction = useFormMutation({
    mutationFn: () => revokeTokens.mutateAsync(),  // wire to useMutation
    messages: { success: '', error: 'Revocation failed.', forceRevoke: true },
    onSuccess: onClose,
  })
  // NOTE: useFormMutation wraps useMutation — use the deleteOverride mutation
  //       directly with useFormMutation wrapper for propagation-honest toast

  return (
    <Sheet open={!!override} onOpenChange={(open) => { if (!open) onClose() }}>
      <SheetContent side="right" className="w-[480px] overflow-y-auto">
        ...
      </SheetContent>
    </Sheet>
  )
}
```

> **Important**: `useDeleteOverride` and `useRevokeUserTokens` should be plain `useMutation` hooks in `api.ts`. Wire them through `useFormMutation` in the Sheet component for toast behavior.

### SeatUsageIndicator — Full Implementation

```tsx
// src/OneId.Web/src/components/shared/SeatUsageIndicator.tsx
import { TriangleAlert, AlertCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

export function isSeatLimitReached(used: number, max: number | null): boolean {
  return max !== null && used >= max
}

function getSeatState(used: number, max: number | null) {
  if (max === null) return 'unlimited'
  const pct = used / max
  if (pct >= 1) return 'limit'
  if (pct >= 0.8) return 'warning'
  return 'normal'
}

export function SeatUsageIndicator({ used, max }: { used: number; max: number | null }) {
  const state = getSeatState(used, max)

  const label = max === null
    ? `${used} seats used`
    : `${used} of ${max} seats used`

  return (
    <span
      aria-label={label}
      className={cn(
        'inline-flex items-center gap-1 text-xs font-medium',
        state === 'normal' && 'text-zinc-400',
        state === 'warning' && 'text-amber-400',
        state === 'limit' && 'text-red-400',
        state === 'unlimited' && 'text-zinc-400',
      )}
    >
      {state === 'warning' && <TriangleAlert className="h-3.5 w-3.5" aria-hidden="true" />}
      {state === 'limit' && <AlertCircle className="h-3.5 w-3.5" aria-hidden="true" />}
      <span>{label}</span>
    </span>
  )
}
```

### Mock State for Overrides

Add to `state` in `store.ts`:

```ts
const state = {
  // ...existing...
  overrides: [
    {
      id: 'override-1',
      permissionId: 'od.users.deactivate',
      overrideType: 'DENY' as const,
      reason: 'Pending compliance review',
      appliedByName: 'System Admin',
      appliedAt: '2026-05-01T10:00:00.000Z',
      expiresAt: undefined,
    },
  ] as DenyOverride[],
}
```

`getDenyOverridesForUser` ignores `userId` in mock mode (returns the demo list for any user that has `od.users.deactivate` in their permissions).

`deleteOverride` removes by `overrideId` from `state.overrides`.

### Wire DenyOverrideBadge with onReview in LivePanel

Current state (`EffectivePermissions.tsx` line 339):
```tsx
{perm.isDenied && (
  <DenyOverrideBadge permissionLabel={getPermissionLabel(perm.id)} />
)}
```

Change to pass `onReview`:
```tsx
{perm.isDenied && (
  <DenyOverrideBadge
    permissionLabel={getPermissionLabel(perm.id)}
    onReview={() => {
      const ov = overrides?.find((o) => o.permissionId === perm.id) ?? null
      setSelectedOverride(ov)
    }}
  />
)}
```

Same change in both `capabilities` tab (line 339) and `details` tab (line 359). The `overrides` array comes from `useUserOverrides(tenantId, userId).data`.

### Existing File State

**`DenyOverrideBadge.tsx`** — Already implements the interactive button with `onReview` prop (lines 9–18). This story wires up `onReview` from the parent — no changes to `DenyOverrideBadge.tsx` itself.

**`DenyOverrideBadge.test.tsx`** — Already has 5 passing tests for both interactive and non-interactive states. No changes needed.

**`EffectivePermissions.tsx`** — Modify `LivePanel` only:
- Add `useUserOverrides` call
- Add `selectedOverride` state + `setSelectedOverride`
- Pass `onReview` to each `DenyOverrideBadge` in both tabs
- Add `<DenyOverrideSheet>` after the closing main `</div>`
- `PreviewPanel` is UNTOUCHED

**`TenantUsersPage.tsx`** — Modify header area only (lines 415–419). Add `useTenant` call. Add `SeatUsageIndicator`. Disable "Create User" when limit reached.

**`features/users/api.ts`** — Add 3 new exports: `useUserOverrides`, `useDeleteOverride`, `useRevokeUserTokens`. Do NOT modify `useEffectivePermissionsLive` or `useEffectivePermissionsPreview`.

**`features/users/schemas.ts`** — Add `DenyOverride` interface. Do NOT change existing interfaces.

**`queries/keys.ts`** — Add `userOverrides` key. Do NOT change existing keys.

**`mocks/store.ts`** — Add `overrides` to `state`, add 3 new methods. Do NOT touch `getEffectivePermissions` or `getEffectivePermissionsPreview`.

### Test Count

Current: 99 tests (from 5b-4 completion notes). This story adds ≥10 new tests:
- `DenyOverrideSheet.test.tsx`: 5 tests (new file)
- `SeatUsageIndicator.test.tsx`: 5 tests (new file, includes 3 axe states)

Target: ≥109 tests total.

### useFormMutation Note for DenyOverrideSheet

`useFormMutation` wraps `useMutation` and handles toasts. The pattern for calling it in `DenyOverrideSheet`:

```ts
const deleteOverrideMutation = useFormMutation({
  mutationFn: (overrideId: string) => {
    mockStore.deleteOverride(userId, overrideId)
    // invalidation via onSuccess callback
  },
  messages: {
    success: 'Override removed.',
    error: 'Failed to remove override.',
    propagationNote: true,
  },
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
    queryClient.invalidateQueries({ queryKey: queryKeys.userOverrides(tenantId, userId) })
    setConfirmOpen(false)
    onClose()
  },
})
```

Use `useQueryClient()` from `@tanstack/react-query` for invalidation in `onSuccess`.

### Project Context

- Story is Phase 8 (requires Epic 4b: done ✓)
- `DenyOverrideBadge` already has `onReview` prop support — this story wires it up end-to-end
- Seat usage data already exists on `Tenant` mock fixtures (8/25, 3/10, 12/20 across 3 demo tenants)
- The `od.admin.users.revoke` permission is already registered in `permissions/registry.ts` line 57
- `queryKeys.seatUsage` already exists in `keys.ts` line 21 — not used in this story (SeatUsageIndicator reads from `useTenant`, not a separate seatUsage query)
- Internal admin `TenantUsersPage` is the integration target for `SeatUsageIndicator` (tenant admin `/tenant/users` is still a stub); the component is designed for reuse once the tenant admin page is built

### References

- Epics.md Story 5b.5 — DenyOverrideSheet, DenyOverrideBadge, SeatUsageIndicator ACs
- UX-DR15 — permission-gate three-state rule (hidden / disabled / enabled)
- `DenyOverrideBadge.tsx` — `onReview` prop already implemented, just needs wiring
- `AuditEventSheet.tsx` — Sheet pattern reference for `DenyOverrideSheet`
- `useFormMutation.ts` — `forceRevoke` and `propagationNote` options
- `useHasPermission.ts` — `{ permitted, isLoading }` return shape
- `EffectivePermissions.tsx` lines 338–360 — existing `DenyOverrideBadge` usages in `LivePanel`
- `TenantUsersPage.tsx` lines 414–419 — header area to modify for `SeatUsageIndicator`
- `mocks/store.ts` lines 205–207 — `DEMO_DENY_IDS` defines which permission is DENY-overridden
- `queries/keys.ts` line 21 — `seatUsage` key already exists (not used in this story)
- Story 5b-4 completion: 99 tests, 0 TypeScript errors

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- `DenyOverride` type placed in `features/users/schemas.ts` (not mocks) per Rule 11 — single source of truth; mock store imports from there
- `useFormMutation` used in `DenyOverrideSheet` to wrap both remove-override and force-revoke mutations for consistent toast behavior
- `DenyOverrideSheet` uses `useDeleteOverride` + `useRevokeUserTokens` as plain mutations, wired through `useFormMutation` in the sheet for toasts
- Force Re-authenticate is hidden (`!permitted && !permLoading`), disabled (`!permitted && permLoading`), or enabled (`permitted`) — satisfies UX-DR15 three-state rule
- `SeatUsageIndicator` uses `aria-label` on outer `<span>` for screen reader; inner `<span>{label}</span>` for visible text — tests use `getByLabelText` not `getByText` to target the outer element
- Test count: 115 passing (up from 99), 0 TypeScript errors

### File List

- `src/OneId.Web/src/features/users/schemas.ts` — MODIFIED: added `DenyOverride` interface
- `src/OneId.Web/src/queries/keys.ts` — MODIFIED: added `userOverrides` key
- `src/OneId.Web/src/mocks/store.ts` — MODIFIED: added `overrides` to state + 3 new methods
- `src/OneId.Web/src/features/users/api.ts` — MODIFIED: added `useUserOverrides`, `useDeleteOverride`, `useRevokeUserTokens`
- `src/OneId.Web/src/components/shared/DenyOverrideSheet.tsx` — NEW
- `src/OneId.Web/src/components/shared/DenyOverrideSheet.test.tsx` — NEW
- `src/OneId.Web/src/components/shared/SeatUsageIndicator.tsx` — NEW
- `src/OneId.Web/src/components/shared/SeatUsageIndicator.test.tsx` — NEW
- `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx` — MODIFIED: wired `onReview` in `LivePanel`, added `DenyOverrideSheet`
- `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx` — MODIFIED: added `SeatUsageIndicator` + seat limit CTA disable

- `src/OneId.Web/src/features/users/schemas.ts` — MODIFY: add `DenyOverride` interface
- `src/OneId.Web/src/queries/keys.ts` — MODIFY: add `userOverrides` key
- `src/OneId.Web/src/mocks/store.ts` — MODIFY: add `overrides` state + `getDenyOverridesForUser`, `deleteOverride`, `revokeUserTokens`
- `src/OneId.Web/src/features/users/api.ts` — MODIFY: add `useUserOverrides`, `useDeleteOverride`, `useRevokeUserTokens`
- `src/OneId.Web/src/components/shared/DenyOverrideSheet.tsx` — NEW
- `src/OneId.Web/src/components/shared/DenyOverrideSheet.test.tsx` — NEW
- `src/OneId.Web/src/components/shared/SeatUsageIndicator.tsx` — NEW
- `src/OneId.Web/src/components/shared/SeatUsageIndicator.test.tsx` — NEW
- `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx` — MODIFY: wire `onReview` in `LivePanel`, add `DenyOverrideSheet`
- `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx` — MODIFY: add `SeatUsageIndicator` + seat limit CTA disable
