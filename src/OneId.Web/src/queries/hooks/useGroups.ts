import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { GroupDto, CreateGroupBody, UpdateGroupBody, PagedResponse } from '@/api/types'

export function useGroups(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.groups(tenantId),
    queryFn: () =>
      apiClient
        .get('api/tenant/groups', { searchParams: { pageSize: 100 } })
        .json<PagedResponse<GroupDto>>()
        .then(r => r.items),
    enabled: !!tenantId,
  })
}

export function useGroup(tenantId: string, groupId: string) {
  return useQuery({
    queryKey: queryKeys.group(tenantId, groupId),
    queryFn: () =>
      apiClient.get(`api/tenant/groups/${groupId}`).json<GroupDto>(),
    enabled: !!(tenantId && groupId),
  })
}

export function useCreateGroup(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateGroupBody) =>
      apiClient.post('api/tenant/groups', { json: body }).json<GroupDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.groups(tenantId) })
    },
  })
}

export function useUpdateGroup(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ groupId, patch }: { groupId: string; patch: UpdateGroupBody }) =>
      apiClient.put(`api/tenant/groups/${groupId}`, { json: patch }).json<GroupDto>(),
    onSuccess: (_data, { groupId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.groups(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.group(tenantId, groupId) })
    },
  })
}

export function useDeleteGroup(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (groupId: string) =>
      apiClient.delete(`api/tenant/groups/${groupId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.groups(tenantId) })
    },
  })
}
