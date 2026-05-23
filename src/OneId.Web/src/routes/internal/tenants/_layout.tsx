import { Outlet, useParams } from 'react-router'
import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTenantStore } from '@/store/tenant-store'

export function TenantContextLayout() {
  const { tenantId } = useParams<{ tenantId: string }>()
  const queryClient = useQueryClient()
  const previousTenantId = useRef<string | undefined>(undefined)
  const setActiveTenant = useTenantStore((s) => s.setActiveTenantId)
  const clearTenant = useTenantStore((s) => s.clearTenant)

  useEffect(() => {
    if (tenantId && tenantId !== previousTenantId.current) {
      if (previousTenantId.current) {
        queryClient.invalidateQueries({
          queryKey: ['tenants', previousTenantId.current],
        })
      }
      setActiveTenant(tenantId)
      previousTenantId.current = tenantId
    }
  }, [tenantId, queryClient, setActiveTenant])

  useEffect(() => {
    return () => {
      clearTenant()
    }
  }, [clearTenant])

  if (!tenantId) return null
  return <Outlet />
}
