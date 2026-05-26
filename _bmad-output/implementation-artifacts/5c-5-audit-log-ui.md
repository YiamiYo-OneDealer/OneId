# Story 5c-5: Audit Log UI

Status: review

## Story

As an Internal Admin or Tenant Admin,
I want a read-only audit log showing all significant management actions,
So that I can investigate changes without needing database access.

## Acceptance Criteria

1. **Tenant Admin page** — `/tenant/audit-log` renders an Audit Log page (replacing the current `StubPage`). A `DataTable` lists audit events with columns: timestamp (formatted), actor name + email, action type, entity type, entity ID. `DataTable` renders skeleton rows during fetch and `EmptyState variant="no-data"` when no events exist.

2. **Internal Admin page** — `/internal/audit-log` is a new route. Same `DataTable` layout but shows events across all tenants (no tenant filter). The Internal Admin nav (`INTERNAL_ADMIN_NAV` in `GlobalNav.tsx`) gains an "Audit Log" entry linking to `/internal/audit-log`.

3. **Row detail** — clicking any row opens a `Sheet` (shadcn/ui, `side="right"`) displaying the full event: formatted timestamp, actor name + email (or "System" if `actorUserId` is null), action, entity type, entity ID, and the raw `payload` rendered as a formatted JSON block. The sheet is read-only — no edit or delete controls.

4. **Pagination** — `DataTable` pagination prop is wired. Page size = 25. The hook accepts `page` and `pageSize` params, slices mock data, and returns `Paginated<AuditLogEntry>`. `onPaginationChange` triggers a re-fetch with updated params. Pages beyond available data show `EmptyState`.

5. **Performance** — mock hook simulates the 400ms delay (matches `mockDelay` pattern). No N+1: actor name/email is denormalized on the `AuditLogEntry` type (not looked up per-row from users array).

6. **Build clean** — `npm run build`, `npm run lint`, `npm test` all pass with no new errors or warnings. All pre-existing tests still pass.

## Tasks / Subtasks

- [x] Install shadcn `sheet` component: `npx shadcn@latest add sheet` (inside `src/OneId.Web/`)
- [x] Add `AuditLogEntry` type to `src/mocks/types.ts`
- [x] Add audit log fixtures to `src/mocks/fixtures.ts`
- [x] Add `getAuditLog` to `src/mocks/store.ts`
- [x] Add `auditLog` query key to `src/queries/keys.ts`
- [x] Create `src/queries/hooks/useAuditLog.ts` and export from `src/queries/hooks/index.ts`
- [x] Create `src/routes/tenant/audit-log.tsx` (replace StubPage)
- [x] Create `src/routes/internal/audit-log.tsx` (new)
- [x] Update `src/routes/index.tsx` — import both new pages, add `/internal/audit-log` route, replace tenant `StubPage` with `TenantAuditLogPage`
- [x] Update `src/components/shared/GlobalNav.tsx` — add "Audit Log" to `INTERNAL_ADMIN_NAV`
- [x] Run `npm test -- --run`, `npm run build`, `npm run lint` and confirm all pass

---

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**Available shadcn/ui components** (in `src/components/ui/`):
`badge`, `breadcrumb`, `button`, `checkbox`, `dialog`, `input`, `label`, `separator`, `skeleton`, `theme-toggle`, `tooltip`

**MISSING for this story** — install before writing any component code:
```bash
cd src/OneId.Web
npx shadcn@latest add sheet
```
This creates `src/components/ui/sheet.tsx`.

**`Sheet` import path**: `@/components/ui/sheet` — exports `Sheet`, `SheetContent`, `SheetHeader`, `SheetTitle`, `SheetDescription` (and more). Use `side="right"` for a right-sliding detail panel. Open/close via `open` + `onOpenChange` props (controlled).

**Mock data layer** — all pages use `mockStore` via hooks from `@/queries/hooks`. No real API calls. Data resets on page reload.

**No `useFormMutation`** — Story 5b-1 is not yet done. For this story it doesn't matter (read-only page, no mutations).

**`Paginated<T>`** type is already defined in `src/mocks/types.ts`:
```typescript
export interface Paginated<T> {
  items: T[]
  totalCount: number
  pageIndex: number   // zero-based
  pageSize: number
}
```

