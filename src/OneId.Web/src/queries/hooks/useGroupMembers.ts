import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { GroupDto, PagedResponse } from '@/api/types'

export function useUserGroups(tenantId: string, userId: string) {
  return useQuery({
    queryKey: queryKeys.userGroups(tenantId, userId),
    queryFn: () =>
      apiClient
        .get(`api/tenant/users/${userId}/groups`)
        .json<{ items: GroupDto[] }>()
        .then(r => r.items),
    enabled: !!(tenantId && userId),
  })
}

export function useAddGroupMember(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ groupId, userId }: { groupId: string; userId: string }) =>
      apiClient.put(`api/tenant/groups/${groupId}/members`, { json: { userId } }),
    onSuccess: (_data, { userId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.userGroups(tenantId, userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
    },
  })
}

export function useRemoveGroupMember(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ groupId, userId }: { groupId: string; userId: string }) =>
      apiClient.delete(`api/tenant/groups/${groupId}/members/${userId}`),
    onSuccess: (_data, { userId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.userGroups(tenantId, userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
    },
  })
}
