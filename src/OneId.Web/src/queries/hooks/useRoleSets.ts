import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { RoleSet } from '@/mocks/types'

export function useRoleSets(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.roleSets(tenantId),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getRoleSets(tenantId)
    },
    enabled: !!tenantId,
  })
}

export function useRoleSet(tenantId: string, roleSetId: string) {
  return useQuery({
    queryKey: queryKeys.roleSet(tenantId, roleSetId),
    queryFn: async () => {
      await mockDelay()
      const roleSet = mockStore.getRoleSet(tenantId, roleSetId)
      if (!roleSet) throw new Error(`RoleSet ${roleSetId} not found`)
      return roleSet
    },
    enabled: !!(tenantId && roleSetId),
  })
}

export function useCreateRoleSet(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: Omit<RoleSet, 'id' | 'tenantId'>) => {
      await mockDelay(200)
      return mockStore.createRoleSet({ ...data, tenantId })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSets(tenantId) })
    },
  })
}

export function useUpdateRoleSet(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ roleSetId, patch }: { roleSetId: string; patch: Partial<RoleSet> }) => {
      await mockDelay(200)
      return mockStore.updateRoleSet(tenantId, roleSetId, patch)
    },
    onSuccess: (_data, { roleSetId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSets(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSet(tenantId, roleSetId) })
    },
  })
}

export function useDeleteRoleSet(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (roleSetId: string) => {
      await mockDelay(200)
      mockStore.deleteRoleSet(tenantId, roleSetId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roleSets(tenantId) })
    },
  })
}
