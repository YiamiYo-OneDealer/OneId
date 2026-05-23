import { useParams } from 'react-router'
import { useTenantStore } from '@/store/tenant-store'

export function useActiveTenant() {
  const { tenantId } = useParams<{ tenantId: string }>()
  const activeTenantId = useTenantStore((s) => s.activeTenantId)

  return {
    tenantId: tenantId ?? null, // URL is authoritative
    cachedTenantId: activeTenantId, // Zustand cache (convenience only)
  }
}
