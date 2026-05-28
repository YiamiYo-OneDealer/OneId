import { useState } from 'react'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  usePermissions,
  useCreatePermission,
  useUpdatePermission,
  useDeactivatePermission,
  useActivatePermission,
} from '@/queries/hooks'
import type { PermissionDto } from '@/api/types'

const PERMISSION_ID_RE = /^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$/

// ── Add Permission Dialog ─────────────────────────────────────────────────────

function AddPermissionDialog({
  isOpen,
  onClose,
}: {
  isOpen: boolean
  onClose: () => void
}) {
  const [permissionId, setPermissionId] = useState('')
  const [label, setLabel] = useState('')
  const [permissionIdError, setPermissionIdError] = useState('')
  const [labelError, setLabelError] = useState('')
  const [serverError, setServerError] = useState('')

  const createPermission = useCreatePermission()

  const validatePermissionId = () => {
    if (!PERMISSION_ID_RE.test(permissionId)) {
      setPermissionIdError('Must be dot-notation with at least 3 segments, e.g. od.crm.feature.action')
      return false
    }
    setPermissionIdError('')
    return true
  }

  const validateLabel = () => {
    if (!label.trim()) {
      setLabelError('Label is required.')
      return false
    }
    setLabelError('')
    return true
  }

  const handleSubmit = () => {
    const pidOk = validatePermissionId()
    const lblOk = validateLabel()
    if (!pidOk || !lblOk) return

    setServerError('')
    createPermission.mutate(
      { permissionId, label: label.trim() },
      {
        onSuccess: () => {
          handleClose()
        },
        onError: async (err: unknown) => {
          try {
            const body = await (err as { response?: Response }).response?.json()
            if (body?.error === 'permission_id_taken') {
              setServerError('Permission ID already exists')
            } else {
              setServerError('Failed to create permission.')
            }
          } catch {
            setServerError('Failed to create permission.')
          }
        },
      },
    )
  }

  const handleClose = () => {
    setPermissionId('')
    setLabel('')
    setPermissionIdError('')
    setLabelError('')
    setServerError('')
    onClose()
  }

  const isValid =
    PERMISSION_ID_RE.test(permissionId) && label.trim().length > 0

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) handleClose() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Add Permission</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="new-permission-id">Permission ID</Label>
            <Input
              id="new-permission-id"
              value={permissionId}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setPermissionId(e.target.value)}
              onBlur={validatePermissionId}
              placeholder="e.g. od.crm.quotes.approve"
              className="font-mono"
            />
            {permissionIdError && (
              <p className="text-sm text-destructive">{permissionIdError}</p>
            )}
          </div>
          <div className="space-y-1">
            <Label htmlFor="new-permission-label">Label</Label>
            <Input
              id="new-permission-label"
              value={label}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setLabel(e.target.value)}
              onBlur={validateLabel}
              placeholder="e.g. Approve Quotes"
            />
            {labelError && <p className="text-sm text-destructive">{labelError}</p>}
          </div>
          {serverError && <p className="text-sm text-destructive">{serverError}</p>}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={createPermission.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={createPermission.isPending || !isValid}>
            {createPermission.isPending ? 'Creating…' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Edit Label Dialog ─────────────────────────────────────────────────────────

function EditPermissionDialog({
  permission,
  onClose,
}: {
  permission: PermissionDto | null
  onClose: () => void
}) {
  const [label, setLabel] = useState(permission?.label ?? '')
  const [labelError, setLabelError] = useState('')
  const [serverError, setServerError] = useState('')

  const updatePermission = useUpdatePermission()

  const validateLabel = () => {
    if (!label.trim()) {
      setLabelError('Label is required.')
      return false
    }
    setLabelError('')
    return true
  }

  const handleSubmit = () => {
    if (!validateLabel() || !permission) return

    setServerError('')
    updatePermission.mutate(
      { permissionId: permission.permissionId, body: { label: label.trim(), version: permission.version } },
      {
        onSuccess: () => onClose(),
        onError: async (err: unknown) => {
          try {
            const body = await (err as { response?: Response }).response?.json()
            if (body?.error === 'conflict') {
              setServerError('Someone else edited this permission — reload and try again')
            } else {
              setServerError('Failed to update permission.')
            }
          } catch {
            setServerError('Failed to update permission.')
          }
        },
      },
    )
  }

  if (!permission) return null

  return (
    <Dialog open={!!permission} onOpenChange={(open) => { if (!open) onClose() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Edit Permission</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label className="text-muted-foreground text-sm">Permission ID</Label>
            <p className="font-mono text-sm text-foreground">{permission.permissionId}</p>
          </div>
          <div className="space-y-1">
            <Label htmlFor="edit-permission-label">Label</Label>
            <Input
              id="edit-permission-label"
              value={label}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setLabel(e.target.value)}
              onBlur={validateLabel}
            />
            {labelError && <p className="text-sm text-destructive">{labelError}</p>}
          </div>
          {serverError && <p className="text-sm text-destructive">{serverError}</p>}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={updatePermission.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={updatePermission.isPending || !label.trim()}>
            {updatePermission.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Deactivate Dialog ─────────────────────────────────────────────────────────

function DeactivateDialog({
  permission,
  onClose,
}: {
  permission: PermissionDto | null
  onClose: () => void
}) {
  const [error, setError] = useState('')
  const deactivatePermission = useDeactivatePermission()

  const handleConfirm = () => {
    if (!permission) return
    setError('')
    deactivatePermission.mutate(permission.permissionId, {
      onSuccess: () => onClose(),
      onError: () => setError('Failed to deactivate permission.'),
    })
  }

  if (!permission) return null

  return (
    <Dialog open={!!permission} onOpenChange={(open) => { if (!open && !deactivatePermission.isPending) onClose() }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Deactivate &ldquo;{permission.permissionId}&rdquo;?</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          This permission will no longer be included in token claims. The record is retained for audit purposes.
        </p>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={deactivatePermission.isPending}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={handleConfirm} disabled={deactivatePermission.isPending}>
            {deactivatePermission.isPending ? 'Deactivating…' : 'Deactivate'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ── Page ─────────────────────────────────────────────────────────────────────

export function PermissionsPage() {
  const { data: permissions = [], isLoading } = usePermissions()
  const activatePermission = useActivatePermission()

  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<PermissionDto | null>(null)
  const [deactivateTarget, setDeactivateTarget] = useState<PermissionDto | null>(null)

  const sorted = [...permissions].sort((a, b) => {
    const aDomain = a.permissionId.split('.')[0]
    const bDomain = b.permissionId.split('.')[0]
    return aDomain !== bDomain
      ? aDomain.localeCompare(bDomain)
      : a.permissionId.localeCompare(b.permissionId)
  })

  const columns: ColumnDef<PermissionDto, unknown>[] = [
    {
      accessorKey: 'permissionId',
      header: 'Permission ID',
      cell: ({ row }) => (
        <span className="font-mono text-sm text-foreground">{row.original.permissionId}</span>
      ),
    },
    {
      id: 'domain',
      header: 'Domain',
      cell: ({ row }) => (
        <span className="capitalize text-muted-foreground">
          {row.original.permissionId.split('.')[0]}
        </span>
      ),
    },
    {
      accessorKey: 'label',
      header: 'Description',
      cell: ({ row }) => <span className="text-foreground">{row.original.label}</span>,
    },
    {
      accessorKey: 'status',
      header: 'Status',
      cell: ({ row }) => (
        <Badge variant={row.original.status === 'Active' ? 'default' : 'secondary'}>
          {row.original.status}
        </Badge>
      ),
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => {
        const perm = row.original
        const isActive = perm.status === 'Active'
        return (
          <div className="flex items-center gap-2 justify-end">
            <Button variant="outline" size="sm" onClick={() => setEditTarget(perm)}>
              Edit
            </Button>
            {isActive ? (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setDeactivateTarget(perm)}
              >
                Deactivate
              </Button>
            ) : (
              <Button
                size="sm"
                disabled={activatePermission.isPending}
                onClick={() => activatePermission.mutate(perm.permissionId)}
              >
                Activate
              </Button>
            )}
          </div>
        )
      },
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-foreground">Permissions</h1>
        <Button size="sm" onClick={() => setCreateOpen(true)}>Add Permission</Button>
      </div>

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

      <AddPermissionDialog isOpen={createOpen} onClose={() => setCreateOpen(false)} />

      <EditPermissionDialog
        permission={editTarget}
        onClose={() => setEditTarget(null)}
      />

      <DeactivateDialog
        permission={deactivateTarget}
        onClose={() => setDeactivateTarget(null)}
      />
    </div>
  )
}
