import * as React from 'react'
import { useNavigate } from 'react-router'
import { useIsFetching } from '@tanstack/react-query'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { EmptyState } from '@/components/shared/EmptyState'
import { DenyOverrideBadge } from '@/components/shared/DenyOverrideBadge'
import { ProvenanceChain } from '@/components/shared/ProvenanceChain'
import { useEffectivePermissionsLive, useEffectivePermissionsPreview, useUserOverrides } from '@/features/users/api'
import { DenyOverrideSheet } from '@/components/shared/DenyOverrideSheet'
import type { DenyOverride } from '@/features/users/schemas'
import { getPermissionLabel } from '@/permissions/registry'
import { queryKeys } from '@/queries/keys'
import { cn } from '@/lib/utils'
import { Users, ShieldAlert } from 'lucide-react'
import { useTenantStore } from '@/store/tenant-store'
import { Alert, AlertDescription } from '@/components/ui/alert'
import type { PreviewPayload } from '@/features/users/schemas'

type EffectivePermissionsPanelProps =
  | { mode: 'live'; userId: string }
  | { mode: 'preview'; userId: string; previewPayload: PreviewPayload }

function formatDistanceToNow(isoDate: string): string {
  const diffMs = Math.max(0, Date.now() - new Date(isoDate).getTime())
  const diffMin = Math.floor(diffMs / 60_000)
  if (diffMin < 1) return 'just now'
  if (diffMin === 1) return '1m ago'
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr === 1) return '1h ago'
  return `${diffHr}h ago`
}

function SkeletonRows() {
  return (
    <div className="flex flex-col gap-3 py-2">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="flex items-center gap-3">
          <Skeleton className="h-4 w-1/3" />
          <Skeleton className="h-4 w-1/4" />
        </div>
      ))}
    </div>
  )
}

export function EffectivePermissionsPanel(props: EffectivePermissionsPanelProps) {
  if (props.mode === 'preview') {
    return <PreviewPanel userId={props.userId} previewPayload={props.previewPayload} />
  }

  return <LivePanel userId={props.userId} />
}

