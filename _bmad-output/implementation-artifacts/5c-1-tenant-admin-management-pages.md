# Story 5c-1: Tenant Admin Management Pages

Status: review

## Story

As an Internal Admin,
I want management list pages for Users, Groups, Roles, and Role Sets within a selected tenant,
So that I can see the full authorization structure of any tenant from the console during the demo.

## Acceptance Criteria

1. **Tenant sub-nav** — `TenantContextLayout` renders a horizontal sub-nav above the outlet with five tabs: Overview, Users, Groups, Roles, Role Sets. Each tab links to its route. The active tab is highlighted (same token pattern as GlobalNav). "Overview" uses `end` matching so it only activates at the exact `/internal/tenants/:tenantId` path.

2. **Users list** — `/internal/tenants/:tenantId/users` renders a `DataTable` with columns: Name, Email, Status (badge), Groups (count), Last Login. Active users show `Badge variant="default"`, inactive show `Badge variant="secondary"`. Last Login shows formatted date or "Never" when null.

3. **Groups list** — `/internal/tenants/:tenantId/groups` renders a `DataTable` with columns: Name, Members (count), Roles (roleIds.length), Role Sets (roleSetIds.length).

4. **Roles list** — `/internal/tenants/:tenantId/roles` renders a `DataTable` with columns: Name, Permissions (permissionIds.length).

5. **Role Sets list** — `/internal/tenants/:tenantId/role-sets` renders a `DataTable` with columns: Name, Roles (roleIds.length).

6. **Skeleton + EmptyState** — All four pages show skeleton rows during loading and `EmptyState variant="no-data"` when the list is empty.

7. **Router wired** — The four `StubPage` entries in `index.tsx` are replaced with the real page components. Imports added.

8. **TenantAdminDashboard updated** — `/tenant` index page removes "Tenant management — Epic 5c" placeholder text.

9. **Build clean** — `npm run build`, `npm run lint`, `npm test` all pass with no new errors.

## Tasks / Subtasks

