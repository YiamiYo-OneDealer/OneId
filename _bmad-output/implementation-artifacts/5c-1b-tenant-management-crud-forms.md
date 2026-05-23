# Story 5c-1b: Tenant Management CRUD Forms

Status: review

## Story

As an Internal Admin (demo mode),
I want to create, edit, and delete Roles, Role Sets, Groups, and Users within a tenant,
So that I can demonstrate the full authorization structure management flow using mock data.

## Acceptance Criteria

1. **Roles CRUD** — The Roles list page (`/internal/tenants/:tenantId/roles`) shows a "Create Role" button aligned with the page title. Clicking opens a Dialog with a name field and a permission checkbox list (with search input). Each row has Edit (pre-filled dialog) and Delete (confirm dialog). Deleting a role assigned to a group shows inline error in the dialog: "Cannot delete: assigned to [Group names]."

2. **Role Sets CRUD** — The Role Sets list page shows "Create Role Set". Dialog has name + role checkbox list. Delete uses same confirm dialog + 409 inline error pattern.

3. **Groups CRUD** — Groups list shows "Create Group". Dialog has name field + role checkbox list + role set checkbox list. Each row has Edit (pre-filled) and Delete (confirm dialog). No 409 guard on group delete.

4. **Users CRUD** — Users list shows "Create User". Dialog has name, email, status (Active/Inactive toggle), and group multi-select. Each user row has an Activate/Deactivate button (no dialog — immediate Low-tier action). No full user edit dialog (group reassignment is Phase 8).

5. **Form validation** — Required fields: name (Roles, Role Sets, Groups), name+email (Users). Validation fires on blur and on submit (not on every keystroke). Empty/invalid field shows `<p className="text-sm text-destructive">` inline error below the input. Submit button is disabled while mutation is pending.

6. **mockStore extensions** — `deleteGroup` added to mockStore. `deleteRoleSet` extended with a 409 guard (checks if any group in that tenant references the roleSetId). `useDeleteGroup` hook added and exported.

7. **Build clean** — `npm run build`, `npm run lint`, `npm test` all pass with no new errors.

## Tasks / Subtasks

- [x] Install shadcn `checkbox` component (`npx shadcn@latest add checkbox` in `src/OneId.Web/`)
- [x] Extend `src/mocks/store.ts` — add `deleteGroup`, extend `deleteRoleSet` with 409 guard
- [x] Extend `src/queries/hooks/useGroups.ts` — add `useDeleteGroup`, re-export via barrel
- [x] Create `src/routes/internal/tenants/_delete-dialog.tsx` — reusable `DeleteDialog` component
- [x] Update `src/routes/internal/tenants/TenantRolesPage.tsx` — CRUD: create/edit/delete with permission multi-select
- [x] Update `src/routes/internal/tenants/TenantRoleSetsPage.tsx` — CRUD: create/edit/delete with role multi-select
- [x] Update `src/routes/internal/tenants/TenantGroupsPage.tsx` — CRUD: create/edit/delete with role + role set multi-select
- [x] Update `src/routes/internal/tenants/TenantUsersPage.tsx` — create dialog + activate/deactivate button
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: #7)

---

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**Routing** — No route changes needed. All four pages already exist and are wired. This story modifies the existing page components in-place.

**Available shadcn/ui components** BEFORE this story:
- `button.tsx`, `dialog.tsx`, `tooltip.tsx`, `separator.tsx`, `breadcrumb.tsx`, `skeleton.tsx`, `badge.tsx`, `input.tsx`, `label.tsx`

**Must install**: `checkbox` — run `npx shadcn@latest add checkbox` inside `src/OneId.Web/`. Creates `src/components/ui/checkbox.tsx`.

**ESLint design-token rule** — only semantic tokens in JSX `className`. Valid: `bg-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`, `text-destructive`. Raw Tailwind color classes (e.g. `text-red-500`) will fail lint.

**No `useFormMutation`** — Story 5b-1 not done. Use `useMutation`-based hooks from `@/queries/hooks` directly.

**Pre-existing lint error** in `TenantDetailPage.tsx` (`react-hooks/set-state-in-effect`) — not introduced by this story; do not try to fix it.

**Pre-existing build warning** — chunk size > 500 kB. Do not attempt to fix.

---

### Mock Store State

**Types** (`src/mocks/types.ts`):
```typescript
interface Role   { id: string; tenantId: string; name: string; permissionIds: string[] }
interface RoleSet { id: string; tenantId: string; name: string; roleIds: string[] }
interface Group   { id: string; tenantId: string; name: string; memberCount: number; roleIds: string[]; roleSetIds: string[] }
interface User    { id: string; tenantId: string; name: string; email: string; status: 'active'|'inactive'; groupIds: string[]; lastLogin: string|null; createdAt: string }
interface Permission { id: string; domain: string; description: string; isActive: boolean }
```

