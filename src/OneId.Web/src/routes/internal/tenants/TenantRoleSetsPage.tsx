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

// ── Role select list ──────────────────────────────────────────────────────────

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

// ── RoleSet Form Dialog ───────────────────────────────────────────────────────

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

// ── Page ─────────────────────────────────────────────────────────────────────

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