- [x] Update `src/routes/internal/tenants/_layout.tsx` — add horizontal sub-nav above `<Outlet />` (AC: #1)
- [x] Create `src/routes/internal/tenants/TenantUsersPage.tsx` — users DataTable (AC: #2, #6)
- [x] Create `src/routes/internal/tenants/TenantGroupsPage.tsx` — groups DataTable (AC: #3, #6)
- [x] Create `src/routes/internal/tenants/TenantRolesPage.tsx` — roles DataTable (AC: #4, #6)
- [x] Create `src/routes/internal/tenants/TenantRoleSetsPage.tsx` — role sets DataTable (AC: #5, #6)
- [x] Update `src/routes/index.tsx` — replace four StubPage entries + add imports (AC: #7)
- [x] Update `src/routes/tenant/index.tsx` — remove stale placeholder text (AC: #8)
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: #9)

---

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**Routing structure** — The four `StubPage` routes already exist in `index.tsx`:
```typescript
{ path: 'users', element: <StubPage title="Users" /> },
{ path: 'groups', element: <StubPage title="Groups" /> },
{ path: 'roles', element: <StubPage title="Roles" /> },
{ path: 'role-sets', element: <StubPage title="Role Sets" /> },
```
These are children of `path: 'tenants/:tenantId'` under `TenantContextLayout`. Replace all four.

**Available shadcn/ui components** in `src/components/ui/`:
- `button.tsx`, `dialog.tsx`, `tooltip.tsx`, `separator.tsx`, `breadcrumb.tsx`, `skeleton.tsx`
- `badge.tsx`, `input.tsx`, `label.tsx` (added in 5c-3)

No `tabs`, `card`, `toast` — do NOT use them.

**Available hooks** (all from `@/queries/hooks`):
| Hook | Signature |
|---|---|
| `useUsers(tenantId)` | `→ { data: User[], isLoading }` |
| `useGroups(tenantId)` | `→ { data: Group[], isLoading }` |
| `useRoles(tenantId)` | `→ { data: Role[], isLoading }` |
| `useRoleSets(tenantId)` | `→ { data: RoleSet[], isLoading }` |

All have `enabled: !!tenantId` — they do nothing when tenantId is empty.

**How tenantId is resolved** — These pages are children of `TenantContextLayout` which reads `tenantId` from `useParams()`. Use `useParams<{ tenantId: string }>()` directly in each page component — no Zustand needed here.

**Mock data** — 3 tenants, each with 5 users, 4 groups, 6 roles, 3 role sets. No pagination needed.

**ESLint design-token rule** — only semantic tokens in JSX `className`. Valid tokens: `bg-background`, `bg-card`, `bg-sidebar`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`, `text-destructive`. Raw Tailwind color classes will fail lint.

**No `useFormMutation`** — Story 5b-1 not yet done. No mutations in this story (read-only pages).

---

### Step 1: Tenant Sub-Nav

**File: `src/routes/internal/tenants/_layout.tsx`** — MODIFY

Add a horizontal sub-nav above `<Outlet />`. Uses `NavLink` from `react-router` — same active-state pattern as `GlobalNav.tsx` (border-l replaces with border-b for horizontal).

```typescript
import { Outlet, useParams, NavLink } from 'react-router'
import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTenantStore } from '@/store/tenant-store'
import { useUiStore } from '@/store/ui-store'
import { queryKeys } from '@/queries/keys'
import { cn } from '@/lib/utils'

const SUB_NAV_TABS = [
  { label: 'Overview', path: '', end: true },
  { label: 'Users', path: '/users', end: false },
  { label: 'Groups', path: '/groups', end: false },
  { label: 'Roles', path: '/roles', end: false },
  { label: 'Role Sets', path: '/role-sets', end: false },
]

export function TenantContextLayout() {
  const { tenantId } = useParams<{ tenantId: string }>()
  const queryClient = useQueryClient()
  const previousTenantId = useRef<string | undefined>(undefined)
  const setActiveTenant = useTenantStore((s) => s.setActiveTenantId)
  const clearTenant = useTenantStore((s) => s.clearTenant)
  const setFormDirty = useUiStore((s) => s.setFormDirty)

  useEffect(() => {
    if (tenantId && tenantId !== previousTenantId.current) {
      if (previousTenantId.current) {
        queryClient.invalidateQueries({ queryKey: queryKeys.tenant(previousTenantId.current) })
      }
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
      setActiveTenant(tenantId)
      previousTenantId.current = tenantId
    }
  }, [tenantId, queryClient, setActiveTenant])

  useEffect(() => {
    return () => {
      clearTenant()
      setFormDirty(false)
    }
  }, [clearTenant, setFormDirty])

  if (!tenantId) return null

  const base = `/internal/tenants/${tenantId}`

  return (
    <div className="flex flex-col gap-0">
      <nav
        aria-label="Tenant sections"
        className="flex gap-1 border-b border-border px-1 pb-0"
      >
        {SUB_NAV_TABS.map((tab) => (
          <NavLink
            key={tab.label}
            to={`${base}${tab.path}`}
            end={tab.end}
            className={({ isActive }) =>
              cn(
                'px-4 py-2 text-sm transition-colors border-b-2 -mb-px',
                isActive
                  ? 'border-primary text-foreground font-medium'
                  : 'border-transparent text-muted-foreground hover:text-foreground',
              )
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>
      <div className="pt-4">
        <Outlet />
      </div>
    </div>
  )
}
```

**Why `-mb-px` and `-mb-px` trick** — The tab's `border-b-2` overlaps the container's `border-b border-border` so the active tab appears to "connect" to the content. Standard tab pattern.

**NavLink `end` prop** — The Overview tab uses `end: true` so that `/internal/tenants/acme-corp` is active only when on the exact path, not also when on `/internal/tenants/acme-corp/users`. The other tabs default `end: false` (already the NavLink default).

---

### Step 2: Router Changes

**File: `src/routes/index.tsx`** — MODIFY

Replace the four StubPage imports and route definitions under `tenants/:tenantId`:

Add imports:
```typescript
import { TenantUsersPage } from './internal/tenants/TenantUsersPage'
import { TenantGroupsPage } from './internal/tenants/TenantGroupsPage'
import { TenantRolesPage } from './internal/tenants/TenantRolesPage'
import { TenantRoleSetsPage } from './internal/tenants/TenantRoleSetsPage'
```

Replace in the `tenants/:tenantId` children:
```typescript
{ path: 'users', element: <TenantUsersPage /> },
{ path: 'groups', element: <TenantGroupsPage /> },
{ path: 'roles', element: <TenantRolesPage /> },
{ path: 'role-sets', element: <TenantRoleSetsPage /> },
```

Remove the `StubPage` import ONLY if no other routes still use it. Check: `/tenant/users`, `/tenant/groups`, `/tenant/roles`, `/tenant/role-sets`, `/tenant/audit-log` all still use `StubPage` — keep the import.

---

### Step 3: Users Page

**File: `src/routes/internal/tenants/TenantUsersPage.tsx`** — NEW

```typescript
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { useUsers } from '@/queries/hooks'
import type { User } from '@/mocks/types'

const columns: ColumnDef<User, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    accessorKey: 'email',
    header: 'Email',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.email}</span>
    ),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.status === 'active' ? 'default' : 'secondary'}>
        {row.original.status === 'active' ? 'Active' : 'Inactive'}
      </Badge>
    ),
  },
  {
    id: 'groups',
    header: 'Groups',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.groupIds.length}</span>
    ),
  },
  {
    accessorKey: 'lastLogin',
    header: 'Last Login',
    cell: ({ row }) => {
      const v = row.original.lastLogin
      if (!v) return <span className="text-muted-foreground">Never</span>
      return (
        <span className="text-muted-foreground">
          {new Date(v).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}
        </span>
      )
    },
  },
]

export function TenantUsersPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: users = [], isLoading } = useUsers(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Users</h1>
      {!isLoading && users.length === 0 ? (
        <EmptyState variant="no-data" title="No users" description="Users will appear here once provisioned." />
      ) : (
        <DataTable columns={columns} data={users} isLoading={isLoading} aria-label="Users list" />
      )}
    </div>
  )
}
```

---

### Step 4: Groups Page

**File: `src/routes/internal/tenants/TenantGroupsPage.tsx`** — NEW

```typescript
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useGroups } from '@/queries/hooks'
import type { Group } from '@/mocks/types'

