import { Outlet, useParams } from 'react-router'
import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTenantStore } from '@/store/tenant-store'
import { useUiStore } from '@/store/ui-store'
import { queryKeys } from '@/queries/keys'

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
  return <Outlet />
}
