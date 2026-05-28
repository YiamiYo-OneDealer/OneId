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
import type { GroupDto, RoleDto, RoleSetDto } from '@/api/types'

// ── Generic checkbox list ─────────────────────────────────────────────────────

function CheckboxList<T extends { id: string; name: string }>({
  items,
  selected,
  onChange,
  placeholder,
  listId,
}: {
  items: T[]
  selected: string[]
  onChange: (ids: string[]) => void
  placeholder: string
  listId: string
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
            <div key={item.id} className="flex items-center gap-2 px-1 py-0.5 rounded hover:bg-card">
              <Checkbox
                id={`${listId}-${item.id}`}
                checked={selected.includes(item.id)}
                onCheckedChange={() => toggle(item.id)}
              />
              <label htmlFor={`${listId}-${item.id}`} className="text-sm text-foreground cursor-pointer flex-1">
                {item.name}
              </label>
            </div>
          ))
        )}
      </div>
      <p className="text-xs text-muted-foreground">{selected.length} selected</p>
    </div>
  )
}

// ── Group Form Dialog ─────────────────────────────────────────────────────────

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
  initial: GroupDto | null
  tenantId: string
  roles: RoleDto[]
  roleSets: RoleSetDto[]
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [nameError, setNameError] = useState('')
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>(
    initial?.roles.map((r) => r.id) ?? [],
  )
  const [selectedRoleSetIds, setSelectedRoleSetIds] = useState<string[]>(
    initial?.roleSets.map((rs) => rs.id) ?? [],
  )
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
            version: initial.version,
          },
        },
        { onSuccess: onClose },
      )
    } else {
      createGroup.mutate(
        {
          name: name.trim(),
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
    setSelectedRoleIds(initial?.roles.map((r) => r.id) ?? [])
    setSelectedRoleSetIds(initial?.roleSets.map((rs) => rs.id) ?? [])
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
              listId="group-roles"
            />
          </div>
          <div className="space-y-1">
            <Label>Role Sets</Label>
            <CheckboxList
              items={roleSets}
              selected={selectedRoleSetIds}
              onChange={setSelectedRoleSetIds}
              placeholder="Search role sets…"
              listId="group-rolesets"
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={createGroup.isPending || updateGroup.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createGroup.isPending || updateGroup.isPending}>
            {mutation.isPending ? 'Saving…' : isEditing ? 'Save' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Page ─────────────────────────────────────────────────────────────────────

export function TenantGroupsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: groups = [], isLoading } = useGroups(tenantId)
  const { data: roles = [] } = useRoles(tenantId)
  const { data: roleSets = [] } = useRoleSets(tenantId)
  const deleteGroup = useDeleteGroup(tenantId)

  const [createOpen, setCreateOpen] = useState(false)
  const [editGroup, setEditGroup] = useState<GroupDto | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<GroupDto | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const columns: ColumnDef<GroupDto, unknown>[] = [
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
        <span className="text-muted-foreground">{row.original.roles.length}</span>
      ),
    },
    {
      id: 'roleSets',
      header: 'Role Sets',
      cell: ({ row }) => (
        <span className="text-muted-foreground">{row.original.roleSets.length}</span>
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
      {editGroup && (
        <GroupFormDialog
          isOpen={true}
          onClose={() => setEditGroup(null)}
          initial={editGroup}
          tenantId={tenantId}
          roles={roles}
          roleSets={roleSets}
        />
      )}
      <DeleteDialog
        entityName={deleteTarget?.name ?? ''}
        isOpen={!!deleteTarget}
        onClose={() => { setDeleteTarget(null); setDeleteError(null) }}
        onConfirm={() => {
          if (!deleteTarget) return
          setDeleteError(null)
          deleteGroup.mutate(deleteTarget.id, {
            onSuccess: () => setDeleteTarget(null),
            onError: () => setDeleteError('Failed to delete group.'),
          })
        }}
        isPending={deleteGroup.isPending}
        error={deleteError}
      />
    </div>
  )
}
