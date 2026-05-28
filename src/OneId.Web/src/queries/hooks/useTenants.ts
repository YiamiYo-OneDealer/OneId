import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { TenantDto, CreateTenantBody, UpdateTenantBody } from '@/api/types'

export function useTenants() {
  return useQuery({
    queryKey: queryKeys.tenants(),
    queryFn: () =>
      apiClient.get('api/internal/tenants').json<TenantDto[]>(),
  })
}

export function useTenant(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.tenant(tenantId),
    queryFn: () =>
      apiClient.get(`api/internal/tenants/${tenantId}`).json<TenantDto>(),
    enabled: !!tenantId,
  })
}

export function useCreateTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateTenantBody) =>
      apiClient.post('api/internal/tenants', { json: body }).json<TenantDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants() })
    },
  })
}

export function useUpdateTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ tenantId, patch }: { tenantId: string; patch: UpdateTenantBody }) =>
      apiClient.patch(`api/internal/tenants/${tenantId}`, { json: patch }).json<TenantDto>(),
    onSuccess: (_data, { tenantId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants() })
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
    },
  })
}
