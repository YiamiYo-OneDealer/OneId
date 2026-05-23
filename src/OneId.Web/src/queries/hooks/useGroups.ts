import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { Group } from '@/mocks/types'

export function useGroups(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.groups(tenantId),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getGroups(tenantId)
    },
    enabled: !!tenantId,
  })
}

export function useGroup(tenantId: string, groupId: string) {
  return useQuery({
    queryKey: queryKeys.group(tenantId, groupId),
    queryFn: async () => {
      await mockDelay()
      const group = mockStore.getGroup(tenantId, groupId)
      if (!group) throw new Error(`Group ${groupId} not found`)
      return group
    },
    enabled: !!(tenantId && groupId),
  })
}

export function useCreateGroup(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: Omit<Group, 'id'>) => {
      await mockDelay(200)
      return mockStore.createGroup(data)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.groups(tenantId) })
    },
  })
}

export function useUpdateGroup(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ groupId, patch }: { groupId: string; patch: Partial<Group> }) => {
      await mockDelay(200)
      return mockStore.updateGroup(tenantId, groupId, patch)
    },
    onSuccess: (_data, { groupId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.groups(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.group(tenantId, groupId) })
    },
  })
}
