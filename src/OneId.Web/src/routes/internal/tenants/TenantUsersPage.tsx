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
import { useTenant } from '@/queries/hooks/useTenants'
import { SeatUsageIndicator, isSeatLimitReached } from '@/components/shared/SeatUsageIndicator'
import { DisabledButtonWithTooltip } from '@/components/shared/DisabledButtonWithTooltip'
import type { User, Group } from '@/mocks/types'

// ── Group select list ─────────────────────────────────────────────────────────

function GroupSelectList({
  groups,
  selected,
  onChange,
  idPrefix = 'user-grp',
}: {
  groups: Group[]
  selected: string[]
  onChange: (ids: string[]) => void
  idPrefix?: string
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
            <div key={g.id} className="flex items-center gap-2 px-1 py-0.5 rounded hover:bg-card">
              <Checkbox
                id={`${idPrefix}-${g.id}`}
                checked={selected.includes(g.id)}
                onCheckedChange={() => toggle(g.id)}
              />
              <label htmlFor={`${idPrefix}-${g.id}`} className="text-sm text-foreground cursor-pointer flex-1">
                {g.name}
              </label>
            </div>
          ))
        )}
      </div>
      <p className="text-xs text-muted-foreground">{selected.length} selected</p>
    </div>
  )
}

// ── Create User Dialog ────────────────────────────────────────────────────────

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

// ── Edit User Dialog ──────────────────────────────────────────────────────────

function EditUserDialog({
  user,
  onClose,
  groups,
  updateUser,
}: {
  user: User
  onClose: () => void
  groups: Group[]
  updateUser: ReturnType<typeof useUpdateUser>
}) {
  const [name, setName] = useState(user.name)
  const [nameError, setNameError] = useState('')
  const [email, setEmail] = useState(user.email)
  const [emailError, setEmailError] = useState('')
  const [status, setStatus] = useState<'active' | 'inactive'>(user.status)
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>(user.groupIds)
  const [saveError, setSaveError] = useState<string | null>(null)

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
    setSaveError(null)
    updateUser.mutate(
      {
        userId: user.id,
        patch: { name: name.trim(), email: email.trim(), status, groupIds: selectedGroupIds },
      },
      {
        onSuccess: onClose,
        onError: () => setSaveError('Failed to save changes. Please try again.'),
      },
    )
  }

  return (
    <Dialog open onOpenChange={(open) => { if (!open && !updateUser.isPending) onClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Edit User</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="edit-user-name">Name</Label>
            <Input
              id="edit-user-name"
              value={name}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
              onBlur={validateName}
            />
            {nameError && <p className="text-sm text-destructive">{nameError}</p>}
          </div>
          <div className="space-y-1">
            <Label htmlFor="edit-user-email">Email</Label>
            <Input
              id="edit-user-email"
              type="email"
              value={email}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEmail(e.target.value)}
              onBlur={validateEmail}
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
            <GroupSelectList groups={groups} selected={selectedGroupIds} onChange={setSelectedGroupIds} idPrefix="edit-user-grp" />
          </div>
        </div>
        {saveError && <p className="text-sm text-destructive">{saveError}</p>}
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={updateUser.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={updateUser.isPending}>
            {updateUser.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Page ─────────────────────────────────────────────────────────────────────

export function TenantUsersPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: users = [], isLoading } = useUsers(tenantId)
  const { data: groups = [] } = useGroups(tenantId)
  const { data: tenant } = useTenant(tenantId)
  const updateUser = useUpdateUser(tenantId)
  const [createOpen, setCreateOpen] = useState(false)
  const [editUser, setEditUser] = useState<User | null>(null)

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
          <div className="flex justify-end gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={updateUser.isPending}
              onClick={() => setEditUser(user)}
            >
              Edit
            </Button>
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
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold text-foreground">Users</h1>
          {tenant && (
            <SeatUsageIndicator used={tenant.seatUsage.used} max={tenant.seatUsage.max} />
          )}
        </div>
        {tenant && isSeatLimitReached(tenant.seatUsage.used, tenant.seatUsage.max) ? (
          <DisabledButtonWithTooltip tooltip="Seat limit reached. Contact your administrator to expand your license.">
            <Button size="sm">Create User</Button>
          </DisabledButtonWithTooltip>
        ) : (
          <Button size="sm" onClick={() => setCreateOpen(true)}>Create User</Button>
        )}
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

      {editUser && (
        <EditUserDialog
          user={editUser}
          onClose={() => setEditUser(null)}
          groups={groups}
          updateUser={updateUser}
        />
      )}
    </div>
  )
}