**Existing mock store CRUD methods** (`src/mocks/store.ts`):
| Entity | Create | Update | Delete |
|--------|--------|--------|--------|
| Role | `createRole(data: Omit<Role,'id'>)` | `updateRole(tenantId, roleId, patch)` | `deleteRole(tenantId, roleId)` — throws `{ status:409, assignedTo: string[] }` if in use |
| RoleSet | `createRoleSet(data: Omit<RoleSet,'id'>)` | `updateRoleSet(tenantId, roleSetId, patch)` | `deleteRoleSet(tenantId, roleSetId)` — **no 409 guard yet** |
| Group | `createGroup(data: Omit<Group,'id'>)` | `updateGroup(tenantId, groupId, patch)` | **missing — must add** |
| User | `createUser(data: Omit<User,'id'|'createdAt'>)` | `updateUser(tenantId, userId, patch)` | **not in scope** — deactivate via `updateUser` with `status:'inactive'` |

**Query hooks available** (`@/queries/hooks`):
| Hook | Signature |
|------|-----------|
| `useRoles(tenantId)` | `→ { data: Role[], isLoading }` |
| `useCreateRole(tenantId)` | `→ UseMutationResult` — `mutate(data: Omit<Role,'id'|'tenantId'>)` |
| `useUpdateRole(tenantId)` | `→ UseMutationResult` — `mutate({ roleId, patch })` |
| `useDeleteRole(tenantId)` | `→ UseMutationResult` — `mutate(roleId: string)` |
| `useRoleSets(tenantId)` | `→ { data: RoleSet[], isLoading }` |
| `useCreateRoleSet(tenantId)` | `→ UseMutationResult` — `mutate(data: Omit<RoleSet,'id'|'tenantId'>)` |
| `useUpdateRoleSet(tenantId)` | `→ UseMutationResult` — `mutate({ roleSetId, patch })` |
| `useDeleteRoleSet(tenantId)` | `→ UseMutationResult` — `mutate(roleSetId: string)` |
| `useGroups(tenantId)` | `→ { data: Group[], isLoading }` |
| `useCreateGroup(tenantId)` | `→ UseMutationResult` — `mutate(data: Omit<Group,'id'>)` |
| `useUpdateGroup(tenantId)` | `→ UseMutationResult` — `mutate({ groupId, patch })` |
| `useDeleteGroup(tenantId)` | **must add** |
| `useUsers(tenantId)` | `→ { data: User[], isLoading }` |
| `useCreateUser(tenantId)` | `→ UseMutationResult` — `mutate(data: Omit<User,'id'|'createdAt'>)` |
| `useUpdateUser(tenantId)` | `→ UseMutationResult` — `mutate({ userId, patch })` |
| `usePermissions()` | `→ { data: Permission[], isLoading }` |

**Known limitation**: `Group.memberCount` is a denormalized counter from fixtures. It does NOT auto-update when users are created or group assignments change in this story. This is acceptable for mock demo — note it in completion notes.

---

### Step 0: Install shadcn checkbox

```bash
cd src/OneId.Web
npx shadcn@latest add checkbox
```

Creates `src/components/ui/checkbox.tsx`. Verify it exists before implementing.

---

### Step 1: mockStore Extensions

**File: `src/mocks/store.ts`** — MODIFY

**Add `deleteGroup`** (add after `updateGroup`):
```typescript
deleteGroup: (tenantId: string, groupId: string): void => {
  state.groups = state.groups.filter((g) => !(g.id === groupId && g.tenantId === tenantId))
},
```

**Replace `deleteRoleSet`** with 409 guard:
```typescript
deleteRoleSet: (tenantId: string, roleSetId: string): void => {
  const usedByGroup = state.groups.some(
    (g) => g.tenantId === tenantId && g.roleSetIds.includes(roleSetId),
  )
  if (usedByGroup) {
    const groups = state.groups.filter(
      (g) => g.tenantId === tenantId && g.roleSetIds.includes(roleSetId),
    )
    throw Object.assign(new Error('RoleSet is assigned to groups'), {
      status: 409,
      assignedTo: groups.map((g) => g.name),
    })
  }
  state.roleSets = state.roleSets.filter(
    (rs) => !(rs.id === roleSetId && rs.tenantId === tenantId),
  )
},
```

---

### Step 2: Add `useDeleteGroup` Hook

**File: `src/queries/hooks/useGroups.ts`** — MODIFY (append after `useUpdateGroup`):

```typescript
export function useDeleteGroup(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (groupId: string) => {
      await mockDelay(200)
      mockStore.deleteGroup(tenantId, groupId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.groups(tenantId) })
    },
  })
}
```

The barrel (`src/queries/hooks/index.ts`) already does `export * from './useGroups'` — no changes needed there.

---

### Step 3: Reusable DeleteDialog

**File: `src/routes/internal/tenants/_delete-dialog.tsx`** — NEW

```typescript
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

export function DeleteDialog({
  entityName,
  isOpen,
  onClose,
  onConfirm,
  isPending,
  error,
}: {
  entityName: string
  isOpen: boolean
  onClose: () => void
  onConfirm: () => void
  isPending: boolean
  error?: string | null
}) {
  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) onClose() }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete &ldquo;{entityName}&rdquo;?</DialogTitle>
          <DialogDescription>This action cannot be undone.</DialogDescription>
        </DialogHeader>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={onConfirm} disabled={isPending}>
            {isPending ? 'Deleting…' : 'Delete'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
```

---

### Step 4: TenantRolesPage CRUD