const columns: ColumnDef<Group, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    accessorKey: 'memberCount',
    header: 'Members',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.memberCount}</span>
    ),
  },
  {
    id: 'roles',
    header: 'Roles',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.roleIds.length}</span>
    ),
  },
  {
    id: 'roleSets',
    header: 'Role Sets',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.roleSetIds.length}</span>
    ),
  },
]

export function TenantGroupsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: groups = [], isLoading } = useGroups(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Groups</h1>
      {!isLoading && groups.length === 0 ? (
        <EmptyState variant="no-data" title="No groups" description="Groups will appear here once created." />
      ) : (
        <DataTable columns={columns} data={groups} isLoading={isLoading} aria-label="Groups list" />
      )}
    </div>
  )
}
```

---

### Step 5: Roles Page

**File: `src/routes/internal/tenants/TenantRolesPage.tsx`** — NEW

```typescript
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useRoles } from '@/queries/hooks'
import type { Role } from '@/mocks/types'

const columns: ColumnDef<Role, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    id: 'permissions',
    header: 'Permissions',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.permissionIds.length}</span>
    ),
  },
]

export function TenantRolesPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: roles = [], isLoading } = useRoles(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Roles</h1>
      {!isLoading && roles.length === 0 ? (
        <EmptyState variant="no-data" title="No roles" description="Roles will appear here once created." />
      ) : (
        <DataTable columns={columns} data={roles} isLoading={isLoading} aria-label="Roles list" />
      )}
    </div>
  )
}
```

---

### Step 6: Role Sets Page

**File: `src/routes/internal/tenants/TenantRoleSetsPage.tsx`** — NEW

```typescript
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useRoleSets } from '@/queries/hooks'
import type { RoleSet } from '@/mocks/types'

