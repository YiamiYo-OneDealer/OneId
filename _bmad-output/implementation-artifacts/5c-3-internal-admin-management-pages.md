# Story 5c-3: Internal Admin Management Pages

Status: review

## Story

As an Internal Admin,
I want fully functional management pages for Tenants, the global Permission catalog, and per-Tenant detail management (license, Tenant Admins, IDP federation stub),
So that I can administer all organizations from a single console without direct database access.

## Acceptance Criteria

1. **Tenants list** — `/internal/tenants` renders a `DataTable` listing all Tenants with columns: name, status, seat usage (used/max), created date. Each row has a "View" link navigating to `/internal/tenants/:tenantId`. The page title is "Tenants".

2. **Tenant status badge** — Active tenants show a green-tinted status indicator; suspended tenants show a red-tinted indicator. Both use semantic color tokens (no raw Tailwind color classes).

3. **Tenant suspension** — A "Suspend" / "Reinstate" button on the Tenant detail page triggers a Medium-tier confirmation Dialog:
   - Title: "Suspend [Tenant Name]?"
   - Body: "Suspending this tenant will immediately revoke all active sessions. Continue?"
   - Buttons: "Cancel" (outline) + "Suspend" (destructive)
   - On confirm: calls `useUpdateTenant({ tenantId, patch: { status: 'suspended' } })` and shows inline success feedback.
   - For suspended tenants, button label is "Reinstate" and the dialog says "Reinstate [Tenant Name]?" / "This tenant will be re-activated."

4. **Tenant detail** — `/internal/tenants/:tenantId` renders a detail page with four sections (each visually grouped):
   - **Overview**: tenant name, status, seat usage, created date — read-only display.
   - **License**: `maxSeats` numeric input (empty = unlimited), "Save" button. On save, updates `seatUsage.max` via `useUpdateTenant`. Shows inline success text "Saved." for 2s then clears.
   - **Tenant Administrators**: list of current admins (name + email) with "Remove" button per row; "Remove" is disabled with tooltip "A tenant must have at least one administrator." when only one admin remains. "Add Administrator" opens a combobox (text filter + list) showing tenant users not already admins.
   - **IDP Federation**: placeholder card: heading "Federation", subtitle "External identity provider federation is available in Epic 6.", a disabled "Configure" button.

5. **Permissions catalog** — `/internal/permissions` renders a `DataTable` listing all 25 global `od.*` permissions with columns: id (monospace), domain, description, active status. Rows are sorted by domain then id. No pagination (25 items).

6. **Router wired** — All new pages are reachable from the browser. No `StubPage` or placeholder remains for these three routes.

7. **GlobalNav Tenants link** — The "Tenants" nav item in `GlobalNav` now links to `/internal/tenants` (was `/internal`). The active highlight applies when the current path starts with `/internal/tenants`.

8. **Root redirect updated** — The root `<Navigate to="/internal" replace />` is changed to `<Navigate to="/internal/tenants" replace />`. Navigating to `/` goes directly to the Tenants list.