**File: `src/routes/internal/tenants/TenantRolesPage.tsx`** — MODIFY (full rewrite)

```typescript
import { useState } from 'react'
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { DeleteDialog } from './_delete-dialog'
import { useRoles, useCreateRole, useUpdateRole, useDeleteRole, usePermissions } from '@/queries/hooks'
import type { Role, Permission } from '@/mocks/types'

// ── Permission multi-select ──────────────────────────────────────────────────

function PermissionSelect({
  permissions,
  selected,
  onChange,
}: {
  permissions: Permission[]
  selected: string[]
  onChange: (ids: string[]) => void
}) {
  const [search, setSearch] = useState('')
  const filtered = permissions.filter(
    (p) =>
      p.id.toLowerCase().includes(search.toLowerCase()) ||
      p.description.toLowerCase().includes(search.toLowerCase()),
  )
  const toggle = (id: string) =>
    onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id])

  return (
    <div className="space-y-2">
      <Input
        placeholder="Search permissions…"
        value={search}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearch(e.target.value)}
      />
      <div className="max-h-48 overflow-y-auto rounded-md border border-border bg-background p-2 space-y-1">
        {filtered.length === 0 ? (
          <p className="text-sm text-muted-foreground px-1">No matches.</p>
        ) : (
          filtered.map((p) => (
            <label key={p.id} className="flex items-center gap-2 px-1 py-0.5 cursor-pointer rounded hover:bg-card">
              <Checkbox
                checked={selected.includes(p.id)}
                onCheckedChange={() => toggle(p.id)}
              />
              <span className="text-sm text-foreground font-mono">{p.id}</span>
            </label>
          ))
        )}
      </div>
      <p className="text-xs text-muted-foreground">{selected.length} selected</p>
    </div>
  )
}

// ── Role Form Dialog ─────────────────────────────────────────────────────────

function RoleFormDialog({
  isOpen,
  onClose,
  initial,
  tenantId,
  permissions,
}: {
  isOpen: boolean
  onClose: () => void
  initial: Role | null
  tenantId: string
  permissions: Permission[]
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [nameError, setNameError] = useState('')
  const [selectedPermIds, setSelectedPermIds] = useState<string[]>(initial?.permissionIds ?? [])
  const createRole = useCreateRole(tenantId)
  const updateRole = useUpdateRole(tenantId)

  const mutation = initial ? updateRole : createRole
  const isEditing = !!initial

  const validateName = () => {
    if (!name.trim()) { setNameError('Name is required.'); return false }
    setNameError('')
    return true
  }

  const handleSubmit = () => {
    if (!validateName()) return
    if (isEditing) {
      updateRole.mutate(
        { roleId: initial.id, patch: { name: name.trim(), permissionIds: selectedPermIds } },
        { onSuccess: onClose },
      )
    } else {
      createRole.mutate(
        { name: name.trim(), permissionIds: selectedPermIds },
        { onSuccess: onClose },
      )
    }
  }

  const handleClose = () => {
    setName(initial?.name ?? '')
    setNameError('')
    setSelectedPermIds(initial?.permissionIds ?? [])
    onClose()
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) handleClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Edit Role' : 'Create Role'}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="role-name">Name</Label>
            <Input
              id="role-name"
              value={name}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
              onBlur={validateName}
              placeholder="e.g. User Manager"
            />
            {nameError && <p className="text-sm text-destructive">{nameError}</p>}
          </div>
          <div className="space-y-1">
            <Label>Permissions</Label>
            <PermissionSelect
              permissions={permissions}
              selected={selectedPermIds}
              onChange={setSelectedPermIds}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : isEditing ? 'Save' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Page ─────────────────────────────────────────────────────────────────────

export function TenantRolesPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: roles = [], isLoading } = useRoles(tenantId)
  const { data: permissions = [] } = usePermissions()
  const deleteRole = useDeleteRole(tenantId)

  const [createOpen, setCreateOpen] = useState(false)
  const [editRole, setEditRole] = useState<Role | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<Role | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const handleDelete = () => {
    if (!deleteTarget) return
    setDeleteError(null)
    deleteRole.mutate(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
      onError: (err: unknown) => {
        const e = err as { status?: number; assignedTo?: string[] }
        if (e.status === 409 && e.assignedTo?.length) {
          setDeleteError(`Cannot delete: assigned to ${e.assignedTo.join(', ')}`)
        } else {
          setDeleteError('Failed to delete role.')
        }
      },
    })
  }

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
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <div className="flex items-center gap-2 justify-end">
          <Button variant="outline" size="sm" onClick={() => setEditRole(row.original)}>
            Edit
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => { setDeleteError(null); setDeleteTarget(row.original) }}
          >
            Delete
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Roles</h1>
        <Button size="sm" onClick={() => setCreateOpen(true)}>Create Role</Button>
      </div>

      {!isLoading && roles.length === 0 ? (
        <EmptyState variant="no-data" title="No roles" description="Create a role to get started." />
      ) : (
        <DataTable columns={columns} data={roles} isLoading={isLoading} aria-label="Roles list" />
      )}

      <RoleFormDialog
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        initial={null}
        tenantId={tenantId}
        permissions={permissions}
      />
      <RoleFormDialog
        isOpen={!!editRole}
        onClose={() => setEditRole(null)}
        initial={editRole}
        tenantId={tenantId}
        permissions={permissions}
      />
      <DeleteDialog
        entityName={deleteTarget?.name ?? ''}
        isOpen={!!deleteTarget}
        onClose={() => { setDeleteTarget(null); setDeleteError(null) }}
        onConfirm={handleDelete}
        isPending={deleteRole.isPending}
        error={deleteError}
      />
    </div>
  )
}
```