**ESLint design-token rule** — applies to JSX `className`. Use semantic tokens only: `bg-background`, `bg-card`, `bg-sidebar`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`, `text-destructive`. Raw Tailwind color classes (e.g. `text-gray-500`, `bg-zinc-800`) will fail lint.

**`DataTable` pagination** — The `DataTable` component at `src/components/shared/DataTable.tsx` supports server-side pagination via:
```typescript
pagination?: {
  pageIndex: number        // zero-based
  pageSize: number
  total: number
  onPaginationChange: OnChangeFn<PaginationState>
}
```
Wire this to local `useState<{ pageIndex: number; pageSize: number }>`.

---

### Step 1: Add `AuditLogEntry` type

**File: `src/mocks/types.ts`** — ADD at end of file

```typescript
export interface AuditLogEntry {
  id: string
  tenantId: string
  actorUserId: string | null
  actorName: string | null    // denormalized — null for system events
  actorEmail: string | null   // denormalized — null for system events
  action: string              // e.g. "user.created", "role.updated"
  entityType: string          // e.g. "User", "Role", "Group"
  entityId: string
  payload: Record<string, unknown> | null
  timestamp: string           // ISO 8601 UTC
}
```

---

### Step 2: Add audit log fixtures

**File: `src/mocks/fixtures.ts`** — ADD `auditLog` array and update exports

At the top, add the import:
```typescript
import type { Tenant, User, Group, Role, RoleSet, Permission, AuditLogEntry } from './types'
```

Add this array (after the existing fixture data, before the export):
```typescript
const auditLog: AuditLogEntry[] = [
  {
    id: 'audit-001',
    tenantId: 'tenant-alpha',
    actorUserId: 'user-alice',
    actorName: 'Alice Admin',
    actorEmail: 'alice@alpha.com',
    action: 'user.created',
    entityType: 'User',
    entityId: 'user-bob',
    payload: { name: 'Bob User', email: 'bob@alpha.com', status: 'active' },
    timestamp: '2026-05-20T09:00:00Z',
  },
  {
    id: 'audit-002',
    tenantId: 'tenant-alpha',
    actorUserId: 'user-alice',
    actorName: 'Alice Admin',
    actorEmail: 'alice@alpha.com',
    action: 'role.created',
    entityType: 'Role',
    entityId: 'role-viewer',
    payload: { name: 'Viewer', permissionIds: ['od.users.read', 'od.groups.read'] },
    timestamp: '2026-05-20T10:15:00Z',
  },
  {
    id: 'audit-003',
    tenantId: 'tenant-alpha',
    actorUserId: 'user-alice',
    actorName: 'Alice Admin',
    actorEmail: 'alice@alpha.com',
    action: 'group.updated',
    entityType: 'Group',
    entityId: 'group-admins',
    payload: { roleIds: ['role-viewer', 'role-editor'] },
    timestamp: '2026-05-21T08:30:00Z',
  },
  {
    id: 'audit-004',
    tenantId: 'tenant-beta',
    actorUserId: 'user-carol',
    actorName: 'Carol Manager',
    actorEmail: 'carol@beta.com',
    action: 'user.updated',
    entityType: 'User',
    entityId: 'user-dave',
    payload: { status: 'inactive' },
    timestamp: '2026-05-21T11:45:00Z',
  },
  {
    id: 'audit-005',
    tenantId: 'tenant-alpha',
    actorUserId: null,
    actorName: null,
    actorEmail: null,
    action: 'tenant.created',
    entityType: 'Tenant',
    entityId: 'tenant-alpha',
    payload: { name: 'Alpha Corp', status: 'active' },
    timestamp: '2026-05-19T07:00:00Z',
  },
  {
    id: 'audit-006',
    tenantId: 'tenant-beta',
    actorUserId: 'user-carol',
    actorName: 'Carol Manager',
    actorEmail: 'carol@beta.com',
    action: 'role-set.created',
    entityType: 'RoleSet',
    entityId: 'roleset-full',
    payload: { name: 'Full Access', roleIds: ['role-admin'] },
    timestamp: '2026-05-22T14:00:00Z',
  },
]
```

Update the exported `fixtures` object to include `auditLog`:
```typescript
export const fixtures = {
  tenants,
  users,
  groups,
  roles,
  roleSets,
  permissions,
  auditLog,
}
```

**Important:** The fixture uses hardcoded IDs like `'tenant-alpha'`, `'user-alice'`, etc. Check `fixtures.ts` for the actual tenant/user IDs used in the existing fixtures and align the audit log entries accordingly. If tenant IDs differ, update the `tenantId` fields to match real fixture IDs.

---

### Step 3: Add `getAuditLog` to mockStore

**File: `src/mocks/store.ts`** — ADD to mockStore and update imports

Update the import at top:
```typescript
import type { Tenant, User, Group, Role, RoleSet, Permission, AuditLogEntry } from './types'
```

Add the `auditLog` state field in `state`:
```typescript
const state = {
  // ... existing fields ...
  auditLog: [...fixtures.auditLog],
}
```

Add to the `mockStore` object:
```typescript
  // ── Audit Log ────────────────────────────────────────────────────────────
  getAuditLog: (
    tenantId: string | null,
    pageIndex: number,
    pageSize: number,
  ): import('./types').Paginated<AuditLogEntry> => {
    const filtered = tenantId
      ? state.auditLog.filter((e) => e.tenantId === tenantId)
      : [...state.auditLog]
    // Descending by timestamp (most recent first)
    filtered.sort((a, b) => b.timestamp.localeCompare(a.timestamp))
    const start = pageIndex * pageSize
    return {
      items: filtered.slice(start, start + pageSize),
      totalCount: filtered.length,
      pageIndex,
      pageSize,
    }
  },