function PreviewPanel({ userId, previewPayload }: { userId: string; previewPayload: PreviewPayload }) {
  const { data, isLoading } = useEffectivePermissionsPreview(userId, previewPayload)
  const [search, setSearch] = React.useState('')

  const showNoPermissionsWarning =
    !previewPayload.groupIds?.length ||
    (data !== null && data.permissions.length === 0)

  const filteredPermissions = React.useMemo(() => {
    if (!data?.permissions) return []
    const q = search.trim().toLowerCase()
    if (!q) return data.permissions
    return data.permissions.filter(
      (p) =>
        p.id.toLowerCase().includes(q) ||
        getPermissionLabel(p.id).toLowerCase().includes(q),
    )
  }, [data?.permissions, search])

  if (isLoading) {
    return (
      <div aria-busy="true" aria-label="Loading preview">
        <SkeletonRows />
      </div>
    )
  }

  if (showNoPermissionsWarning) {
    return (
      <Alert variant="default" className="border-amber-500 bg-amber-950/20 text-amber-400">
        <AlertDescription>This user will have no permissions.</AlertDescription>
      </Alert>
    )
  }

  return (
    <div className="flex flex-col gap-3">
      <input
        type="search"
        placeholder="Search permissions…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="h-8 w-full rounded-md border border-input bg-background px-3 py-1 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        aria-label="Search permissions"
      />

      <Tabs defaultValue="capabilities">
        <TabsList>
          <TabsTrigger value="capabilities">Capabilities</TabsTrigger>
          <TabsTrigger value="details">Permission Details</TabsTrigger>
        </TabsList>

        <TabsContent value="capabilities">
          <div className="flex flex-col divide-y divide-border">
            {filteredPermissions.length === 0 && search && (
              <EmptyState variant="no-results" />
            )}
            {filteredPermissions.map((perm) => (
              <div
                key={perm.id}
                className={cn(
                  'flex items-start justify-between gap-2 py-2.5',
                  perm.diffStatus === 'added' && 'border-l-2 border-green-500 pl-2',
                  perm.diffStatus === 'removed' && 'opacity-60',
                )}
              >
                <div className="flex flex-col gap-1 min-w-0">
                  <div className="flex items-center gap-1.5 flex-wrap">
                    <TooltipProvider>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <span
                            className={cn(
                              'text-sm font-medium cursor-default',
                              perm.diffStatus === 'removed' && 'line-through text-red-400',
                              perm.diffStatus === 'added' && 'text-green-400',
                              perm.diffStatus !== 'removed' && perm.diffStatus !== 'added' && 'text-foreground',
                            )}
                          >
                            {getPermissionLabel(perm.id)}
                          </span>
                        </TooltipTrigger>
                        <TooltipContent>
                          <code className="font-mono text-xs">{perm.id}</code>
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                    {perm.isDenied && (
                      <DenyOverrideBadge permissionLabel={getPermissionLabel(perm.id)} />
                    )}
                  </div>
                  <ProvenanceChain chain={perm.provenanceChain} collapsed />
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="details">
          <div className="flex flex-col divide-y divide-border">
            {filteredPermissions.length === 0 && search && (
              <EmptyState variant="no-results" />
            )}
            {filteredPermissions.map((perm) => (
              <div key={perm.id} className="flex flex-col gap-1.5 py-2.5">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <code
                    className={cn(
                      'font-mono text-[13px]',
                      perm.diffStatus === 'removed' ? 'line-through text-red-400' : 'text-indigo-300',
                    )}
                  >
                    {perm.id}
                  </code>
                  {perm.isDenied && (
                    <DenyOverrideBadge permissionLabel={getPermissionLabel(perm.id)} />
                  )}
                </div>
                <ProvenanceChain chain={perm.provenanceChain} collapsed={false} />
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}

function LivePanel({ userId }: { userId: string }) {
  const navigate = useNavigate()
  const tenantId = useTenantStore((s) => s.activeTenantId) ?? ''
  const { data, isLoading, isFetching } = useEffectivePermissionsLive(userId)
  const { data: overrides } = useUserOverrides(tenantId, userId)
  const fetchingCount = useIsFetching({ queryKey: queryKeys.effectivePermissions(userId) })
  const isBackgroundFetching = fetchingCount > 0 && !isLoading

  const [search, setSearch] = React.useState('')
  const [announcement, setAnnouncement] = React.useState('')
  const [selectedOverride, setSelectedOverride] = React.useState<DenyOverride | null>(null)

  // Debounced aria-live announcement after fetch settles
  React.useEffect(() => {
    if (!isFetching && data) {
      const timer = setTimeout(() => {
        setAnnouncement(`Permissions loaded. Last resolved ${formatDistanceToNow(data.resolvedAt)}.`)
      }, 400)
      return () => clearTimeout(timer)
    }
  }, [isFetching, data])

  const filteredPermissions = React.useMemo(() => {
    if (!data?.permissions) return []
    const q = search.trim().toLowerCase()
    if (!q) return data.permissions
    return data.permissions.filter(
      (p) =>
        p.id.toLowerCase().includes(q) ||
        getPermissionLabel(p.id).toLowerCase().includes(q),
    )
  }, [data?.permissions, search])

  const dimming = isBackgroundFetching

  if (isLoading) {
    return (
      <div aria-busy="true" aria-label="Loading permissions">
        <SkeletonRows />
      </div>
    )
  }

  // Determine empty state
  if (!data || (!data.hasGroupAssignments && data.permissions.length === 0)) {
    return (
      <EmptyState
        variant="no-data"
        icon={Users}
        title="No group assignments"
        description="This user has no group memberships. Add them to a group to grant permissions."
        action={{
          label: 'Manage Groups',
          onClick: () => navigate(tenantId ? `/tenant/groups` : '/tenant/groups'),
        }}
      />
    )
  }

  if (data.hasGroupAssignments && data.permissions.length === 0) {
    return (
      <EmptyState
        variant="empty"
        icon={Users}
        title="No permissions in groups"
        description="This user's groups have no roles assigned. Add roles to the groups to grant permissions."
        action={{
          label: 'Manage Groups',
          onClick: () => navigate('/tenant/groups'),
        }}
      />
    )
  }

  if (data.permissions.length > 0 && data.permissions.every((p) => p.isDenied)) {
    return (
      <EmptyState
        variant="empty"
        icon={ShieldAlert}
        title="All permissions DENY-overridden"
        description="Every permission for this user has a DENY override applied. Review and remove overrides to restore access."
        action={{
          label: 'Review Overrides',
          onClick: () => navigate(`/tenant/users/${userId}`),
        }}
      />
    )
  }

  return (
    <div
      className={cn(
        'flex flex-col gap-3 transition-opacity duration-300',
        dimming && 'opacity-60',
      )}
    >
      {/* Propagation dimming timestamp */}
      {dimming && data && (
        <p className="text-xs text-muted-foreground">
          Last resolved {formatDistanceToNow(data.resolvedAt)}
        </p>
      )}

      {/* aria-live region */}
      <div
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {announcement}
      </div>

      {/* Search */}
      <input
        type="search"
        placeholder="Search permissions…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="h-8 w-full rounded-md border border-input bg-background px-3 py-1 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        aria-label="Search permissions"
      />

      <Tabs defaultValue="capabilities">
        <TabsList>
          <TabsTrigger value="capabilities">Capabilities</TabsTrigger>
          <TabsTrigger value="details">Permission Details</TabsTrigger>
        </TabsList>

        <TabsContent value="capabilities">
          <div
            aria-busy={isFetching}
            className="flex flex-col divide-y divide-border"
          >
            {filteredPermissions.length === 0 && search && (
              <EmptyState variant="no-results" />
            )}
            {filteredPermissions.map((perm) => (
              <div key={perm.id} className="flex items-start justify-between gap-2 py-2.5">
                <div className="flex flex-col gap-1 min-w-0">
                  <div className="flex items-center gap-1.5 flex-wrap">
                    <TooltipProvider>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <span className="text-sm font-medium text-foreground cursor-default">
                            {getPermissionLabel(perm.id)}
                          </span>
                        </TooltipTrigger>
                        <TooltipContent>
                          <code className="font-mono text-xs">{perm.id}</code>
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                    {perm.isDenied && (
                      <DenyOverrideBadge
                        permissionLabel={getPermissionLabel(perm.id)}
                        onReview={() => {
                          const ov = overrides?.find((o) => o.permissionId === perm.id)
                          if (ov) setSelectedOverride(ov)
                        }}
                      />
                    )}
                  </div>
                  <ProvenanceChain chain={perm.provenanceChain} collapsed />
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="details">
          <div className="flex flex-col divide-y divide-border">
            {filteredPermissions.length === 0 && search && (
              <EmptyState variant="no-results" />
            )}
            {filteredPermissions.map((perm) => (
              <div key={perm.id} className="flex flex-col gap-1.5 py-2.5">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <code className="font-mono text-[13px] text-indigo-300">{perm.id}</code>
                  {perm.isDenied && (
                    <DenyOverrideBadge
                      permissionLabel={getPermissionLabel(perm.id)}
                      onReview={() => {
                        const ov = overrides?.find((o) => o.permissionId === perm.id) ?? null
                        setSelectedOverride(ov)
                      }}
                    />
                  )}
                </div>
                <ProvenanceChain chain={perm.provenanceChain} collapsed={false} />
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>

      <DenyOverrideSheet
        userId={userId}
        tenantId={tenantId}
        override={selectedOverride}
        onClose={() => setSelectedOverride(null)}
      />
    </div>
  )
}