---

### Step 5: TenantRoleSetsPage CRUD

**File: `src/routes/internal/tenants/TenantRoleSetsPage.tsx`** — MODIFY (full rewrite)

Same pattern as Roles. Role multi-select instead of permission multi-select.

```typescript
import { useState } from 'react'
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { DeleteDialog } from './_delete-dialog'
import { useRoleSets, useCreateRoleSet, useUpdateRoleSet, useDeleteRoleSet, useRoles } from '@/queries/hooks'
import type { RoleSet, Role } from '@/mocks/types'

function RoleSelectList({
  roles,
  selected,
  onChange,
}: {
  roles: Role[]
  selected: string[]
  onChange: (ids: string[]) => void
}) {
  const [search, setSearch] = useState('')
  const filtered = roles.filter((r) =>
    r.name.toLowerCase().includes(search.toLowerCase()),
  )
  const toggle = (id: string) =>
    onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id])

  return (
    <div className="space-y-2">
      <Input
        placeholder="Search roles…"
        value={search}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearch(e.target.value)}
      />
      <div className="max-h-40 overflow-y-auto rounded-md border border-border bg-background p-2 space-y-1">
        {filtered.length === 0 ? (
          <p className="text-sm text-muted-foreground px-1">No matches.</p>
        ) : (
          filtered.map((r) => (
            <label key={r.id} className="flex items-center gap-2 px-1 py-0.5 cursor-pointer rounded hover:bg-card">
              <Checkbox
                checked={selected.includes(r.id)}
                onCheckedChange={() => toggle(r.id)}
              />
              <span className="text-sm text-foreground">{r.name}</span>
            </label>
          ))
        )}
      </div>
      <p className="text-xs text-muted-foreground">{selected.length} selected</p>
    </div>
  )
}

function RoleSetFormDialog({
  isOpen,
  onClose,
  initial,
  tenantId,
  roles,
}: {
  isOpen: boolean
  onClose: () => void
  initial: RoleSet | null
  tenantId: string
  roles: Role[]
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [nameError, setNameError] = useState('')
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>(initial?.roleIds ?? [])
  const createRoleSet = useCreateRoleSet(tenantId)
  const updateRoleSet = useUpdateRoleSet(tenantId)

  const mutation = initial ? updateRoleSet : createRoleSet
  const isEditing = !!initial

  const validateName = () => {
    if (!name.trim()) { setNameError('Name is required.'); return false }
    setNameError('')
    return true
  }

  const handleSubmit = () => {
    if (!validateName()) return
    if (isEditing) {
      updateRoleSet.mutate(
        { roleSetId: initial.id, patch: { name: name.trim(), roleIds: selectedRoleIds } },
        { onSuccess: onClose },
      )
    } else {
      createRoleSet.mutate(
        { name: name.trim(), roleIds: selectedRoleIds },
        { onSuccess: onClose },
      )
    }
  }

  const handleClose = () => {
    setName(initial?.name ?? '')
    setNameError('')
    setSelectedRoleIds(initial?.roleIds ?? [])
    onClose()
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) handleClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Edit Role Set' : 'Create Role Set'}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="roleset-name">Name</Label>
            <Input
              id="roleset-name"
              value={name}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
              onBlur={validateName}
              placeholder="e.g. Managers Bundle"
            />
            {nameError && <p className="text-sm text-destructive">{nameError}</p>}
          </div>
          <div className="space-y-1">
            <Label>Roles</Label>
            <RoleSelectList roles={roles} selected={selectedRoleIds} onChange={setSelectedRoleIds} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : isEditing ? 'Save' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function TenantRoleSetsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: roleSets = [], isLoading } = useRoleSets(tenantId)
  const { data: roles = [] } = useRoles(tenantId)
  const deleteRoleSet = useDeleteRoleSet(tenantId)

  const [createOpen, setCreateOpen] = useState(false)
  const [editRoleSet, setEditRoleSet] = useState<RoleSet | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<RoleSet | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const handleDelete = () => {
    if (!deleteTarget) return
    setDeleteError(null)
    deleteRoleSet.mutate(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
      onError: (err: unknown) => {
        const e = err as { status?: number; assignedTo?: string[] }
        if (e.status === 409 && e.assignedTo?.length) {
          setDeleteError(`Cannot delete: assigned to ${e.assignedTo.join(', ')}`)
        } else {
          setDeleteError('Failed to delete role set.')
        }
      },
    })
  }

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
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <div className="flex items-center gap-2 justify-end">
          <Button variant="outline" size="sm" onClick={() => setEditRoleSet(row.original)}>
            Edit
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => { setDeleteError(null); setDeleteTarget(row.original) }}
          >
            Delete
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Role Sets</h1>
        <Button size="sm" onClick={() => setCreateOpen(true)}>Create Role Set</Button>
      </div>

      {!isLoading && roleSets.length === 0 ? (
        <EmptyState variant="no-data" title="No role sets" description="Create a role set to get started." />
      ) : (
        <DataTable columns={columns} data={roleSets} isLoading={isLoading} aria-label="Role Sets list" />
      )}

      <RoleSetFormDialog
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        initial={null}
        tenantId={tenantId}
        roles={roles}
      />
      <RoleSetFormDialog
        isOpen={!!editRoleSet}
        onClose={() => setEditRoleSet(null)}
        initial={editRoleSet}
        tenantId={tenantId}
        roles={roles}
      />
      <DeleteDialog
        entityName={deleteTarget?.name ?? ''}
        isOpen={!!deleteTarget}
        onClose={() => { setDeleteTarget(null); setDeleteError(null) }}
        onConfirm={handleDelete}
        isPending={deleteRoleSet.isPending}
        error={deleteError}
      />
    </div>
  )
}
```

