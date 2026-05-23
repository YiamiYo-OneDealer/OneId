import { useState, useEffect } from 'react'
import { useParams } from 'react-router'
import { useTenant, useUsers, useUpdateTenant } from '@/queries/hooks'
import { mockStore } from '@/mocks/store'
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

// ── Suspension Dialog ─────────────────────────────────────────────────────────

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

function LicenseSection({
  tenantId,
  currentMax,
  currentUsed,
}: {
  tenantId: string
  currentMax: number | null
  currentUsed: number
}) {
  // null = not editing; derive from prop so field auto-syncs after save+refetch
  const [editedMax, setEditedMax] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const updateTenant = useUpdateTenant()

  const maxSeats = editedMax ?? (currentMax === null ? '' : String(currentMax))

  const handleSave = () => {
    const parsed = maxSeats.trim() === '' ? null : parseInt(maxSeats, 10)
    if (parsed !== null && (isNaN(parsed) || parsed < 1)) return
    updateTenant.mutate(
      { tenantId, patch: { seatUsage: { used: currentUsed, max: parsed } } },
      {
        onSuccess: () => {
          setEditedMax(null)
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
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEditedMax(e.target.value)}
            className="w-40"
            placeholder="Unlimited"
          />
        </div>
        <Button size="sm" onClick={handleSave} disabled={updateTenant.isPending}>
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
  const [admins, setAdmins] = useState<User[]>([])
  const [searchQuery, setSearchQuery] = useState('')
  const [showAddPanel, setShowAddPanel] = useState(false)

  useEffect(() => {
    if (users.length === 0) return
    const groups = mockStore.getGroups(tenantId)
    const adminGroup = groups.find((g) => g.name === 'Administrators')
    if (adminGroup) {
      setAdmins(users.filter((u) => u.groupIds.includes(adminGroup.id)))
    }
  }, [users, tenantId])

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
        <div className="rounded-md border border-border bg-background p-3 space-y-2">
          <Input
            placeholder="Search users…"
            value={searchQuery}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
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
                    className="w-full rounded px-2 py-1 text-left text-sm text-foreground hover:bg-card"
                  >
                    {u.name}{' '}
                    <span className="text-muted-foreground">— {u.email}</span>
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
                  <TooltipContent>
                    A tenant must have at least one administrator.
                  </TooltipContent>
                )}
              </Tooltip>
            </TooltipProvider>
          </li>
        ))}
        {admins.length === 0 && (
          <li className="text-sm text-muted-foreground">No administrators assigned.</li>
        )}
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
    <div className="max-w-2xl space-y-6">
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
          disabled={updateTenant.isPending}
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
          <dt className="text-muted-foreground">Status</dt>
          <dd>
            <Badge variant={tenant.status === 'active' ? 'default' : 'destructive'}>
              {tenant.status === 'active' ? 'Active' : 'Suspended'}
            </Badge>
          </dd>
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
      <LicenseSection
        tenantId={tenantId}
        currentMax={tenant.seatUsage.max}
        currentUsed={tenant.seatUsage.used}
      />

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
