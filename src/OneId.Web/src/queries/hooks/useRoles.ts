import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { Role } from '@/mocks/types'

export function useRoles(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.roles(tenantId),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getRoles(tenantId)
    },
    enabled: !!tenantId,
  })
}

export function useRole(tenantId: string, roleId: string) {
  return useQuery({
    queryKey: queryKeys.role(tenantId, roleId),
    queryFn: async () => {
      await mockDelay()
      const role = mockStore.getRole(tenantId, roleId)
      if (!role) throw new Error(`Role ${roleId} not found`)
      return role
    },
    enabled: !!(tenantId && roleId),
  })
}

export function useCreateRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: Omit<Role, 'id' | 'tenantId'>) => {
      await mockDelay(200)
      return mockStore.createRole({ ...data, tenantId })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
    },
  })
}

export function useUpdateRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ roleId, patch }: { roleId: string; patch: Partial<Role> }) => {
      await mockDelay(200)
      return mockStore.updateRole(tenantId, roleId, patch)
    },
    onSuccess: (_data, { roleId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.role(tenantId, roleId) })
    },
  })
}

export function useDeleteRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (roleId: string) => {
      await mockDelay(200)
      mockStore.deleteRole(tenantId, roleId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
    },
  })
}