---

### Step 6: TenantGroupsPage CRUD

**File: `src/routes/internal/tenants/TenantGroupsPage.tsx`** — MODIFY (full rewrite)

Groups have both `roleIds` and `roleSetIds`. The form has two checkbox lists. Delete has no 409 guard.

```typescript
import { useState } from 'react'
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { DeleteDialog } from './_delete-dialog'
import {
  useGroups,
  useCreateGroup,
  useUpdateGroup,
  useDeleteGroup,
  useRoles,
  useRoleSets,
} from '@/queries/hooks'
import type { Group, Role, RoleSet } from '@/mocks/types'

function CheckboxList<T extends { id: string; name: string }>({
  items,
  selected,
  onChange,
  placeholder,
}: {
  items: T[]
  selected: string[]
  onChange: (ids: string[]) => void
  placeholder: string
}) {
  const [search, setSearch] = useState('')
  const filtered = items.filter((item) =>
    item.name.toLowerCase().includes(search.toLowerCase()),
  )
  const toggle = (id: string) =>
    onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id])

  return (
    <div className="space-y-2">
      <Input
        placeholder={placeholder}
        value={search}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearch(e.target.value)}
      />
      <div className="max-h-32 overflow-y-auto rounded-md border border-border bg-background p-2 space-y-1">
        {filtered.length === 0 ? (
          <p className="text-sm text-muted-foreground px-1">No matches.</p>
        ) : (
          filtered.map((item) => (
            <label key={item.id} className="flex items-center gap-2 px-1 py-0.5 cursor-pointer rounded hover:bg-card">
              <Checkbox
                checked={selected.includes(item.id)}
                onCheckedChange={() => toggle(item.id)}
              />
              <span className="text-sm text-foreground">{item.name}</span>
            </label>
          ))
        )}
      </div>
      <p className="text-xs text-muted-foreground">{selected.length} selected</p>
    </div>
  )
}

function GroupFormDialog({
  isOpen,
  onClose,
  initial,
  tenantId,
  roles,
  roleSets,
}: {
  isOpen: boolean
  onClose: () => void
  initial: Group | null
  tenantId: string
  roles: Role[]
  roleSets: RoleSet[]
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [nameError, setNameError] = useState('')
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>(initial?.roleIds ?? [])
  const [selectedRoleSetIds, setSelectedRoleSetIds] = useState<string[]>(initial?.roleSetIds ?? [])
  const createGroup = useCreateGroup(tenantId)
  const updateGroup = useUpdateGroup(tenantId)

  const mutation = initial ? updateGroup : createGroup
  const isEditing = !!initial

  const validateName = () => {
    if (!name.trim()) { setNameError('Name is required.'); return false }
    setNameError('')
    return true
  }

  const handleSubmit = () => {
    if (!validateName()) return
    if (isEditing) {
      updateGroup.mutate(
        {
          groupId: initial.id,
          patch: {
            name: name.trim(),
            roleIds: selectedRoleIds,
            roleSetIds: selectedRoleSetIds,
          },
        },
        { onSuccess: onClose },
      )
    } else {
      createGroup.mutate(
        {
          tenantId,
          name: name.trim(),
          memberCount: 0,
          roleIds: selectedRoleIds,
          roleSetIds: selectedRoleSetIds,
        },
        { onSuccess: onClose },
      )
    }
  }

  const handleClose = () => {
    setName(initial?.name ?? '')
    setNameError('')
    setSelectedRoleIds(initial?.roleIds ?? [])
    setSelectedRoleSetIds(initial?.roleSetIds ?? [])
    onClose()
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) handleClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Edit Group' : 'Create Group'}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="group-name">Name</Label>
            <Input
              id="group-name"
              value={name}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
              onBlur={validateName}
              placeholder="e.g. HR Team"
            />
            {nameError && <p className="text-sm text-destructive">{nameError}</p>}
          </div>
          <div className="space-y-1">
            <Label>Roles</Label>
            <CheckboxList
              items={roles}
              selected={selectedRoleIds}
              onChange={setSelectedRoleIds}
              placeholder="Search roles…"
            />
          </div>
          <div className="space-y-1">
            <Label>Role Sets</Label>
            <CheckboxList
              items={roleSets}
              selected={selectedRoleSetIds}
              onChange={setSelectedRoleSetIds}
              placeholder="Search role sets…"
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : isEditing ? 'Save' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function TenantGroupsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: groups = [], isLoading } = useGroups(tenantId)
  const { data: roles = [] } = useRoles(tenantId)
  const { data: roleSets = [] } = useRoleSets(tenantId)
  const deleteGroup = useDeleteGroup(tenantId)

  const [createOpen, setCreateOpen] = useState(false)
  const [editGroup, setEditGroup] = useState<Group | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<Group | null>(null)

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
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <div className="flex items-center gap-2 justify-end">
          <Button variant="outline" size="sm" onClick={() => setEditGroup(row.original)}>
            Edit
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setDeleteTarget(row.original)}
          >
            Delete
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Groups</h1>
        <Button size="sm" onClick={() => setCreateOpen(true)}>Create Group</Button>
      </div>

      {!isLoading && groups.length === 0 ? (
        <EmptyState variant="no-data" title="No groups" description="Create a group to get started." />
      ) : (
        <DataTable columns={columns} data={groups} isLoading={isLoading} aria-label="Groups list" />
      )}

      <GroupFormDialog
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        initial={null}
        tenantId={tenantId}
        roles={roles}
        roleSets={roleSets}
      />
      <GroupFormDialog
        isOpen={!!editGroup}
        onClose={() => setEditGroup(null)}
        initial={editGroup}
        tenantId={tenantId}
        roles={roles}
        roleSets={roleSets}
      />
      <DeleteDialog
        entityName={deleteTarget?.name ?? ''}
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={() => {
          if (!deleteTarget) return
          deleteGroup.mutate(deleteTarget.id, { onSuccess: () => setDeleteTarget(null) })
        }}
        isPending={deleteGroup.isPending}
      />
    </div>
  )
}
```

