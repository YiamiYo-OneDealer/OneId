import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { Tenant } from '@/mocks/types'

export function useTenants() {
  return useQuery({
    queryKey: queryKeys.tenants(),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getTenants()
    },
  })
}

export function useTenant(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.tenant(tenantId),
    queryFn: async () => {
      await mockDelay()
      const tenant = mockStore.getTenant(tenantId)
      if (!tenant) throw new Error(`Tenant ${tenantId} not found`)
      return tenant
    },
    enabled: !!tenantId,
  })
}

export function useCreateTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: Omit<Tenant, 'id' | 'createdAt'>) => {
      await mockDelay(200)
      return mockStore.createTenant(data)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants() })
    },
  })
}

export function useUpdateTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ tenantId, patch }: { tenantId: string; patch: Partial<Tenant> }) => {
      await mockDelay(200)
      return mockStore.updateTenant(tenantId, patch)
    },
    onSuccess: (_data, { tenantId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants() })
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
    },
  })
}