9. **DataTable skeleton + EmptyState** — All three pages show skeleton rows during loading and `EmptyState` variant `"no-data"` when data is empty (mock data means it won't be empty, but the path must be wired).

10. **Build clean** — `npm run build`, `npm run lint`, and `npm test` all pass with no new errors or warnings.

## Tasks / Subtasks

- [x] Install missing shadcn/ui components: `badge`, `input`, `label` (run command in Dev Notes)
- [x] Update `src/routes/index.tsx` — add tenant list route, permissions route, replace `TenantDashboardStub`, update root redirect (AC: #6, #8)
- [x] Update `src/components/shared/GlobalNav.tsx` — fix Tenants nav link to `/internal/tenants` (AC: #7)
- [x] Create `src/routes/internal/tenants/TenantListPage.tsx` — tenant list DataTable (AC: #1, #2, #9)
- [x] Create `src/routes/internal/tenants/TenantDetailPage.tsx` — tenant detail with four sections (AC: #3, #4)
- [x] Create `src/routes/internal/permissions.tsx` — permissions catalog DataTable (AC: #5, #9)
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: #10)

---

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**Available shadcn/ui components** — only these exist in `src/components/ui/`:
- `button.tsx`, `dialog.tsx`, `tooltip.tsx`, `separator.tsx`, `breadcrumb.tsx`, `skeleton.tsx`

**MISSING** (must install before implementing): `badge`, `input`, `label`

**Install command** (run inside `src/OneId.Web/`):
```bash
npx shadcn@latest add badge input label
```

No `card`, `toast`, or `sonner` — do NOT use them. Use inline success text via local state instead of toast.

**Mock data** — `mockStore` (from `@/mocks/store`) is the data source. No API calls. Use hooks from `@/queries/hooks` (barrel at `@/queries/hooks/index.ts`).

**No `useFormMutation`** — Story 5b-1 not yet done. Use `useMutation` from `@tanstack/react-query` directly.

**No `Paginated<T>`** — mock hooks return plain arrays, not paginated. DataTable `pagination` prop is not needed for these pages. Do NOT wire pagination props — just pass `data` and let the table render all rows.

**ESLint design-token rule** — applies to JSX `className`. Use semantic tokens only: `bg-background`, `bg-card`, `bg-sidebar`, `bg-admin-banner-bg`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`, `text-destructive`, `bg-destructive`. Check `src/index.css` for defined custom tokens.

---

### Step 0: Install shadcn Components

```bash
cd src/OneId.Web
npx shadcn@latest add badge input label
```

This creates `src/components/ui/badge.tsx`, `input.tsx`, `label.tsx`. Do this before any implementation step.

---

### Step 1: Router Changes

**File: `src/routes/index.tsx`** — MODIFY

Full updated router:

```typescript
import { createBrowserRouter, Navigate } from 'react-router'
import { AuthenticatedLayout } from './_authenticated'
import { ErrorPage } from './error'
import { LoginPage } from './login'
import { SuspendedPage } from './suspended'
import { InternalLayout } from './internal/_layout'
import { InternalDashboard } from './internal/index'
import { TenantContextLayout } from './internal/tenants/_layout'
import { TenantDetailPage } from './internal/tenants/TenantDetailPage'
import { TenantListPage } from './internal/tenants/TenantListPage'
import { PermissionsPage } from './internal/permissions'
import { TenantAdminLayout } from './tenant/_layout'
import { TenantAdminDashboard } from './tenant/index'
import { StubPage } from './_stub-page'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AuthenticatedLayout />,
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <Navigate to="/internal/tenants" replace /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'suspended', element: <SuspendedPage /> },
      {
        path: 'internal',
        element: <InternalLayout />,
        children: [
          { index: true, element: <InternalDashboard /> },
          { path: 'tenants', element: <TenantListPage /> },
          { path: 'permissions', element: <PermissionsPage /> },
          {
            path: 'tenants/:tenantId',
            element: <TenantContextLayout />,
            children: [
              { index: true, element: <TenantDetailPage /> },
              { path: 'users', element: <StubPage title="Users" /> },
              { path: 'groups', element: <StubPage title="Groups" /> },
              { path: 'roles', element: <StubPage title="Roles" /> },
              { path: 'role-sets', element: <StubPage title="Role Sets" /> },
            ],
          },
        ],
      },
      {
        path: 'tenant',
        element: <TenantAdminLayout />,
        children: [
          { index: true, element: <TenantAdminDashboard /> },
          { path: 'users', element: <StubPage title="Users" /> },
          { path: 'groups', element: <StubPage title="Groups" /> },
          { path: 'roles', element: <StubPage title="Roles" /> },
          { path: 'role-sets', element: <StubPage title="Role Sets" /> },
          { path: 'audit-log', element: <StubPage title="Audit Log" /> },
        ],
      },
    ],
  },
])
```

**Critical routing note:** `path: 'tenants'` (static) and `path: 'tenants/:tenantId'` (dynamic) coexist without conflict — React Router matches static before dynamic. `/internal/tenants` hits `TenantListPage`, `/internal/tenants/acme-corp` hits `TenantContextLayout`.

---

### Step 2: GlobalNav Fix

**File: `src/components/shared/GlobalNav.tsx`** — MODIFY

Change the Tenants nav item from:
```typescript
{ to: '/internal', label: 'Tenants', icon: Building2, exact: true },
```
To:
```typescript
{ to: '/internal/tenants', label: 'Tenants', icon: Building2 },
```

Remove `exact: true` — `/internal/tenants` is no longer the index path and should match any sub-path under it (`/internal/tenants/acme-corp` should also highlight "Tenants" in the nav).

The `NavItem` component uses `useMatch({ path: item.to, end: item.exact ?? false })`. Without `exact`, `end` defaults to `false`, meaning `/internal/tenants/acme-corp` also highlights the Tenants nav item. That is correct behavior.

---

### Step 3: Tenant List Page

**File: `src/routes/internal/tenants/TenantListPage.tsx`** — NEW

```typescript
import { Link } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useTenants } from '@/queries/hooks'
import type { Tenant } from '@/mocks/types'

