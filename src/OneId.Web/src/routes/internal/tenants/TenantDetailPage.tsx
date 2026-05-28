import { useState } from 'react'
import { useParams } from 'react-router'
import { useTenant, useUpdateTenant } from '@/queries/hooks'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

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
  currentStatus: 'Active' | 'Suspended'
  isPending: boolean
}) {
  const isSuspending = currentStatus === 'Active'
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
        patch: { status: tenant.status === 'Active' ? 'Suspended' : 'Active' },
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
          <Badge variant={tenant.status === 'Active' ? 'default' : 'destructive'}>
            {tenant.status}
          </Badge>
        </div>
        <Button
          variant={tenant.status === 'Active' ? 'destructive' : 'default'}
          size="sm"
          disabled={updateTenant.isPending}
          onClick={() => setSuspendDialogOpen(true)}
        >
          {tenant.status === 'Active' ? 'Suspend' : 'Reinstate'}
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
            <Badge variant={tenant.status === 'Active' ? 'default' : 'destructive'}>
              {tenant.status}
            </Badge>
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
