import { useState } from 'react'
import { useParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
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
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { UserDto, GroupDto } from '@/api/types'

// ── Group select list ─────────────────────────────────────────────────────────

function GroupSelectList({
  groups,
  selected,
  onChange,
  idPrefix = 'user-grp',
}: {
  groups: GroupDto[]
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
  groups: GroupDto[]
}) {
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState('')
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [isAssigningGroups, setIsAssigningGroups] = useState(false)
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>([])
  const createUser = useCreateUser(tenantId)
  const queryClient = useQueryClient()

  const validateEmail = () => {
    if (!email.trim()) { setEmailError('Email is required.'); return false }
    if (!email.includes('@')) { setEmailError('Enter a valid email address.'); return false }
    setEmailError('')
    return true
  }

  const handleSubmit = async () => {
    if (!validateEmail()) return
    setSubmitError(null)
    try {
      const newUser = await createUser.mutateAsync(
        { email: email.trim(), displayName: displayName.trim() || null },
      )
      if (selectedGroupIds.length > 0) {
        setIsAssigningGroups(true)
        const results = await Promise.allSettled(
          selectedGroupIds.map((groupId) =>
            apiClient.put(`api/tenant/groups/${groupId}/members`, { json: { userId: newUser.id } }),
          ),
        )
        setIsAssigningGroups(false)
        const failed = results.filter((r) => r.status === 'rejected')
        if (failed.length > 0) {
          queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
          setSubmitError(
            'User created but some group assignments failed. Please check the user\'s group memberships.',
          )
          return
        }
      }
      queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
      setDisplayName(''); setEmail(''); setSelectedGroupIds([])
      onClose()
    } catch {
      setIsAssigningGroups(false)
      setSubmitError('Failed to create user. Please try again.')
    }
  }

  const handleClose = () => {
    setDisplayName(''); setEmail(''); setEmailError(''); setSubmitError(null); setIsAssigningGroups(false); setSelectedGroupIds([])
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
            <Label htmlFor="user-display-name">Display Name</Label>
            <Input
              id="user-display-name"
              value={displayName}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDisplayName(e.target.value)}
              placeholder="e.g. Jane Doe"
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="user-email">Email *</Label>
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
            <Label>Groups</Label>
            <GroupSelectList groups={groups} selected={selectedGroupIds} onChange={setSelectedGroupIds} />
          </div>
          {submitError && <p className="text-sm text-destructive">{submitError}</p>}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={createUser.isPending || isAssigningGroups}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createUser.isPending || isAssigningGroups}>
            {createUser.isPending ? 'Creating…' : isAssigningGroups ? 'Assigning groups…' : 'Create'}
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
  updateUser,
}: {
  user: UserDto
  onClose: () => void
  updateUser: ReturnType<typeof useUpdateUser>
}) {
  const [displayName, setDisplayName] = useState(user.displayName ?? '')
  const [email, setEmail] = useState(user.email)
  const [emailError, setEmailError] = useState('')
  const [saveError, setSaveError] = useState<string | null>(null)

  const validateEmail = () => {
    if (!email.trim()) { setEmailError('Email is required.'); return false }
    if (!email.includes('@')) { setEmailError('Enter a valid email address.'); return false }
    setEmailError('')
    return true
  }

  const handleSubmit = () => {
    if (!validateEmail()) return
    setSaveError(null)
    updateUser.mutate(
      {
        userId: user.id,
        patch: { displayName: displayName.trim() || null, email: email.trim() },
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
            <Label htmlFor="edit-user-display-name">Display Name</Label>
            <Input
              id="edit-user-display-name"
              value={displayName}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDisplayName(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="edit-user-email">Email *</Label>
            <Input
              id="edit-user-email"
              type="email"
              value={email}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEmail(e.target.value)}
              onBlur={validateEmail}
            />
            {emailError && <p className="text-sm text-destructive">{emailError}</p>}
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
  const updateUser = useUpdateUser(tenantId)
  const [createOpen, setCreateOpen] = useState(false)
  const [editUser, setEditUser] = useState<UserDto | null>(null)

  const columns: ColumnDef<UserDto, unknown>[] = [
    {
      accessorKey: 'displayName',
      header: 'Name',
      cell: ({ row }) => (
        <span className="font-medium text-foreground">
          {row.original.displayName ?? row.original.email}
        </span>
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
      accessorKey: 'isActive',
      header: 'Status',
      cell: ({ row }) => (
        <Badge variant={row.original.isActive ? 'default' : 'secondary'}>
          {row.original.isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => {
        const user = row.original
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
                  patch: { isActive: !user.isActive },
                })
              }
            >
              {user.isActive ? 'Deactivate' : 'Activate'}
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

      {editUser && (
        <EditUserDialog
          user={editUser}
          onClose={() => setEditUser(null)}
          updateUser={updateUser}
        />
      )}
    </div>
  )
}