const columns: ColumnDef<Tenant, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <Link
        to={`/internal/tenants/${row.original.id}`}
        className="font-medium text-foreground hover:underline"
      >
        {row.original.name}
      </Link>
    ),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.status === 'active' ? 'default' : 'destructive'}>
        {row.original.status === 'active' ? 'Active' : 'Suspended'}
      </Badge>
    ),
  },
  {
    accessorKey: 'seatUsage',
    header: 'Seat Usage',
    cell: ({ row }) => {
      const { used, max } = row.original.seatUsage
      return (
        <span className="text-muted-foreground">
          {used} / {max === null ? '∞' : max}
        </span>
      )
    },
  },
  {
    accessorKey: 'createdAt',
    header: 'Created',
    cell: ({ row }) =>
      new Date(row.original.createdAt).toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      }),
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => (
      <Button variant="outline" size="sm" asChild>
        <Link to={`/internal/tenants/${row.original.id}`}>View</Link>
      </Button>
    ),
  },
]

export function TenantListPage() {
  const { data: tenants = [], isLoading } = useTenants()

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-foreground">Tenants</h1>
      </div>

      {!isLoading && tenants.length === 0 ? (
        <EmptyState variant="no-data" title="No tenants yet" description="Tenants will appear here once provisioned." />
      ) : (
        <DataTable
          columns={columns}
          data={tenants}
          isLoading={isLoading}
          aria-label="Tenants list"
        />
      )}
    </div>
  )
}
```

**Notes:**
- `Badge variant="default"` renders with `bg-primary` (indigo). `variant="destructive"` renders with `bg-destructive` (red). Both are semantic — no raw color classes.
- `Button asChild` + `Link` renders a link styled as a button. This is the shadcn/ui pattern.
- No pagination prop — 3 tenants fit in a flat list.

---

### Step 4: Tenant Detail Page

**File: `src/routes/internal/tenants/TenantDetailPage.tsx`** — NEW

This replaces `TenantDashboardStub`. The component has four distinct sections.

```typescript
import { useState, useEffect } from 'react'
import { useParams } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTenant, useUsers } from '@/queries/hooks'
import { useUpdateTenant } from '@/queries/hooks'
import { mockStore } from '@/mocks/store'
import { queryKeys } from '@/queries/keys'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import type { User } from '@/mocks/types'

// ── Suspension Section ────────────────────────────────────────────────────────