const columns: ColumnDef<RoleSet, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    id: 'roles',
    header: 'Roles',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.roleIds.length}</span>
    ),
  },
]

export function TenantRoleSetsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: roleSets = [], isLoading } = useRoleSets(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Role Sets</h1>
      {!isLoading && roleSets.length === 0 ? (
        <EmptyState variant="no-data" title="No role sets" description="Role sets will appear here once created." />
      ) : (
        <DataTable columns={columns} data={roleSets} isLoading={isLoading} aria-label="Role Sets list" />
      )}
    </div>
  )
}
```

---

### Step 7: TenantAdminDashboard Cleanup

**File: `src/routes/tenant/index.tsx`** — MODIFY

```typescript
export function TenantAdminDashboard() {
  return (
    <div className="p-8 text-foreground">
      <h1 className="text-2xl font-semibold">Tenant Admin</h1>
      <p className="text-muted-foreground mt-2">Select a section from the left to manage your organization.</p>
    </div>
  )
}
```

---

### File Structure After This Story

```
src/OneId.Web/src/
  routes/
    index.tsx                                    ← MODIFY (replace 4 StubPages)
    internal/
      tenants/
        _layout.tsx                              ← MODIFY (add sub-nav)
        TenantUsersPage.tsx                      ← NEW
        TenantGroupsPage.tsx                     ← NEW
        TenantRolesPage.tsx                      ← NEW
        TenantRoleSetsPage.tsx                   ← NEW
    tenant/
      index.tsx                                  ← MODIFY (remove stale text)