---

### Step 7: TenantUsersPage CRUD

**File: `src/routes/internal/tenants/TenantUsersPage.tsx`** — MODIFY (full rewrite)

Users: Create dialog (name, email, status, group multi-select) + Activate/Deactivate button per row.

```typescript
import { useState } from 'react'
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useUsers, useCreateUser, useUpdateUser, useGroups } from '@/queries/hooks'
import type { User, Group } from '@/mocks/types'

function GroupSelectList({
  groups,
  selected,
  onChange,
}: {
  groups: Group[]
  selected: string[]
  onChange: (ids: string[]) => void
}) {
  const [search, setSearch] = useState('')
  const filtered = groups.filter((g) =>
    g.name.toLowerCase().includes(search.toLowerCase()),
  )
  const toggle = (id: string) =>
    onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id])

  return (
    <div className="space-y-2">
      <Input
        placeholder="Search groups…"
        value={search}
        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearch(e.target.value)}
      />
      <div className="max-h-32 overflow-y-auto rounded-md border border-border bg-background p-2 space-y-1">
        {filtered.length === 0 ? (
          <p className="text-sm text-muted-foreground px-1">No matches.</p>
        ) : (
          filtered.map((g) => (
            <label key={g.id} className="flex items-center gap-2 px-1 py-0.5 cursor-pointer rounded hover:bg-card">
              <Checkbox
                checked={selected.includes(g.id)}
                onCheckedChange={() => toggle(g.id)}
              />
              <span className="text-sm text-foreground">{g.name}</span>
            </label>
          ))
        )}
      </div>
      <p className="text-xs text-muted-foreground">{selected.length} selected</p>
    </div>
  )
}

function CreateUserDialog({
  isOpen,
  onClose,
  tenantId,
  groups,
}: {
  isOpen: boolean
  onClose: () => void
  tenantId: string
  groups: Group[]
}) {
  const [name, setName] = useState('')
  const [nameError, setNameError] = useState('')
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState('')
  const [status, setStatus] = useState<'active' | 'inactive'>('active')
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>([])
  const createUser = useCreateUser(tenantId)

  const validateName = () => {
    if (!name.trim()) { setNameError('Name is required.'); return false }
    setNameError('')
    return true
  }

  const validateEmail = () => {
    if (!email.trim()) { setEmailError('Email is required.'); return false }
    if (!email.includes('@')) { setEmailError('Enter a valid email address.'); return false }
    setEmailError('')
    return true
  }

  const handleSubmit = () => {
    const nameOk = validateName()
    const emailOk = validateEmail()
    if (!nameOk || !emailOk) return
    createUser.mutate(
      {
        tenantId,
        name: name.trim(),
        email: email.trim(),
        status,
        groupIds: selectedGroupIds,
        lastLogin: null,
      },
      {
        onSuccess: () => {
          setName(''); setEmail(''); setStatus('active'); setSelectedGroupIds([])
          onClose()
        },
      },
    )
  }

  const handleClose = () => {
    setName(''); setNameError(''); setEmail(''); setEmailError('')
    setStatus('active'); setSelectedGroupIds([])
    onClose()
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) handleClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create User</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="user-name">Name</Label>
            <Input
              id="user-name"
              value={name}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
              onBlur={validateName}
              placeholder="e.g. Jane Doe"
            />
            {nameError && <p className="text-sm text-destructive">{nameError}</p>}
          </div>
          <div className="space-y-1">
            <Label htmlFor="user-email">Email</Label>
            <Input
              id="user-email"
              type="email"
              value={email}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEmail(e.target.value)}
              onBlur={validateEmail}
              placeholder="e.g. jane@example.com"
            />
            {emailError && <p className="text-sm text-destructive">{emailError}</p>}
          </div>
          <div className="space-y-1">
            <Label>Status</Label>
            <div className="flex gap-2">
              <Button
                type="button"
                variant={status === 'active' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setStatus('active')}
              >
                Active
              </Button>
              <Button
                type="button"
                variant={status === 'inactive' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setStatus('inactive')}
              >
                Inactive
              </Button>
            </div>
          </div>
          <div className="space-y-1">
            <Label>Groups</Label>
            <GroupSelectList groups={groups} selected={selectedGroupIds} onChange={setSelectedGroupIds} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={createUser.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createUser.isPending}>
            {createUser.isPending ? 'Creating…' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function TenantUsersPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: users = [], isLoading } = useUsers(tenantId)
  const { data: groups = [] } = useGroups(tenantId)
  const updateUser = useUpdateUser(tenantId)
  const [createOpen, setCreateOpen] = useState(false)

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
            {new Date(v).toLocaleDateString('en-GB', {
              day: '2-digit',
              month: 'short',
              year: 'numeric',
            })}
          </span>
        )
      },
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => {
        const user = row.original
        const isActive = user.status === 'active'
        return (
          <div className="flex justify-end">
            <Button
              variant="outline"
              size="sm"
              disabled={updateUser.isPending}
              onClick={() =>
                updateUser.mutate({
                  userId: user.id,
                  patch: { status: isActive ? 'inactive' : 'active' },
                })
              }
            >
              {isActive ? 'Deactivate' : 'Activate'}
            </Button>
          </div>
        )
      },
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Users</h1>
        <Button size="sm" onClick={() => setCreateOpen(true)}>Create User</Button>
      </div>

      {!isLoading && users.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No users"
          description="Create a user to get started."
        />
      ) : (
        <DataTable columns={columns} data={users} isLoading={isLoading} aria-label="Users list" />
      )}

      <CreateUserDialog
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        tenantId={tenantId}
        groups={groups}
      />
    </div>
  )
}
```