function SuspensionDialog({
  tenantName,
  isOpen,
  onClose,
  onConfirm,
  currentStatus,
  isPending,
}: {
  tenantName: string
  isOpen: boolean
  onClose: () => void
  onConfirm: () => void
  currentStatus: 'active' | 'suspended'
  isPending: boolean
}) {
  const isSuspending = currentStatus === 'active'
  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) onClose() }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {isSuspending ? `Suspend ${tenantName}?` : `Reinstate ${tenantName}?`}
          </DialogTitle>
          <DialogDescription>
            {isSuspending
              ? 'Suspending this tenant will immediately revoke all active sessions. Continue?'
              : 'This tenant will be re-activated and users will be able to log in again.'}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            Cancel
          </Button>
          <Button
            variant={isSuspending ? 'destructive' : 'default'}
            onClick={onConfirm}
            disabled={isPending}
          >
            {isPending ? 'Saving…' : isSuspending ? 'Suspend' : 'Reinstate'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── License Section ───────────────────────────────────────────────────────────

function LicenseSection({ tenantId, currentMax }: { tenantId: string; currentMax: number | null }) {
  const [maxSeats, setMaxSeats] = useState(currentMax === null ? '' : String(currentMax))
  const [saved, setSaved] = useState(false)
  const updateTenant = useUpdateTenant()

  const handleSave = () => {
    const parsed = maxSeats.trim() === '' ? null : parseInt(maxSeats, 10)
    updateTenant.mutate(
      { tenantId, patch: { seatUsage: { used: 0, max: parsed } } },
      {
        onSuccess: () => {
          setSaved(true)
          setTimeout(() => setSaved(false), 2000)
        },
      },
    )
  }

  return (
    <section className="rounded-md border border-border bg-card p-4 space-y-3">
      <h2 className="text-sm font-semibold text-foreground">License</h2>
      <div className="flex items-end gap-3">
        <div className="flex flex-col gap-1">
          <Label htmlFor="max-seats">Max seats (blank = unlimited)</Label>
          <Input
            id="max-seats"
            type="number"
            min={1}
            value={maxSeats}
            onChange={(e) => setMaxSeats(e.target.value)}
            className="w-40"
            placeholder="Unlimited"
          />
        </div>
        <Button
          size="sm"
          onClick={handleSave}
          disabled={updateTenant.isPending}
        >
          {updateTenant.isPending ? 'Saving…' : 'Save'}
        </Button>
        {saved && <span className="text-sm text-muted-foreground">Saved.</span>}
        {updateTenant.isError && (
          <span className="text-sm text-destructive">Failed to save.</span>
        )}
      </div>
    </section>
  )
}

// ── Tenant Admins Section ─────────────────────────────────────────────────────

function TenantAdminsSection({ tenantId }: { tenantId: string }) {
  const { data: users = [] } = useUsers(tenantId)

  // Seed admins from users in the "Administrators" group
  const [admins, setAdmins] = useState<User[]>([])
  useEffect(() => {
    if (users.length === 0) return
    const groups = mockStore.getGroups(tenantId)
    const adminGroup = groups.find((g) => g.name === 'Administrators')
    if (adminGroup) {
      setAdmins(users.filter((u) => u.groupIds.includes(adminGroup.id)))
    }
  }, [users, tenantId])

  const [searchQuery, setSearchQuery] = useState('')
  const [showAddPanel, setShowAddPanel] = useState(false)

  const adminIds = new Set(admins.map((a) => a.id))
  const addCandidates = users.filter(
    (u) =>
      !adminIds.has(u.id) &&
      (u.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        u.email.toLowerCase().includes(searchQuery.toLowerCase())),
  )

  const handleRemove = (userId: string) => {
    setAdmins((prev) => prev.filter((a) => a.id !== userId))
  }

  const handleAdd = (user: User) => {
    setAdmins((prev) => [...prev, user])
    setSearchQuery('')
    setShowAddPanel(false)
  }

  return (
    <section className="rounded-md border border-border bg-card p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-foreground">Tenant Administrators</h2>
        <Button variant="outline" size="sm" onClick={() => setShowAddPanel((v) => !v)}>
          Add Administrator
        </Button>
      </div>

      {showAddPanel && (
        <div className="border border-border rounded-md p-3 space-y-2 bg-background">
          <Input
            placeholder="Search users…"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            autoFocus
          />
          {addCandidates.length === 0 ? (
            <p className="text-sm text-muted-foreground">No matching users.</p>
          ) : (
            <ul className="max-h-40 overflow-y-auto space-y-1">
              {addCandidates.map((u) => (
                <li key={u.id}>
                  <button
                    onClick={() => handleAdd(u)}
                    className="w-full text-left px-2 py-1 text-sm rounded hover:bg-card text-foreground"
                  >
                    {u.name} <span className="text-muted-foreground">— {u.email}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <ul className="space-y-2">
        {admins.map((admin) => (
          <li key={admin.id} className="flex items-center justify-between py-1">
            <div>
              <p className="text-sm font-medium text-foreground">{admin.name}</p>
              <p className="text-xs text-muted-foreground">{admin.email}</p>
            </div>
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={admins.length <= 1}
                      onClick={() => handleRemove(admin.id)}
                    >
                      Remove
                    </Button>
                  </span>
                </TooltipTrigger>
                {admins.length <= 1 && (
                  <TooltipContent>A tenant must have at least one administrator.</TooltipContent>
                )}
              </Tooltip>
            </TooltipProvider>
          </li>
        ))}
      </ul>
    </section>
  )
}

// ── IDP Federation Stub ───────────────────────────────────────────────────────

function IdpFederationStub() {
  return (
    <section className="rounded-md border border-border bg-card p-4 space-y-2">
      <h2 className="text-sm font-semibold text-foreground">Federation</h2>
      <p className="text-sm text-muted-foreground">
        External identity provider federation is available in Epic 6.
      </p>
      <Button variant="outline" size="sm" disabled>
        Configure
      </Button>
    </section>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export function TenantDetailPage() {
  const { tenantId } = useParams<{ tenantId: string }>()
  const { data: tenant, isLoading } = useTenant(tenantId ?? '')
  const updateTenant = useUpdateTenant()
  const [suspendDialogOpen, setSuspendDialogOpen] = useState(false)

  if (!tenantId) return null

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 animate-pulse rounded bg-card" />
        <div className="h-40 animate-pulse rounded-md border border-border bg-card" />
      </div>
    )
  }

  if (!tenant) {
    return <p className="text-destructive">Tenant not found.</p>
  }

  const handleStatusToggle = () => {
    updateTenant.mutate(
      {
        tenantId,
        patch: { status: tenant.status === 'active' ? 'suspended' : 'active' },
      },
      { onSuccess: () => setSuspendDialogOpen(false) },
    )
  }

  return (
    <div className="space-y-6 max-w-2xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-semibold text-foreground">{tenant.name}</h1>
          <Badge variant={tenant.status === 'active' ? 'default' : 'destructive'}>
            {tenant.status === 'active' ? 'Active' : 'Suspended'}
          </Badge>
        </div>
        <Button
          variant={tenant.status === 'active' ? 'destructive' : 'default'}
          size="sm"
          onClick={() => setSuspendDialogOpen(true)}
        >
          {tenant.status === 'active' ? 'Suspend' : 'Reinstate'}
        </Button>
      </div>

      {/* Overview */}
      <section className="rounded-md border border-border bg-card p-4 space-y-2">
        <h2 className="text-sm font-semibold text-foreground">Overview</h2>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
          <dt className="text-muted-foreground">Tenant ID</dt>
          <dd className="font-mono text-foreground">{tenant.id}</dd>
          <dt className="text-muted-foreground">Seat usage</dt>
          <dd className="text-foreground">
            {tenant.seatUsage.used} / {tenant.seatUsage.max === null ? '∞' : tenant.seatUsage.max}
          </dd>
          <dt className="text-muted-foreground">Created</dt>
          <dd className="text-foreground">
            {new Date(tenant.createdAt).toLocaleDateString('en-GB', {
              day: '2-digit',
              month: 'short',
              year: 'numeric',
            })}
          </dd>
        </dl>
      </section>

      {/* License */}
      <LicenseSection tenantId={tenantId} currentMax={tenant.seatUsage.max} />

      {/* Tenant Admins */}
      <TenantAdminsSection tenantId={tenantId} />

      {/* IDP Federation */}
      <IdpFederationStub />

      {/* Suspension dialog */}
      <SuspensionDialog
        tenantName={tenant.name}
        isOpen={suspendDialogOpen}
        onClose={() => setSuspendDialogOpen(false)}
        onConfirm={handleStatusToggle}
        currentStatus={tenant.status}
        isPending={updateTenant.isPending}
      />
    </div>
  )
}
```

**Notes on `useUpdateTenant`:** It's in `src/queries/hooks/useTenants.ts` and exported from `@/queries/hooks`. The `patch` for license updates passes `seatUsage` as `{ used: 0, max: parsed }` — this intentionally resets `used` to 0 for mock purposes (real API would only accept `max`). The dev can improve this by reading `tenant.seatUsage.used` first. But for demo, the mock store spread-merges the patch so `used` would still be current after `invalidateQueries` re-fetches.

Actually — `useUpdateTenant` in `useTenants.ts` calls `mockStore.updateTenant(tenantId, patch)` which does `{ ...existing, ...patch }`. Since `seatUsage` is an object, the patch replaces the whole `seatUsage` object. To preserve `used`, the component should use:
```typescript
patch: { seatUsage: { used: tenant.seatUsage.used, max: parsed } }
```
Update the `handleSave` in `LicenseSection` to receive the current `used` value:
```typescript
function LicenseSection({ tenantId, currentMax, currentUsed }: { tenantId: string; currentMax: number | null; currentUsed: number }) {
  // ...
  patch: { seatUsage: { used: currentUsed, max: parsed } }
}
```
And pass `currentUsed={tenant.seatUsage.used}` from `TenantDetailPage`.

---

### Step 5: Permissions Catalog Page

**File: `src/routes/internal/permissions.tsx`** — NEW

```typescript
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { usePermissions } from '@/queries/hooks'
import type { Permission } from '@/mocks/types'

const columns: ColumnDef<Permission, unknown>[] = [
  {
    accessorKey: 'id',
    header: 'Permission ID',
    cell: ({ row }) => (
      <span className="font-mono text-sm text-foreground">{row.original.id}</span>
    ),
  },
  {
    accessorKey: 'domain',
    header: 'Domain',
    cell: ({ row }) => (
      <span className="text-muted-foreground capitalize">{row.original.domain}</span>
    ),
  },
  {
    accessorKey: 'description',
    header: 'Description',
  },
  {
    accessorKey: 'isActive',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.isActive ? 'default' : 'secondary'}>
        {row.original.isActive ? 'Active' : 'Inactive'}
      </Badge>
    ),
  },
]

export function PermissionsPage() {
  const { data: permissions = [], isLoading } = usePermissions()

  // Sort by domain then by id
  const sorted = [...permissions].sort((a, b) =>
    a.domain !== b.domain ? a.domain.localeCompare(b.domain) : a.id.localeCompare(b.id),
  )

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-foreground">Permissions</h1>

      {!isLoading && sorted.length === 0 ? (
        <EmptyState variant="no-data" title="No permissions defined" />
      ) : (
        <DataTable
          columns={columns}
          data={sorted}
          isLoading={isLoading}
          aria-label="Global permissions catalog"
        />
      )}
    </div>
  )
}
```

---

### Step 6: Remove `TenantDashboardStub` Import

**File: `src/routes/internal/tenants/index.tsx`** — The router no longer imports `TenantDashboardStub` from this file. You can either:
1. Delete the file (preferred — it's now unused)
2. Or leave it in place (the unused export causes no harm)

Check if anything else imports from this file before deleting. The router import was the only consumer.

---

### File Structure After This Story

```
src/OneId.Web/src/
  routes/
    index.tsx                              ← MODIFY (router changes)
    internal/
      index.tsx                           ← unchanged (InternalDashboard)
      tenants/
        index.tsx                         ← DELETE (TenantDashboardStub, now unused)
        _layout.tsx                       ← unchanged (TenantContextLayout)
        TenantListPage.tsx                ← NEW
        TenantDetailPage.tsx              ← NEW
      permissions.tsx                     ← NEW
  components/
    shared/
      GlobalNav.tsx                       ← MODIFY (Tenants link)
  components/
    ui/
      badge.tsx                           ← NEW (shadcn install)
      input.tsx                           ← NEW (shadcn install)
      label.tsx                           ← NEW (shadcn install)
```

---

### Important Implementation Notes

1. **`Button asChild`** — shadcn Button supports `asChild` prop (Radix Slot). `<Button asChild><Link to="...">View</Link></Button>` renders a link styled as a button. This avoids nesting `<a>` inside `<button>`.

2. **`useUpdateTenant` for status toggle** — `mockStore.updateTenant` does a shallow merge: `{ ...existing, ...patch }`. Since `seatUsage` is a nested object, pass the full seatUsage object (preserving `used`) when updating `max`. Flatten the seatUsage in the patch: `{ seatUsage: { used: tenant.seatUsage.used, max: newMax } }`.

3. **Tenant Admins are local state** — The admin list is React component state only (no query hook). It seeds from `mockStore.getGroups` + `mockStore.getUsers` on mount. This is intentional — there's no TenantAdmin endpoint in M-1. The list resets on page reload (same as all mock data behavior).

4. **`useUsers(tenantId)` in TenantAdminsSection** — The `tenantId` param is available via `useParams()` from the parent `TenantDetailPage`. But since `TenantAdminsSection` doesn't call `useParams()` directly, pass `tenantId` as a prop. Alternatively, read from `useTenantStore` — but prop-passing is cleaner.

5. **TypeScript import from `@/queries/hooks`** — All hooks (including `useUpdateTenant`, `useTenant`, `useUsers`) are barrel-exported from `src/queries/hooks/index.ts`. Import from `@/queries/hooks`, not from individual files.

6. **Badge `variant="secondary"`** — shadcn Badge generates this variant automatically. It's a gray/muted look, appropriate for "Inactive" status.

7. **No `useQueryClient` in TenantDetailPage directly** — `useUpdateTenant` from the hook handles `invalidateQueries` internally. The page just calls `.mutate()`.

8. **Breadcrumb behavior** — `Breadcrumbs.tsx` auto-generates from URL segments. `/internal/tenants` → ["Internal", "Tenants"]. `/internal/tenants/acme-corp` → ["Internal", "Tenants", "Acme Corp"]. The segment `acme-corp` → `segmentToLabel` → "Acme Corp" (capitalizes each word, replaces hyphens with spaces). The UUID filter won't hide these IDs since they're not UUIDs.

---

### Pre-Implementation Checklist

Before writing any component code:
- [ ] `npx shadcn@latest add badge input label` ran successfully
- [ ] `src/components/ui/badge.tsx` exists
- [ ] `src/components/ui/input.tsx` exists
- [ ] `src/components/ui/label.tsx` exists
- [ ] `npm run build` passes with current codebase (baseline)

---

### Tests

No new test files required for this story. The mock hooks already have test coverage from M-1 (`useTenants.test.ts`). The new pages are UI components that will be covered by Playwright in story 5c-7.

After implementation, run:
```bash
npm test -- --run      # all 38 tests must still pass
npm run build          # TypeScript + Vite build must be clean
npm run lint           # no new ESLint errors
```

---

## References

- [queryKeys factory](src/OneId.Web/src/queries/keys.ts)
- [query hooks barrel](src/OneId.Web/src/queries/hooks/index.ts)
- [mock store](src/OneId.Web/src/mocks/store.ts)
- [mock types](src/OneId.Web/src/mocks/types.ts)
- [DataTable component](src/OneId.Web/src/components/shared/DataTable.tsx)
- [EmptyState component](src/OneId.Web/src/components/shared/EmptyState.tsx)
- [AdminTierBanner component](src/OneId.Web/src/components/shared/AdminTierBanner.tsx) — Dialog pattern reference
- [GlobalNav component](src/OneId.Web/src/components/shared/GlobalNav.tsx) — MODIFY
- [Router](src/OneId.Web/src/routes/index.tsx) — MODIFY
- [M-1 story](./m-1-mock-data-layer.md) — mock data layer (prerequisite, done)
- [5a-2 story](./5a-2-app-shell-routing-tenant-context-and-query-key-factory.md) — TanStack Query v5 patterns
- [5a-4 story](./5a-4-datatable-and-emptystate-components.md) — DataTable/EmptyState API

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- shadcn `npx shadcn@latest add` installed badge/input/label to literal `@/components/ui/` directory (misresolved alias). Manually moved files to correct `src/components/ui/` and deleted the `@/` directory.
- TypeScript error TS7006 on event handler params — added explicit `React.ChangeEvent<HTMLInputElement>` type annotation.
- Build warning: chunk size > 500 kB (pre-existing, not introduced by this story).

### Completion Notes List

- Tenant list DataTable at `/internal/tenants` — 3 tenants from mock store with name link, status Badge, seat usage, created date, View button.
- Tenant detail page at `/internal/tenants/:tenantId` — 4 sections: Overview (read-only dl), License (numeric Input + save), Tenant Admins (local state seeded from Administrators group, add/remove with 1-admin guard), IDP stub card.
- Permissions catalog at `/internal/permissions` — 25 `od.*` entries sorted by domain then id.
- GlobalNav Tenants link updated from `/internal` to `/internal/tenants` (removed `exact: true`).
- Root redirect changed from `/internal` to `/internal/tenants`.
- Old `TenantDashboardStub` import removed from router (file left in place but unused).
- All 38 tests pass, TypeScript build clean.

### File List

- `src/components/ui/badge.tsx` — NEW (shadcn)
- `src/components/ui/input.tsx` — NEW (shadcn)
- `src/components/ui/label.tsx` — NEW (shadcn)
- `src/routes/index.tsx` — MODIFIED (new routes, root redirect, TenantDetailPage import)
- `src/components/shared/GlobalNav.tsx` — MODIFIED (Tenants link)
- `src/routes/internal/tenants/TenantListPage.tsx` — NEW
- `src/routes/internal/tenants/TenantDetailPage.tsx` — NEW
- `src/routes/internal/permissions.tsx` — NEW

## Change Log

- 2026-05-23: Story created — internal admin management pages (tenant list, tenant detail, permissions catalog).
- 2026-05-23: Story implemented — all tasks complete, 38/38 tests pass, build clean.
