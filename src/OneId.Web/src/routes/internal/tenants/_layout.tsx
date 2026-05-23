import { Outlet, useParams, NavLink } from 'react-router'
import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTenantStore } from '@/store/tenant-store'
import { useUiStore } from '@/store/ui-store'
import { queryKeys } from '@/queries/keys'
import { cn } from '@/lib/utils'

const SUB_NAV_TABS = [
  { label: 'Overview', path: '', end: true },
  { label: 'Users', path: '/users', end: false },
  { label: 'Groups', path: '/groups', end: false },
  { label: 'Role Sets', path: '/role-sets', end: false },
  { label: 'Roles', path: '/roles', end: false },
]

export function TenantContextLayout() {
  const { tenantId } = useParams<{ tenantId: string }>()
  const queryClient = useQueryClient()
  const previousTenantId = useRef<string | undefined>(undefined)
  const setActiveTenant = useTenantStore((s) => s.setActiveTenantId)
  const clearTenant = useTenantStore((s) => s.clearTenant)
  const setFormDirty = useUiStore((s) => s.setFormDirty)

  useEffect(() => {
    if (tenantId && tenantId !== previousTenantId.current) {
      if (previousTenantId.current) {
        queryClient.invalidateQueries({ queryKey: queryKeys.tenant(previousTenantId.current) })
      }
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
      setActiveTenant(tenantId)
      previousTenantId.current = tenantId
    }
  }, [tenantId, queryClient, setActiveTenant])

  useEffect(() => {
    return () => {
      clearTenant()
      setFormDirty(false)
    }
  }, [clearTenant, setFormDirty])

  if (!tenantId) return null

  const base = `/internal/tenants/${tenantId}`

  return (
    <div className="flex flex-col gap-0">
      <nav
        aria-label="Tenant sections"
        className="flex gap-1 border-b border-border px-1 pb-0"
      >
        {SUB_NAV_TABS.map((tab) => (
          <NavLink
            key={tab.label}
            to={`${base}${tab.path}`}
            end={tab.end}
            className={({ isActive }) =>
              cn(
                'px-4 py-2 text-sm transition-colors border-b-2 -mb-px',
                isActive
                  ? 'border-primary text-foreground font-medium'
                  : 'border-transparent text-muted-foreground hover:text-foreground',
              )
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>
      <div className="pt-4">
        <Outlet />
      </div>
    </div>
  )
}
