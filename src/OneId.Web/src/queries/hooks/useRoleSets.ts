import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { RoleSetDto, CreateRoleSetBody, UpdateRoleSetBody, PagedResponse } from '@/api/types'

export function useRoleSets(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.roleSets(tenantId),
    queryFn: () =>
      apiClient
        .get('api/tenant/role-sets', { searchParams: { pageSize: 500 } })
        .json<PagedResponse<RoleSetDto>>()
        .then(r => r.items),
    enabled: !!tenantId,
  })
}

export function useRoleSet(tenantId: string, roleSetId: string) {
  return useQuery({
    queryKey: queryKeys.roleSet(tenantId, roleSetId),
    queryFn: () =>
      apiClient.get(`api/tenant/role-sets/${roleSetId}`).json<RoleSetDto>(),
    enabled: !!(tenantId && roleSetId),
  })
}

export function useCreateRoleSet(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateRoleSetBody) =>
      apiClient.post('api/tenant/role-sets', { json: body }).json<RoleSetDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSets(tenantId) })
    },
  })
}

export function useUpdateRoleSet(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ roleSetId, patch }: { roleSetId: string; patch: UpdateRoleSetBody }) =>
      apiClient.put(`api/tenant/role-sets/${roleSetId}`, { json: patch }).json<RoleSetDto>(),
    onSuccess: (_data, { roleSetId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSets(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSet(tenantId, roleSetId) })
    },
  })
}

export function useDeleteRoleSet(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ roleSetId, version }: { roleSetId: string; version: number }) =>
      apiClient.delete(`api/tenant/role-sets/${roleSetId}`, { json: { version } }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSets(tenantId) })
    },
  })
}