---

### Critical Implementation Notes

1. **`handleClose` resets form state** — Each form dialog resets all local state on close (both programmatic close via `onClose` and the Dialog's own `onOpenChange`). Without this, reopening a create dialog shows stale data from the previous interaction.

2. **Dual `RoleFormDialog` instances** — The page renders TWO `RoleFormDialog`s: one for create (`initial={null}`) and one for edit (`initial={editRole}`). This is correct — React reconciles them by position, so state doesn't leak. The same pattern applies to all four entity pages.

3. **`Checkbox` from shadcn** — Import from `@/components/ui/checkbox`. The `onCheckedChange` prop receives `boolean | 'indeterminate'` but you only need the toggle — ignore the value and call your toggle function: `onCheckedChange={() => toggle(id)}`.

4. **`ColumnDef<T, unknown>` with arrow functions** — Column `cell` renderers that use `useState` internally CANNOT be defined in the column array (columns are not React components, they're config objects). Multi-select lists live in separate named components (`PermissionSelect`, `CheckboxList`, etc.). The `cell` in column definitions only renders static JSX or calls simple handlers passed from the page.

5. **`deleteRoleSet` throws like `deleteRole`** — After the mockStore change, it throws `Object.assign(new Error(...), { status: 409, assignedTo: string[] })`. The same `onError` handler pattern from Roles applies verbatim to RoleSets.

6. **`createGroup` data shape** — `useCreateGroup` calls `mockStore.createGroup(data: Omit<Group,'id'>)`, so you must pass `tenantId` explicitly: `{ tenantId, name, memberCount: 0, roleIds: [], roleSetIds: [] }`. Unlike `createRole` which accepts `Omit<Role,'id'|'tenantId'>` and adds `tenantId` internally, `useCreateGroup` passes data directly to `mockStore.createGroup`.

7. **`memberCount` stays stale** — Group `memberCount` is a denormalized counter seeded from fixtures. Creating a user and assigning them to a group does NOT increment `memberCount`. This is an accepted mock limitation — document in completion notes.

8. **`useDeleteRoleSet` mutation error type** — TanStack Query v5 `useMutation` types errors as `Error`. Cast to `unknown` then check `(err as { status?: number; assignedTo?: string[] })`. Same pattern as `deleteRole` in this story.

9. **ESLint: `cursor-pointer` is safe** — It's a layout/behavior utility, not a color token. The design-token rule only restricts color classes (`bg-*`, `text-*`, `border-*`). Layout utilities are fine.

10. **`useDeleteGroup` barrel export** — No changes to `src/queries/hooks/index.ts` needed. It already does `export * from './useGroups'` which re-exports everything from that file including the new `useDeleteGroup`.

---

### File Structure After This Story

```
src/OneId.Web/src/
  components/
    ui/
      checkbox.tsx                                    ← NEW (shadcn install)
  mocks/
    store.ts                                          ← MODIFY (deleteGroup, deleteRoleSet 409)
  queries/
    hooks/
      useGroups.ts                                    ← MODIFY (add useDeleteGroup)
  routes/
    internal/
      tenants/
        _delete-dialog.tsx                            ← NEW
        TenantRolesPage.tsx                           ← MODIFY (CRUD)
        TenantRoleSetsPage.tsx                        ← MODIFY (CRUD)
        TenantGroupsPage.tsx                          ← MODIFY (CRUD)
        TenantUsersPage.tsx                           ← MODIFY (CRUD)
```

---

### Demo Flow After This Story

1. Navigate to `/internal/tenants/acme-corp/roles` → see 6 roles
2. Click **Create Role** → enter "CRM Viewer" → check `od.roles.read`, `od.users.read` → Create
3. New role appears in table
4. Click **Edit** on existing role → change permissions → Save
5. Click **Delete** on "User Viewer" → confirm → it's removed
6. Try deleting "Full Admin" (assigned to Administrators group) → error "Cannot delete: assigned to Administrators"
7. Navigate to **Role Sets** → Create "CRM Bundle" with CRM Viewer → appears in list
8. Navigate to **Groups** → Create "CRM Team" → assign CRM Bundle → Create
9. Navigate to **Users** → Create "Jane Doe" + assign to CRM Team → appears in list
10. Click **Deactivate** on a user → badge changes to Inactive → click **Activate** to restore

---

### Tests

No new test files required. Run:

```bash
npm test -- --run      # 38 tests must still pass
npm run build          # TypeScript + Vite build clean
npm run lint           # no new ESLint errors
```

---

## References

- [TenantRolesPage](src/OneId.Web/src/routes/internal/tenants/TenantRolesPage.tsx) — MODIFY
- [TenantRoleSetsPage](src/OneId.Web/src/routes/internal/tenants/TenantRoleSetsPage.tsx) — MODIFY
- [TenantGroupsPage](src/OneId.Web/src/routes/internal/tenants/TenantGroupsPage.tsx) — MODIFY
- [TenantUsersPage](src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx) — MODIFY
- [mock store](src/OneId.Web/src/mocks/store.ts) — MODIFY
- [useGroups hook](src/OneId.Web/src/queries/hooks/useGroups.ts) — MODIFY
- [mock types](src/OneId.Web/src/mocks/types.ts) — reference only
- [fixtures](src/OneId.Web/src/mocks/fixtures.ts) — reference (data shapes)
- [query keys](src/OneId.Web/src/queries/keys.ts) — reference
- [useRoles hook](src/OneId.Web/src/queries/hooks/useRoles.ts) — reference (deleteRole 409 pattern)
- [useRoleSets hook](src/OneId.Web/src/queries/hooks/useRoleSets.ts) — MODIFY (mockStore 409 affects this)
- [5c-1 story](./_5c-1-tenant-admin-management-pages.md) — patterns, gotchas

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- shadcn installer created `checkbox.tsx` in a literal `@/` directory due to Windows path alias resolution. Fixed by manually copying to `src/components/ui/checkbox.tsx`.

### Completion Notes List

- All 4 entity pages (Roles, RoleSets, Groups, Users) now have full CRUD with Dialog-based forms and permission/role/group checkbox multi-selects.
- Reusable `DeleteDialog` component created for confirm-then-delete flow with inline 409 error display.
- `deleteGroup` added to mockStore. `deleteRoleSet` extended with same 409 guard pattern as `deleteRole`.
- `useDeleteGroup` hook added and exported via existing barrel (`export * from './useGroups'`).
- `Group.memberCount` stays stale — denormalized counter from fixtures, not updated when users join groups. Accepted mock limitation.
- Pre-existing lint issues: `TenantDetailPage.tsx` (react-hooks/set-state-in-effect error) and `DataTable.tsx` (incompatible-library warning). Not introduced by this story.
- Build: clean. Tests: 38/38 pass. Lint: no new issues.

### File List

- src/OneId.Web/src/components/ui/checkbox.tsx (NEW — shadcn Checkbox)
- src/OneId.Web/src/mocks/store.ts (MODIFY — deleteGroup, deleteRoleSet 409 guard)
- src/OneId.Web/src/queries/hooks/useGroups.ts (MODIFY — useDeleteGroup)
- src/OneId.Web/src/routes/internal/tenants/_delete-dialog.tsx (NEW — reusable DeleteDialog)
- src/OneId.Web/src/routes/internal/tenants/TenantRolesPage.tsx (MODIFY — full CRUD)
- src/OneId.Web/src/routes/internal/tenants/TenantRoleSetsPage.tsx (MODIFY — full CRUD)
- src/OneId.Web/src/routes/internal/tenants/TenantGroupsPage.tsx (MODIFY — full CRUD)
- src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx (MODIFY — create + activate/deactivate)

## Change Log

- 2026-05-23: Story created — CRUD forms for Roles, Role Sets, Groups, Users within tenant context (mock data demo).
- 2026-05-23: Implementation complete — all 8 tasks done, build/lint/test passing.
