import * as React from 'react'
import { useQueryClient } from '@tanstack/react-query'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { useHasPermission } from '@/hooks/useHasPermission'
import { useDeleteOverride, useRevokeUserTokens } from '@/features/users/api'
import { useFormMutation } from '@/hooks/useFormMutation'
import { queryKeys } from '@/queries/keys'
import type { DenyOverride } from '@/features/users/schemas'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function DenyOverrideSheet({
  userId,
  tenantId,
  override,
  onClose,
}: {
  userId: string
  tenantId: string
  override: DenyOverride | null
  onClose: () => void
}) {
  const [confirmOpen, setConfirmOpen] = React.useState(false)
  const { permitted, isLoading: permLoading } = useHasPermission('od.admin.users.revoke')
  const queryClient = useQueryClient()

  const deleteOverrideMutation = useDeleteOverride(tenantId, userId)
  const revokeTokensMutation = useRevokeUserTokens(userId)

  const removeOverride = useFormMutation({
    mutationFn: async () => {
      if (!override) return
      await deleteOverrideMutation.mutateAsync(override.id)
    },
    messages: {
      success: 'Override removed.',
      error: 'Failed to remove override.',
      propagationNote: true,
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.userOverrides(tenantId, userId) })
      setConfirmOpen(false)
      onClose()
    },
  })

  const forceReauth = useFormMutation({
    mutationFn: async () => {
      await revokeTokensMutation.mutateAsync()
    },
    messages: {
      success: '',
      error: 'Revocation failed.',
      forceRevoke: true,
    },
    onSuccess: onClose,
  })

  return (
    <>
      <Sheet open={!!override} onOpenChange={(open) => { if (!open) onClose() }}>
        <SheetContent side="right" className="w-[480px] overflow-y-auto">
          <SheetHeader>
            <SheetTitle>DENY Override</SheetTitle>
            <SheetDescription>Review and manage this permission override</SheetDescription>
          </SheetHeader>

          {override && (
            <div className="mt-4 space-y-4 text-sm">
              <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
                <dt className="text-muted-foreground font-medium">Type</dt>
                <dd>
                  <span className="inline-flex items-center rounded px-1.5 py-0.5 text-[13px] font-semibold bg-red-950 text-red-500">
                    DENY
                  </span>
                </dd>

                <dt className="text-muted-foreground font-medium">Permission</dt>
                <dd className="font-mono text-xs text-foreground">{override.permissionId}</dd>

                <dt className="text-muted-foreground font-medium">Reason</dt>
                <dd className="text-foreground">{override.reason ?? 'No reason provided'}</dd>

                <dt className="text-muted-foreground font-medium">Applied by</dt>
                <dd className="text-foreground">{override.appliedByName}</dd>

                <dt className="text-muted-foreground font-medium">Applied at</dt>
                <dd className="text-foreground">{formatDate(override.appliedAt)}</dd>

                {override.expiresAt && (
                  <>
                    <dt className="text-muted-foreground font-medium">Expires</dt>
                    <dd className="text-foreground">{formatDate(override.expiresAt)}</dd>
                  </>
                )}
              </dl>

              <div className="flex flex-col gap-2 pt-2 border-t border-border">
                {permitted && (
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={forceReauth.isPending}
                    onClick={() => forceReauth.mutate()}
                  >
                    {forceReauth.isPending ? 'Revoking…' : 'Force Re-authenticate'}
                  </Button>
                )}
                {!permitted && permLoading && (
                  <Button variant="outline" size="sm" disabled>
                    Force Re-authenticate
                  </Button>
                )}

                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => setConfirmOpen(true)}
                >
                  Remove Override
                </Button>
              </div>
            </div>
          )}
        </SheetContent>
      </Sheet>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove this DENY override?</DialogTitle>
            <DialogDescription>
              This will remove the override. The user's permissions will be recalculated. Changes take effect within 5 minutes.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={removeOverride.isPending}
              onClick={() => removeOverride.mutate()}
            >
              {removeOverride.isPending ? 'Removing…' : 'Remove Override'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
