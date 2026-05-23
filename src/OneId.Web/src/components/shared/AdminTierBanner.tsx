import { Link, useBlocker, useMatches } from 'react-router'
import { useTenantStore } from '@/store/tenant-store'
import { useUiStore } from '@/store/ui-store'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { ChevronLeft } from 'lucide-react'

function useCurrentSection(): string {
  const matches = useMatches()
  const last = matches[matches.length - 1]
  if (!last) return ''
  const segments = last.pathname.split('/').filter(Boolean)
  const tenantIdx = segments.findIndex((s) => s === 'tenants')
  const afterTenant = tenantIdx >= 0 ? segments.slice(tenantIdx + 2) : []
  if (afterTenant.length === 0) return 'Dashboard'
  return afterTenant[afterTenant.length - 1]
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}

export function AdminTierBanner() {
  const activeTenantId = useTenantStore((s) => s.activeTenantId)
  const isFormDirty = useUiStore((s) => s.isFormDirty)
  const currentSection = useCurrentSection()

  const blocker = useBlocker(({ nextLocation }) => {
    return isFormDirty && !nextLocation.pathname.includes(activeTenantId ?? '__never__')
  })

  if (!activeTenantId) return null

  return (
    <>
      <div
        aria-live="polite"
        className="flex h-10 w-full shrink-0 items-center justify-between bg-admin-banner-bg px-4 text-sm"
      >
        <span className="font-medium text-on-admin-banner">
          Internal Admin — Tenant: {activeTenantId} / {currentSection}
        </span>
        <Link
          to="/internal"
          className="flex items-center gap-1 text-on-admin-banner underline-offset-2 hover:underline"
        >
          <ChevronLeft size={14} />
          All Tenants
        </Link>
      </div>

      <Dialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.()
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>You have unsaved changes</DialogTitle>
            <DialogDescription>
              Leaving this page will discard your unsaved changes. Leave anyway?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => blocker.reset?.()}>
              Stay
            </Button>
            <Button variant="destructive" onClick={() => blocker.proceed?.()}>
              Leave anyway
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
