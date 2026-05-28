# Story 5b.2: Permission Label Map and DisabledButtonWithTooltip

**Status:** review
**Epic:** 5b — Permission & Override UX
**Story ID:** 5b-2
**Prerequisite:** Story 5b-1 complete ✓ (`useHasPermission`, `useFormMutation`, `sonner` all in place)

---

## Story

As a developer,
I want a frontend permission label registry and a `DisabledButtonWithTooltip` pattern available before any permission-gated UI is built,
So that permission IDs always display human-readable labels and disabled states communicate their reason accessibly.

---

## Acceptance Criteria

1. **Given** `permissions/registry.ts` is created
   **When** it is committed
   **Then** it exports `PERMISSION_GROUPS: PermissionGroup[]` structured as groups by domain, each containing `{ id: string, label: string, description?: string }` entries covering every `od.*` constant from the backend `Permissions` class
   **And** `PERMISSION_LABELS` is a flat lookup derived from `PERMISSION_GROUPS` — never maintained separately
   **And** `getPermissionLabel(id: string)` returns the label for a known ID or the raw `od.` ID as fallback — never blank or undefined

2. **Given** a frontend vitest test for the permission registry runs
   **When** it executes
   **Then** it asserts every `const string` exported in the backend `Permissions` class has a matching entry in `PERMISSION_GROUPS`
   **And** the test fails if a backend permission constant is added without a corresponding frontend label entry — mirroring `PermissionCatalogSyncTests.cs`

3. **Given** `DisabledButtonWithTooltip` is implemented
   **When** a button is permission-blocked
   **Then** the button is wrapped in a `<span>` for pointer-event capture, has `aria-disabled="true"` alongside native `disabled`, uses `useId()` for SSR-safe IDs, and has `aria-describedby` pointing to the `TooltipContent`
   **And** the permission-block tooltip reads: "You don't have permission to [action]. Contact your administrator."
   **And** the precondition-block tooltip reads the specific blocker with a concrete next step (e.g. "No roles assigned to this group. Add a role first.")

4. **Given** a `DisabledButtonWithTooltip` vitest-axe test runs
   **When** the component is rendered in both permission-block and precondition-block states
   **Then** `expect(container).toHaveNoViolations()` passes for both states
   **And** a keyboard-only user can focus the disabled button wrapper and read the tooltip via `aria-describedby`

---

## Tasks