```

**Note:** `tenantId = null` returns events across ALL tenants (Internal Admin view). `tenantId = 'some-id'` returns only that tenant's events.

---

### Step 4: Add query key

**File: `src/queries/keys.ts`** — ADD one entry

```typescript
  auditLog: (tenantId: string | null) => ['audit-log', tenantId] as const,
```

---

### Step 5: Create `useAuditLog` hook

**File: `src/queries/hooks/useAuditLog.ts`** — NEW

```typescript
import { useQuery } from '@tanstack/react-query'
import { mockStore, mockDelay } from '@/mocks/store'
import { queryKeys } from '@/queries/keys'

export function useAuditLog(
  tenantId: string | null,
  pageIndex: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: [...queryKeys.auditLog(tenantId), pageIndex, pageSize],
    queryFn: async () => {
      await mockDelay()
      return mockStore.getAuditLog(tenantId, pageIndex, pageSize)
    },
  })
}
```

**File: `src/queries/hooks/index.ts`** — ADD export:
```typescript
export * from './useAuditLog'
```

---

### Step 6: Tenant Admin Audit Log page

**File: `src/routes/tenant/audit-log.tsx`** — NEW (replaces StubPage)

```typescript
import { useState } from 'react'
import { type ColumnDef, type PaginationState, type OnChangeFn } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet'
import { useAuditLog } from '@/queries/hooks'
import type { AuditLogEntry } from '@/mocks/types'

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'UTC',
    timeZoneName: 'short',
  })
}

// ── Column definitions ────────────────────────────────────────────────────────

const columns: ColumnDef<AuditLogEntry, unknown>[] = [
  {
    accessorKey: 'timestamp',
    header: 'Timestamp',
    cell: ({ row }) => (
      <span className="text-sm text-muted-foreground font-mono whitespace-nowrap">
        {formatTimestamp(row.original.timestamp)}
      </span>
    ),
  },
  {
    id: 'actor',
    header: 'Actor',
    cell: ({ row }) => {
      const { actorName, actorEmail } = row.original
      if (!actorName) return <span className="text-muted-foreground text-sm italic">System</span>
      return (
        <div>
          <p className="text-sm font-medium text-foreground">{actorName}</p>
          <p className="text-xs text-muted-foreground">{actorEmail}</p>
        </div>
      )
    },
  },
  {
    accessorKey: 'action',
    header: 'Action',
    cell: ({ row }) => (
      <span className="font-mono text-sm text-foreground">{row.original.action}</span>
    ),
  },
  {
    accessorKey: 'entityType',
    header: 'Entity Type',
    cell: ({ row }) => (
      <span className="text-sm text-muted-foreground">{row.original.entityType}</span>
    ),
  },
  {
    accessorKey: 'entityId',
    header: 'Entity ID',
    cell: ({ row }) => (
      <span className="font-mono text-xs text-muted-foreground">{row.original.entityId}</span>
    ),
  },
]