```

---

### Demo Flow After This Story

1. Navigate to `/internal/tenants` → see Tenants list (3 tenants: Acme Corp, BetaTech, Gamma Industries)
2. Click any tenant → `/internal/tenants/acme-corp` → Overview tab with detail sections
3. Click **Users** tab → `/internal/tenants/acme-corp/users` → 5 users with status, group count, last login
4. Click **Groups** tab → see 4 groups with member count and role counts
5. Click **Roles** tab → see 6 roles with permission counts
6. Click **Role Sets** tab → see 3 role sets with role counts
7. Switch to BetaTech → same structure with different data
8. Navigate to `/internal/permissions` → global 25-permission catalog (already done in 5c-3)

---

### Scope Boundaries — What This Story Does NOT Include

These are intentionally deferred:

- **Tenant Admin tier pages** at `/tenant/users`, `/tenant/groups`, etc. — remain stubs. In the mock world, these require a "logged-in tenant admin" context that isn't established yet. Phase 8 when real auth is in.
- **Create / Edit / Delete** for any entity — deferred until 5c-1 full (full story with real backend)
- **User detail page** — deferred (EffectivePermissionsPanel is Phase 8)
- **SeatUsageIndicator** — deferred (Story 5b-5, Phase 8)
- **Dimension assignments** — deferred (Story 4a.6, Phase 7)
- **Permission names on Roles page** — showing count only; full multi-select combobox is Phase 4/8

---

### Known Gotchas from Story 5c-3

1. **ESLint design-token rule** — `text-green-600` etc. will fail. Use `Badge variant="default"` for active (indigo), `variant="secondary"` for inactive (gray), `variant="destructive"` for suspended/danger (red).

2. **TypeScript event handlers** — Explicit `React.ChangeEvent<HTMLInputElement>` typing sometimes needed.

3. **`cn()` utility** — Import from `@/lib/utils`. Used in the sub-nav for conditional class logic.

4. **`useParams` default value** — Use `const { tenantId = '' } = useParams<{ tenantId: string }>()` to avoid the `string | undefined` type issue. The parent `TenantContextLayout` already guards `if (!tenantId) return null`, so by the time any child renders, `tenantId` is always a real value.

5. **`ColumnDef<T, unknown>`** — Use `unknown` as the second type parameter. The `@tanstack/react-table` ColumnDef requires two generics; using `unknown` suppresses the "inferred type" warning.

6. **Build warning: chunk size > 500 kB** — Pre-existing, not introduced by this story. Do not attempt to fix.

---

### Tests

No new test files required. After implementation, run:

```bash
npm test -- --run      # all 38 tests must still pass
npm run build          # TypeScript + Vite build must be clean
npm run lint           # no new ESLint errors
```

---

## References

- [TenantContextLayout](src/OneId.Web/src/routes/internal/tenants/_layout.tsx) — MODIFY
- [Router](src/OneId.Web/src/routes/index.tsx) — MODIFY
- [TenantAdminDashboard](src/OneId.Web/src/routes/tenant/index.tsx) — MODIFY
- [GlobalNav](src/OneId.Web/src/components/shared/GlobalNav.tsx) — NavLink active-state pattern reference
- [TenantDetailPage](src/OneId.Web/src/routes/internal/tenants/TenantDetailPage.tsx) — `useParams` pattern reference
- [TenantListPage](src/OneId.Web/src/routes/internal/tenants/TenantListPage.tsx) — DataTable + EmptyState pattern reference
- [useUsers hook](src/OneId.Web/src/queries/hooks/useUsers.ts)
- [useGroups hook](src/OneId.Web/src/queries/hooks/useGroups.ts)
- [useRoles hook](src/OneId.Web/src/queries/hooks/useRoles.ts)
- [useRoleSets hook](src/OneId.Web/src/queries/hooks/useRoleSets.ts)
- [query hooks barrel](src/OneId.Web/src/queries/hooks/index.ts)
- [mock types](src/OneId.Web/src/mocks/types.ts)
- [DataTable component](src/OneId.Web/src/components/shared/DataTable.tsx)
- [EmptyState component](src/OneId.Web/src/components/shared/EmptyState.tsx)
- [tenant-store](src/OneId.Web/src/store/tenant-store.ts) — Zustand store (context only)
- [useActiveTenant hook](src/OneId.Web/src/hooks/useActiveTenant.ts) — context only (not needed in these pages)
- [5c-3 story](./_5c-3-internal-admin-management-pages.md) — previous story patterns + gotchas

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Pre-existing lint error in `TenantDetailPage.tsx` (react-hooks/set-state-in-effect from story 5c-3) — not introduced by this story.
- Pre-existing lint warning in `DataTable.tsx` (react-hooks/incompatible-library from story 5a-4) — not introduced by this story.
- Pre-existing build warning: chunk size > 500 kB — not introduced by this story.

### Completion Notes List

- `TenantContextLayout` extended with a 5-tab horizontal sub-nav (Overview, Users, Groups, Roles, Role Sets) using `NavLink` with active border-b-2 highlight. `end: true` on Overview prevents false-active on sub-routes.
- `TenantUsersPage` — DataTable with Name, Email, Status badge (default/secondary), Groups count, Last Login (formatted date or "Never").
- `TenantGroupsPage` — DataTable with Name, Members, Roles count, Role Sets count.
- `TenantRolesPage` — DataTable with Name, Permissions count.
- `TenantRoleSetsPage` — DataTable with Name, Roles count.
- All four pages: skeleton during loading, EmptyState variant="no-data" when empty, `useParams` for tenantId (no Zustand needed in sub-pages — parent TenantContextLayout guards).
- Router updated: four StubPage entries under `tenants/:tenantId` replaced with real components.
- TenantAdminDashboard stale text removed.
- 38/38 tests pass, TypeScript build clean.

### File List

- `src/routes/internal/tenants/_layout.tsx` — MODIFIED (added sub-nav)
- `src/routes/internal/tenants/TenantUsersPage.tsx` — NEW
- `src/routes/internal/tenants/TenantGroupsPage.tsx` — NEW
- `src/routes/internal/tenants/TenantRolesPage.tsx` — NEW
- `src/routes/internal/tenants/TenantRoleSetsPage.tsx` — NEW
- `src/routes/index.tsx` — MODIFIED (4 StubPages replaced, 4 imports added)
- `src/routes/tenant/index.tsx` — MODIFIED (stale text removed)

## Change Log

- 2026-05-23: Story created — Tenant management sub-pages (Users, Groups, Roles, Role Sets) under Internal Admin tenant context, scoped for mock-data demo.
- 2026-05-23: Story implemented — all 8 tasks complete, 38/38 tests pass, build clean.