- [x] **Task 1: Create `permissions/registry.ts`** (AC: #1)
  - File: `src/OneId.Web/src/permissions/registry.ts` (NEW directory + file)
  - Define the `PermissionGroup` interface and export `PERMISSION_GROUPS`, `PERMISSION_LABELS`, and `getPermissionLabel`
  - See **Dev Notes → registry.ts Full Spec** for the exact structure and all 40 entries grouped by domain

- [x] **Task 2: Write permission registry sync test** (AC: #2)
  - File: `src/OneId.Web/src/permissions/registry.test.ts` (NEW)
  - Test name: "every backend Permission constant has a PERMISSION_GROUPS entry"
  - Import a mirrored constant array (defined inline in the test) containing all 40 `od.*` permission IDs
  - Assert every ID in that array appears in `PERMISSION_GROUPS` (flattened)
  - If the assert fails, the error message must name the missing ID

- [x] **Task 3: Implement `DisabledButtonWithTooltip` component** (AC: #3)
  - File: `src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.tsx` (NEW)
  - See **Dev Notes → DisabledButtonWithTooltip Full Spec** for the complete implementation
  - Uses `Tooltip`, `TooltipTrigger`, `TooltipContent`, `TooltipProvider` from `@/components/ui/tooltip`
  - Uses `useId()` from React for SSR-safe `aria-describedby` wiring

- [x] **Task 4: Write `DisabledButtonWithTooltip` vitest-axe test** (AC: #4)
  - File: `src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.test.tsx` (NEW)
  - Use `vitest-axe` (already installed per Story 5b-1 dev notes) — `import { axe } from 'vitest-axe'`
  - Render in permission-block state: assert `toHaveNoViolations()`
  - Render in precondition-block state: assert `toHaveNoViolations()`
  - Keyboard test: assert wrapper `<span>` is focusable and `aria-describedby` ID matches the rendered `TooltipContent` id

---

## Dev Notes

### registry.ts Full Spec

```typescript
// src/OneId.Web/src/permissions/registry.ts

export interface PermissionGroup {
  domain: string
  label: string
  permissions: {
    id: string
    label: string
    description?: string
  }[]
}

export const PERMISSION_GROUPS: PermissionGroup[] = [
  {
    domain: 'admin.tenants',
    label: 'Tenants',
    permissions: [
      { id: 'od.admin.tenants.view',    label: 'View Tenants' },
      { id: 'od.admin.tenants.create',  label: 'Create Tenants' },
      { id: 'od.admin.tenants.update',  label: 'Update Tenants' },
      { id: 'od.admin.tenants.suspend', label: 'Suspend Tenants' },
    ],
  },
  {
    domain: 'admin.permissions',
    label: 'Permissions',
    permissions: [
      { id: 'od.admin.permissions.view',       label: 'View Permissions' },
      { id: 'od.admin.permissions.create',     label: 'Create Permissions' },
      { id: 'od.admin.permissions.update',     label: 'Update Permissions' },
      { id: 'od.admin.permissions.deactivate', label: 'Deactivate Permissions' },
    ],
  },
  {
    domain: 'admin.licenses',
    label: 'Licenses',
    permissions: [
      { id: 'od.admin.licenses.view',   label: 'View Licenses' },
      { id: 'od.admin.licenses.create', label: 'Create Licenses' },
      { id: 'od.admin.licenses.update', label: 'Update Licenses' },
    ],
  },
  {
    domain: 'admin.idp',
    label: 'Identity Providers',
    permissions: [
      { id: 'od.admin.idp.view',      label: 'View Identity Providers' },
      { id: 'od.admin.idp.configure', label: 'Configure Identity Providers' },
    ],
  },
  {
    domain: 'admin.users',
    label: 'Users',
    permissions: [
      { id: 'od.admin.users.view',       label: 'View Users' },
      { id: 'od.admin.users.create',     label: 'Create Users' },
      { id: 'od.admin.users.update',     label: 'Update Users' },
      { id: 'od.admin.users.deactivate', label: 'Deactivate Users' },
      { id: 'od.admin.users.revoke',     label: 'Revoke User Sessions' },
    ],
  },
  {
    domain: 'admin.roles',
    label: 'Roles',
    permissions: [
      { id: 'od.admin.roles.view',   label: 'View Roles' },
      { id: 'od.admin.roles.create', label: 'Create Roles' },
      { id: 'od.admin.roles.update', label: 'Update Roles' },
      { id: 'od.admin.roles.delete', label: 'Delete Roles' },
    ],
  },
  {
    domain: 'admin.rolesets',
    label: 'Role Sets',
    permissions: [
      { id: 'od.admin.rolesets.view',   label: 'View Role Sets' },
      { id: 'od.admin.rolesets.create', label: 'Create Role Sets' },
      { id: 'od.admin.rolesets.update', label: 'Update Role Sets' },
      { id: 'od.admin.rolesets.delete', label: 'Delete Role Sets' },
    ],
  },
  {
    domain: 'admin.groups',
    label: 'Groups',
    permissions: [
      { id: 'od.admin.groups.view',           label: 'View Groups' },
      { id: 'od.admin.groups.create',         label: 'Create Groups' },
      { id: 'od.admin.groups.update',         label: 'Update Groups' },
      { id: 'od.admin.groups.delete',         label: 'Delete Groups' },
      { id: 'od.admin.groups.members.manage', label: 'Manage Group Members' },
    ],
  },
  {
    domain: 'admin.dimensions',
    label: 'Dimensions',
    permissions: [
      { id: 'od.admin.dimensions.view',   label: 'View Dimensions' },
      { id: 'od.admin.dimensions.assign', label: 'Assign Dimensions' },
    ],
  },
  {
    domain: 'admin.audit',
    label: 'Audit',
    permissions: [
      { id: 'od.admin.audit.view', label: 'View Audit Log' },
    ],
  },
  {
    domain: 'crm',
    label: 'CRM',
    permissions: [
      { id: 'od.crm.read',             label: 'Read CRM Data' },
      { id: 'od.crm.write',            label: 'Write CRM Data' },
      { id: 'od.crm.invoice.create',   label: 'Create Invoices' },
      { id: 'od.crm.invoice.approve',  label: 'Approve Invoices' },
    ],
  },
  {
    domain: 'finance',
    label: 'Finance',
    permissions: [
      { id: 'od.finance.read',    label: 'Read Finance Data' },
      { id: 'od.finance.write',   label: 'Write Finance Data' },
      { id: 'od.finance.approve', label: 'Approve Finance Operations' },
    ],
  },
]

// Derived flat lookup — NEVER maintain separately
export const PERMISSION_LABELS: Record<string, string> = Object.fromEntries(
  PERMISSION_GROUPS.flatMap((g) => g.permissions.map((p) => [p.id, p.label]))
)

export function getPermissionLabel(id: string): string {
  return PERMISSION_LABELS[id] ?? id
}
```

**Important:** `PERMISSION_LABELS` is intentionally derived from `PERMISSION_GROUPS` via `flatMap` — never add entries to it manually.

**Total permissions:** 40 entries — must match exactly with the backend `Permissions` class and with `mockStore.getCurrentUserPermissions()` from Story 5b-1.

---

### registry.test.ts Full Spec

```typescript
// src/OneId.Web/src/permissions/registry.test.ts
import { describe, it, expect } from 'vitest'
import { PERMISSION_GROUPS } from './registry'

// Mirror of backend Permissions.cs constants — update this list when backend adds new constants
const BACKEND_PERMISSION_IDS = [
  'od.admin.tenants.view',
  'od.admin.tenants.create',
  'od.admin.tenants.update',
  'od.admin.tenants.suspend',
  'od.admin.permissions.view',
  'od.admin.permissions.create',
  'od.admin.permissions.update',
  'od.admin.permissions.deactivate',
  'od.admin.licenses.view',
  'od.admin.licenses.create',
  'od.admin.licenses.update',
  'od.admin.idp.view',
  'od.admin.idp.configure',
  'od.admin.users.view',
  'od.admin.users.create',
  'od.admin.users.update',
  'od.admin.users.deactivate',
  'od.admin.users.revoke',
  'od.admin.roles.view',
  'od.admin.roles.create',
  'od.admin.roles.update',
  'od.admin.roles.delete',
  'od.admin.rolesets.view',
  'od.admin.rolesets.create',
  'od.admin.rolesets.update',
  'od.admin.rolesets.delete',
  'od.admin.groups.view',
  'od.admin.groups.create',
  'od.admin.groups.update',
  'od.admin.groups.delete',
  'od.admin.groups.members.manage',
  'od.admin.dimensions.view',
  'od.admin.dimensions.assign',
  'od.admin.audit.view',
  'od.crm.read',
  'od.crm.write',
  'od.crm.invoice.create',
  'od.crm.invoice.approve',
  'od.finance.read',
  'od.finance.write',
  'od.finance.approve',
] as const

describe('Permission Registry sync', () => {
  const allRegisteredIds = new Set(
    PERMISSION_GROUPS.flatMap((g) => g.permissions.map((p) => p.id))
  )

  it('every backend Permission constant has a PERMISSION_GROUPS entry', () => {
    const missing = BACKEND_PERMISSION_IDS.filter((id) => !allRegisteredIds.has(id))
    expect(
      missing,
      `Missing PERMISSION_GROUPS entries for: ${missing.join(', ')}`
    ).toHaveLength(0)
  })
})
```

---

### DisabledButtonWithTooltip Full Spec

```tsx
// src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.tsx
import * as React from 'react'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'

interface DisabledButtonWithTooltipProps {
  tooltip: string
  children: React.ReactElement<React.ButtonHTMLAttributes<HTMLButtonElement>>
}

export function DisabledButtonWithTooltip({
  tooltip,
  children,
}: DisabledButtonWithTooltipProps) {
  const tooltipId = React.useId()

  const disabledChild = React.cloneElement(children, {
    disabled: true,
    'aria-disabled': 'true',
    'aria-describedby': tooltipId,
  })

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          {/* span captures pointer events that the disabled button swallows */}
          <span
            tabIndex={0}
            style={{ display: 'inline-block', cursor: 'not-allowed' }}
          >
            {disabledChild}
          </span>
        </TooltipTrigger>
        <TooltipContent id={tooltipId} role="tooltip">
          {tooltip}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}
```

**Usage — permission-block:**
```tsx
<DisabledButtonWithTooltip tooltip="You don't have permission to create roles. Contact your administrator.">
  <Button>Create Role</Button>
</DisabledButtonWithTooltip>
```

**Usage — precondition-block:**
```tsx
<DisabledButtonWithTooltip tooltip="No roles assigned to this group. Add a role first.">
  <Button>Assign Users</Button>
</DisabledButtonWithTooltip>
```

**Key design decisions:**
- `<span tabIndex={0}>` is what keyboard focus lands on, since native `disabled` buttons cannot be focused. The `aria-describedby` on the button itself still satisfies accessibility tooling.
- `React.cloneElement` is used so the caller passes a normal `<Button>` child — they don't need to know about the disabled wiring.
- `useId()` generates a stable SSR-safe ID. `TooltipContent` receives `id={tooltipId}` so `aria-describedby` resolves correctly.
- `TooltipProvider` is included inside the component so callers don't need to wrap it externally. This is a self-contained a11y pattern.

---

### DisabledButtonWithTooltip Test Spec

```tsx
// src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.test.tsx
import { render, screen } from '@testing-library/react'
import { axe } from 'vitest-axe'
import { describe, it, expect } from 'vitest'
import { DisabledButtonWithTooltip } from './DisabledButtonWithTooltip'

describe('DisabledButtonWithTooltip', () => {
  it('has no a11y violations in permission-block state', async () => {
    const { container } = render(
      <DisabledButtonWithTooltip tooltip="You don't have permission to create roles. Contact your administrator.">
        <button>Create Role</button>
      </DisabledButtonWithTooltip>
    )
    expect(await axe(container)).toHaveNoViolations()
  })

  it('has no a11y violations in precondition-block state', async () => {
    const { container } = render(
      <DisabledButtonWithTooltip tooltip="No roles assigned to this group. Add a role first.">
        <button>Assign Users</button>
      </DisabledButtonWithTooltip>
    )
    expect(await axe(container)).toHaveNoViolations()
  })

  it('renders button with aria-disabled and aria-describedby', () => {
    render(
      <DisabledButtonWithTooltip tooltip="You don't have permission to delete roles. Contact your administrator.">
        <button>Delete Role</button>
      </DisabledButtonWithTooltip>
    )
    const btn = screen.getByRole('button', { name: 'Delete Role' })
    expect(btn).toBeDisabled()
    expect(btn).toHaveAttribute('aria-disabled', 'true')
    expect(btn).toHaveAttribute('aria-describedby')
  })
})
```

**Note:** `vitest-axe` was noted as "already installed" in Story 5b-1 dev notes. Verify with `grep vitest-axe src/OneId.Web/package.json` before adding — if missing, run `npm install --save-dev vitest-axe` in `src/OneId.Web/`.

---

### File Structure Rules

| Directory | Purpose |
|---|---|
| `src/OneId.Web/src/permissions/` | NEW — permission registry module (data only, no React) |
| `src/OneId.Web/src/components/shared/` | Shared UI components — `DisabledButtonWithTooltip` goes here |
| `src/OneId.Web/src/hooks/` | Framework-agnostic hooks (no new hooks in this story) |
| `src/OneId.Web/src/queries/hooks/` | TanStack Query hooks only (no changes in this story) |

The `permissions/` directory is NEW. Do not put it under `components/` — it's data/logic with no React dependency.

---

### Existing Code to NOT Touch

- **`src/OneId.Web/src/hooks/useHasPermission.ts`** — already implemented in 5b-1. Do not modify.
- **`src/OneId.Web/src/queries/hooks/usePermissions.ts`** — `getCurrentUserPermissionsOptions` and `useCurrentUserPermissions` already in place. Do not modify.
- **`src/OneId.Web/src/mocks/store.ts`** — `getCurrentUserPermissions()` already returns all 40 IDs. Do not modify.
- **`src/OneId.Web/src/components/ui/tooltip.tsx`** — shadcn/ui tooltip component exists and exports `Tooltip`, `TooltipTrigger`, `TooltipContent`, `TooltipProvider`. Use as-is.
- All existing mutation hooks in `queries/hooks/` — untouched.

---

### Architecture Mandates

- `PERMISSION_LABELS` MUST be derived from `PERMISSION_GROUPS` via `flatMap` — never populated directly. This is the single-source-of-truth rule for permission label data.
- `getPermissionLabel(id)` MUST never return `undefined` or empty string. The fallback is `id` itself (the raw `od.*` string). This matters for `EffectivePermissionsPanel` (5b-3) which depends on this function.
- `DisabledButtonWithTooltip` MUST use `useId()` (React 18+) for tooltip ID — not a manual string or `Math.random()`. SSR stability is a requirement.
- `aria-disabled="true"` must be on the `<button>` element itself, not just on the `<span>`. Screen readers read the button's aria attributes, not the wrapper's.
- `TooltipContent` must receive `id={tooltipId}` so the `aria-describedby` wiring resolves. Without this, the accessibility test will fail.

---

### Tooltip Component API (already in project)

```
src/OneId.Web/src/components/ui/tooltip.tsx
```
Exports: `Tooltip`, `TooltipTrigger`, `TooltipContent`, `TooltipProvider`

`TooltipContent` accepts all `TooltipPrimitive.Content` props — including `id`. Portal-rendered. Uses `sideOffset={0}` default. Already styled with Tailwind animations.

`TooltipProvider` accepts `delayDuration` (default `0`). Include it inside `DisabledButtonWithTooltip` so the component is self-contained.

---

### Story 5b-1 Learnings Applied

- `vitest-axe` noted as "already installed" in 5b-1 dev notes — verify before running `npm install`.
- Test pattern: wrap with `QueryClientProvider` only when hooks are involved. `DisabledButtonWithTooltip` has no query hooks, so NO `QueryClientProvider` needed in its tests.
- Component location convention: `components/shared/` for shared UI components. Confirmed by existing `EmptyState`, `DataTable`, `GlobalNav`, etc.
- Import alias `@/` resolves to `src/OneId.Web/src/` — use throughout.

---

### Recent Git Context

Stories completed in order: Epic 1 (infrastructure), Epic 2 (authentication), Epic 3 Phase 3 (tenant management), Epic 4a (authorization data model), Epic 4b (permission evaluation), Epic 5a (frontend shell), M-1 (mock data layer), Epic 5c Phase 5 (admin pages, audit log, command palette), Story 5b-1 (useFormMutation + useHasPermission).

This story (5b-2) is the second story in 5b. It lays the data foundation (`registry.ts`) and accessibility pattern (`DisabledButtonWithTooltip`) that all subsequent permission-gated UI in the app will rely on.

---

## Files to Create / Modify

| File | Action | Notes |
|---|---|---|
| `src/OneId.Web/src/permissions/registry.ts` | NEW | `PermissionGroup` interface, `PERMISSION_GROUPS`, `PERMISSION_LABELS`, `getPermissionLabel` |
| `src/OneId.Web/src/permissions/registry.test.ts` | NEW | Sync test asserting all 40 backend constants have PERMISSION_GROUPS entries |
| `src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.tsx` | NEW | Accessible disabled-button wrapper with tooltip |
| `src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.test.tsx` | NEW | vitest-axe a11y tests + aria-disabled/aria-describedby assertions |

No existing files require modification.

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Fixed `require()` → ES import for `getPermissionLabel` in `registry.test.ts` (vitest uses ESM)

### Completion Notes List

- **2026-05-28** — Story implemented. Created `permissions/registry.ts` with 12 domain groups covering all 40 `od.*` constants. `PERMISSION_LABELS` derived via `flatMap` — never maintained separately. `getPermissionLabel()` falls back to raw ID, never undefined.
- Registry sync test asserts all 40 backend constants have a `PERMISSION_GROUPS` entry; fails with named missing IDs if out of sync.
- `DisabledButtonWithTooltip` uses `React.cloneElement` to inject `disabled`, `aria-disabled`, `aria-describedby` without requiring callers to pass those props. `useId()` generates SSR-safe tooltip IDs. `<span tabIndex={0}>` enables keyboard focus on the disabled button wrapper.
- `TooltipProvider` is self-contained inside the component — callers don't need to wrap it.
- 7 new tests added (3 registry, 4 component). All 69 tests pass. Build clean (no TypeScript errors).

### File List

- `src/OneId.Web/src/permissions/registry.ts` — NEW
- `src/OneId.Web/src/permissions/registry.test.ts` — NEW
- `src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.tsx` — NEW
- `src/OneId.Web/src/components/shared/DisabledButtonWithTooltip.test.tsx` — NEW

## Change Log

- **2026-05-28** — Story 5b-2 implemented. Permission label registry (12 domains, 40 entries) and `DisabledButtonWithTooltip` accessible component created. 7 new tests, 69 total passing, build clean.