// ── Detail Sheet ──────────────────────────────────────────────────────────────

function AuditEventSheet({
  entry,
  onClose,
}: {
  entry: AuditLogEntry | null
  onClose: () => void
}) {
  return (
    <Sheet open={!!entry} onOpenChange={(open) => { if (!open) onClose() }}>
      <SheetContent side="right" className="w-[480px] overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Audit Event</SheetTitle>
          <SheetDescription>Read-only event details</SheetDescription>
        </SheetHeader>
        {entry && (
          <div className="mt-4 space-y-4 text-sm">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
              <dt className="text-muted-foreground font-medium">Timestamp</dt>
              <dd className="font-mono text-foreground">{formatTimestamp(entry.timestamp)}</dd>

              <dt className="text-muted-foreground font-medium">Actor</dt>
              <dd className="text-foreground">
                {entry.actorName ? (
                  <>
                    {entry.actorName}
                    <span className="block text-xs text-muted-foreground">{entry.actorEmail}</span>
                  </>
                ) : (
                  <span className="italic text-muted-foreground">System</span>
                )}
              </dd>

              <dt className="text-muted-foreground font-medium">Action</dt>
              <dd className="font-mono text-foreground">{entry.action}</dd>

              <dt className="text-muted-foreground font-medium">Entity Type</dt>
              <dd className="text-foreground">{entry.entityType}</dd>

              <dt className="text-muted-foreground font-medium">Entity ID</dt>
              <dd className="font-mono text-xs text-foreground">{entry.entityId}</dd>
            </dl>

            {entry.payload && (
              <div>
                <p className="text-muted-foreground font-medium mb-1">Payload</p>
                <pre className="rounded-md border border-border bg-card p-3 text-xs text-foreground overflow-x-auto">
                  {JSON.stringify(entry.payload, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}
      </SheetContent>
    </Sheet>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

const PAGE_SIZE = 25

// NOTE: Tenant Admin audit log — scoped to the current tenant.
// tenantId comes from Zustand (useActiveTenant) since this page is under
// the /tenant layout which doesn't use :tenantId URL param.
// Import useActiveTenant from '@/hooks/useActiveTenant'.
import { useActiveTenant } from '@/hooks/useActiveTenant'

export function TenantAuditLogPage() {
  const tenantId = useActiveTenant()
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: PAGE_SIZE,
  })
  const [selectedEntry, setSelectedEntry] = useState<AuditLogEntry | null>(null)

  const { data, isLoading } = useAuditLog(tenantId, pagination.pageIndex, pagination.pageSize)

  const handlePaginationChange: OnChangeFn<PaginationState> = (updater) => {
    setPagination((prev) =>
      typeof updater === 'function' ? updater(prev) : updater,
    )
  }

  const clickableColumns: ColumnDef<AuditLogEntry, unknown>[] = columns.map((col) => ({
    ...col,
  }))

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-foreground">Audit Log</h1>

      {!isLoading && data?.items.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No audit events"
          description="Management actions will appear here."
        />
      ) : (
        <div
          onClick={(e) => {
            const row = (e.target as HTMLElement).closest('tr[data-row-id]')
            if (row) {
              const rowId = row.getAttribute('data-row-id')
              const entry = data?.items.find((_, i) => String(i) === rowId)
              if (entry) setSelectedEntry(entry)
            }
          }}
        >
          <DataTable
            columns={clickableColumns}
            data={data?.items ?? []}
            isLoading={isLoading}
            aria-label="Audit log"
            pagination={
              data
                ? {
                    pageIndex: data.pageIndex,
                    pageSize: data.pageSize,
                    total: data.totalCount,
                    onPaginationChange: handlePaginationChange,
                  }
                : undefined
            }
          />
        </div>
      )}

      <AuditEventSheet entry={selectedEntry} onClose={() => setSelectedEntry(null)} />
    </div>
  )
}
```

**STOP — Row click pattern note:** The `DataTable` component does not expose a row `onClick` prop. Rather than wrapping in a click-bubbling div (fragile), extend the approach used above OR add an `onRowClick?: (row: TData) => void` prop to `DataTable`. The simpler approach: add an `onRowClick` prop to the `DataTable` component.

**Preferred approach — extend `DataTable`:**

In `src/components/shared/DataTable.tsx`, add `onRowClick?: (row: TData) => void` to `DataTableProps` and wire it:
```typescript
interface DataTableProps<TData extends object, TValue> {
  // ...existing props...
  onRowClick?: (row: TData) => void
}

// In the <tr> for data rows:
<tr
  key={row.id}
  onClick={() => onRowClick?.(row.original)}
  className={cn(
    'border-b border-border transition-colors hover:bg-card',
    onRowClick && 'cursor-pointer',
  )}
>
```

Then in the page component, pass:
```typescript
<DataTable
  columns={columns}
  data={data?.items ?? []}
  isLoading={isLoading}
  onRowClick={(entry) => setSelectedEntry(entry)}
  // ...
/>
```

This is a clean, type-safe approach. The `cursor-pointer` class only applies when `onRowClick` is provided.

---

### Step 7: Internal Admin Audit Log page

**File: `src/routes/internal/audit-log.tsx`** — NEW

This is identical in structure to the Tenant Admin page, but passes `tenantId = null` to `useAuditLog`:

```typescript
import { useState } from 'react'
import { type ColumnDef, type PaginationState, type OnChangeFn } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet'
import { useAuditLog } from '@/queries/hooks'
import type { AuditLogEntry } from '@/mocks/types'

// Re-use the same columns + formatTimestamp + AuditEventSheet as TenantAuditLogPage.
// DO NOT duplicate — extract shared logic.

// Option A: Extract shared components to src/routes/_shared/audit-log-shared.tsx
// Option B: Copy columns/helpers inline (acceptable for POC since pages are small)
// Recommendation: inline copy is fine — pages are < 60 lines each. No shared component needed.

const PAGE_SIZE = 25

export function InternalAuditLogPage() {
  // tenantId = null → shows all tenants' events
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: PAGE_SIZE,
  })
  const [selectedEntry, setSelectedEntry] = useState<AuditLogEntry | null>(null)

  const { data, isLoading } = useAuditLog(null, pagination.pageIndex, pagination.pageSize)

  const handlePaginationChange: OnChangeFn<PaginationState> = (updater) => {
    setPagination((prev) =>
      typeof updater === 'function' ? updater(prev) : updater,
    )
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-foreground">Audit Log</h1>
      <p className="text-sm text-muted-foreground">All tenants — management events across the platform.</p>

      {!isLoading && data?.items.length === 0 ? (
        <EmptyState variant="no-data" title="No audit events" description="Management actions will appear here." />
      ) : (
        <DataTable
          columns={columns}   // same column defs as Tenant Admin page
          data={data?.items ?? []}
          isLoading={isLoading}
          aria-label="Platform audit log"
          onRowClick={(entry) => setSelectedEntry(entry)}
          pagination={
            data
              ? {
                  pageIndex: data.pageIndex,
                  pageSize: data.pageSize,
                  total: data.totalCount,
                  onPaginationChange: handlePaginationChange,
                }
              : undefined
          }
        />
      )}

      <AuditEventSheet entry={selectedEntry} onClose={() => setSelectedEntry(null)} />
    </div>
  )
}
```

**COLUMNS DUPLICATION:** The Internal Admin page needs the same `columns` array and `formatTimestamp` helper as the Tenant Admin page. To avoid copy-paste, extract them to a shared module. **Recommended file:** `src/routes/audit-log-columns.tsx` (or inline in both — your call for POC).

---

### Step 8: Router changes

**File: `src/routes/index.tsx`** — MODIFY

Add imports at top:
```typescript
import { TenantAuditLogPage } from './tenant/audit-log'
import { InternalAuditLogPage } from './internal/audit-log'
```

**Internal Admin children** — ADD the new route:
```typescript
// inside the 'internal' children array:
{ path: 'audit-log', element: <InternalAuditLogPage /> },
```

**Tenant Admin children** — REPLACE StubPage with real component:
```typescript
// Change from:
{ path: 'audit-log', element: <StubPage title="Audit Log" /> },
// Change to:
{ path: 'audit-log', element: <TenantAuditLogPage /> },
```

Remove `StubPage` import only if `StubPage` is no longer used for any other route. If other StubPage routes remain, keep the import.

---

### Step 9: GlobalNav — Add Audit Log to Internal Admin nav

**File: `src/components/shared/GlobalNav.tsx`** — MODIFY

`INTERNAL_ADMIN_NAV` currently:
```typescript
const INTERNAL_ADMIN_NAV: NavConfig[] = [
  { to: '/internal/tenants', label: 'Tenants', icon: Building2 },
  { to: '/internal/permissions', label: 'Permissions', icon: Key },
  { to: '/internal/licenses', label: 'Licenses', icon: CreditCard },
]
```

Add Audit Log entry:
```typescript
const INTERNAL_ADMIN_NAV: NavConfig[] = [
  { to: '/internal/tenants', label: 'Tenants', icon: Building2 },
  { to: '/internal/permissions', label: 'Permissions', icon: Key },
  { to: '/internal/licenses', label: 'Licenses', icon: CreditCard },
  { to: '/internal/audit-log', label: 'Audit Log', icon: ScrollText },
]
```

`ScrollText` is already imported at the top of the file (it's used for Tenant Admin "Audit Log"). No new import needed.

---

### Step 10: DataTable `onRowClick` extension

**File: `src/components/shared/DataTable.tsx`** — MODIFY

This is the cleanest way to support row clicks. Add `onRowClick` to `DataTableProps` and wire it to the `<tr>` in the data rows section. The `DataTable.test.tsx` test file may need updating if it asserts on row rendering — check that the existing tests still pass.

---

### `useActiveTenant` hook

**File: `src/hooks/useActiveTenant.ts`** — READ BEFORE USING

Check what this hook returns. If it returns `string` (current tenant ID), use it directly as the `tenantId` parameter. If it returns `string | null`, the null case would hit the "all tenants" branch in `getAuditLog` — add a guard:

```typescript
const tenantId = useActiveTenant()
// Guard: tenant admin always has a tenantId
if (!tenantId) return null  // or loading state
```

---

### Fixture ID alignment

**IMPORTANT:** The fixture audit log uses hardcoded IDs like `'tenant-alpha'`, `'user-alice'`. Open `src/mocks/fixtures.ts` and check the actual tenant IDs (e.g. `fixtures.tenants[0].id`). Update the `tenantId`, `actorUserId`, `entityId` fields in the audit log fixtures to reference real fixture IDs — otherwise the Internal Admin "all events" view works but the Tenant Admin view filters to nothing.

---

### File structure after this story

```
src/OneId.Web/src/
  mocks/
    types.ts                           ← MODIFY (add AuditLogEntry)
    fixtures.ts                        ← MODIFY (add auditLog array)
    store.ts                           ← MODIFY (add getAuditLog)
  queries/
    keys.ts                            ← MODIFY (add auditLog key)
    hooks/
      useAuditLog.ts                   ← NEW
      index.ts                         ← MODIFY (add export)
  routes/
    index.tsx                          ← MODIFY (add routes, replace StubPage)
    tenant/
      audit-log.tsx                    ← NEW (TenantAuditLogPage)
    internal/
      audit-log.tsx                    ← NEW (InternalAuditLogPage)
  components/
    shared/
      DataTable.tsx                    ← MODIFY (add onRowClick prop)
    ui/
      sheet.tsx                        ← NEW (shadcn install)
  hooks/
    useActiveTenant.ts                 ← READ ONLY (no change)
```

---

### Pre-Implementation Checklist

Before writing any component code:
- [ ] `npx shadcn@latest add sheet` ran — `src/components/ui/sheet.tsx` exists
- [ ] `npm run build` passes with current codebase (get a clean baseline)
- [ ] Fixture IDs noted: actual tenant IDs from `fixtures.ts` for audit log alignment

---

### Tests

No new Vitest unit test files required. The hooks and store will be covered by the existing test infrastructure. After implementation, run:
```bash
npm test -- --run      # all pre-existing tests must still pass
npm run build          # TypeScript + Vite build must be clean
npm run lint           # no new ESLint errors
```

The `DataTable` component change (`onRowClick`) adds a prop to an existing interface — it's additive and won't break the existing `DataTable.test.tsx` if the test doesn't assert on row click behavior. Verify `DataTable.test.tsx` still passes.

---

## References

- [DataTable component](src/OneId.Web/src/components/shared/DataTable.tsx) — MODIFY (onRowClick)
- [EmptyState component](src/OneId.Web/src/components/shared/EmptyState.tsx)
- [mockStore](src/OneId.Web/src/mocks/store.ts) — MODIFY
- [mock types](src/OneId.Web/src/mocks/types.ts) — MODIFY
- [mock fixtures](src/OneId.Web/src/mocks/fixtures.ts) — MODIFY
- [queryKeys factory](src/OneId.Web/src/queries/keys.ts) — MODIFY
- [query hooks barrel](src/OneId.Web/src/queries/hooks/index.ts) — MODIFY
- [GlobalNav component](src/OneId.Web/src/components/shared/GlobalNav.tsx) — MODIFY
- [Router](src/OneId.Web/src/routes/index.tsx) — MODIFY
- [useActiveTenant hook](src/OneId.Web/src/hooks/useActiveTenant.ts) — READ
- [5c-3 story](./5c-3-internal-admin-management-pages.md) — DataTable + EmptyState usage patterns (done, reference)
- [M-1 story](./m-1-mock-data-layer.md) — mock data layer (prerequisite, done)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Completion Notes List

- `AuditLogEntry` type added to `mocks/types.ts` with 8 realistic fixture entries across all 3 tenants
- `mockStore.getAuditLog(tenantId, pageIndex, pageSize)` sorts by timestamp desc and slices for pagination
- `useAuditLog` hook with TanStack Query; query key includes pagination params for per-page caching
- `DataTable` extended with `onRowClick?: (row: TData) => void` prop; adds `cursor-pointer` class when provided
- shadcn `sheet` installed — manually moved from `@/components/ui/` to `src/components/ui/` (same bug as story 5c-3)
- `AuditEventSheet` component in `components/shared/` — shows full event detail with formatted timestamp, actor (System for null actorUserId), and JSON payload block
- Both route pages (`tenant/audit-log.tsx`, `internal/audit-log.tsx`) define columns inline (JSX) to satisfy `react-refresh/only-export-components` lint rule
- `formatTimestamp` extracted to `routes/_audit-log-columns.ts` (pure TS, no JSX) to avoid react-refresh lint issues
- Tenant Admin page uses `useTenantStore activeTenantId ?? 'acme-corp'` fallback (no `:tenantId` in URL under `/tenant/`)
- Internal Admin page passes `tenantId = null` to show all tenants' events
- `INTERNAL_ADMIN_NAV` updated with "Audit Log" entry using `ScrollText` icon (already imported)
- Pre-existing lint errors (useTheme, TenantDetailPage, reset-password): 7 errors, unchanged from baseline
- Build: ✅ clean | Tests: ✅ 51/51 passing | Lint: no new errors introduced

### File List

- `src/components/ui/sheet.tsx` — NEW (shadcn install)
- `src/mocks/types.ts` — MODIFIED (AuditLogEntry type)
- `src/mocks/fixtures.ts` — MODIFIED (auditLog fixtures, updated export)
- `src/mocks/store.ts` — MODIFIED (getAuditLog method, Paginated import)
- `src/queries/keys.ts` — MODIFIED (auditLog key)
- `src/queries/hooks/useAuditLog.ts` — NEW
- `src/queries/hooks/index.ts` — MODIFIED (export useAuditLog)
- `src/components/shared/DataTable.tsx` — MODIFIED (onRowClick prop)
- `src/components/shared/AuditEventSheet.tsx` — NEW
- `src/routes/_audit-log-columns.ts` — NEW (formatTimestamp utility)
- `src/routes/tenant/audit-log.tsx` — NEW (TenantAuditLogPage)
- `src/routes/internal/audit-log.tsx` — NEW (InternalAuditLogPage)
- `src/routes/index.tsx` — MODIFIED (added InternalAuditLogPage route, replaced StubPage for tenant audit-log)
- `src/components/shared/GlobalNav.tsx` — MODIFIED (Audit Log in INTERNAL_ADMIN_NAV)

## Change Log

- 2026-05-26: Story created — audit log UI (Tenant Admin + Internal Admin pages, DataTable + Sheet detail, mock data layer).
- 2026-05-26: Story implemented — all tasks complete, 51/51 tests pass, TypeScript build clean.
