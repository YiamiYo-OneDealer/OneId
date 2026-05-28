import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { RoleDto, CreateRoleBody, UpdateRoleBody, PagedResponse } from '@/api/types'

export function useRoles(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.roles(tenantId),
    queryFn: () =>
      apiClient
        .get('api/tenant/roles', { searchParams: { pageSize: 100 } })
        .json<PagedResponse<RoleDto>>()
        .then(r => r.items),
    enabled: !!tenantId,
  })
}

export function useRole(tenantId: string, roleId: string) {
  return useQuery({
    queryKey: queryKeys.role(tenantId, roleId),
    queryFn: () =>
      apiClient.get(`api/tenant/roles/${roleId}`).json<RoleDto>(),
    enabled: !!(tenantId && roleId),
  })
}

export function useCreateRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateRoleBody) =>
      apiClient.post('api/tenant/roles', { json: body }).json<RoleDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
    },
  })
}

export function useUpdateRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ roleId, patch }: { roleId: string; patch: UpdateRoleBody }) =>
      apiClient.put(`api/tenant/roles/${roleId}`, { json: patch }).json<RoleDto>(),
    onSuccess: (_data, { roleId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.role(tenantId, roleId) })
    },
  })
}

export function useDeleteRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (roleId: string) =>
      apiClient.delete(`api/tenant/roles/${roleId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
    },
  })
}
