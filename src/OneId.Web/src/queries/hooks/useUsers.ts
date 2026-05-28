import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { User } from '@/mocks/types'

export function useUsers(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.users(tenantId),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getUsers(tenantId)
    },
    enabled: !!tenantId,
  })
}

export function useUser(tenantId: string, userId: string) {
  return useQuery({
    queryKey: queryKeys.user(tenantId, userId),
    queryFn: async () => {
      await mockDelay()
      const user = mockStore.getUser(tenantId, userId)
      if (!user) throw new Error(`User ${userId} not found`)
      return user
    },
    enabled: !!(tenantId && userId),
  })
}

export function useCreateUser(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: Omit<User, 'id' | 'createdAt'>) => {
      await mockDelay(200)
      return mockStore.createUser(data)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
    },
  })
}

export function useUpdateUser(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, patch }: { userId: string; patch: Partial<User> }) => {
      await mockDelay(200)
      return mockStore.updateUser(tenantId, userId, patch)
    },
    onSuccess: (_data, { userId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.user(tenantId, userId) })
    },
  })
}
