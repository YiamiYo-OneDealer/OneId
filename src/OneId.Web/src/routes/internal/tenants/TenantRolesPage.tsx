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
import type { RoleDto, PermissionDto } from '@/api/types'

// ── Permission multi-select ──────────────────────────────────────────────────

function PermissionSelect({
  permissions,
  selected,
  onChange,
}: {
  permissions: PermissionDto[]
  selected: string[]
  onChange: (ids: string[]) => void
}) {
  const [search, setSearch] = useState('')
  const filtered = permissions.filter(
    (p) =>
      p.permissionId.toLowerCase().includes(search.toLowerCase()) ||
      p.label.toLowerCase().includes(search.toLowerCase()),
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
            <div key={p.permissionId} className="flex items-center gap-2 px-1 py-0.5 rounded hover:bg-card">
              <Checkbox
                id={`perm-${p.permissionId}`}
                checked={selected.includes(p.permissionId)}
                onCheckedChange={() => toggle(p.permissionId)}
              />
              <label htmlFor={`perm-${p.permissionId}`} className="flex-1 cursor-pointer">
                <span className="text-sm text-foreground font-mono">{p.permissionId}</span>
                <span className="text-xs text-muted-foreground ml-2">{p.label}</span>
              </label>
            </div>
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
  initial: RoleDto | null
  tenantId: string
  permissions: PermissionDto[]
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
        { roleId: initial.id, patch: { name: name.trim(), permissionIds: selectedPermIds, version: initial.version } },
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
          <Button variant="outline" onClick={handleClose} disabled={createRole.isPending || updateRole.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createRole.isPending || updateRole.isPending}>
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
  const [editRole, setEditRole] = useState<RoleDto | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<RoleDto | null>(null)
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

  const columns: ColumnDef<RoleDto, unknown>[] = [
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
      {editRole && (
        <RoleFormDialog
          isOpen={true}
          onClose={() => setEditRole(null)}
          initial={editRole}
          tenantId={tenantId}
          permissions={permissions}
        />
      )}
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
