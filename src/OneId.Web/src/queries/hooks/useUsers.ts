import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { UserDto, CreateUserBody, UpdateUserBody, PagedResponse } from '@/api/types'

export function useUsers(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.users(tenantId),
    queryFn: () =>
      apiClient
        .get('api/tenant/users', { searchParams: { pageSize: 100 } })
        .json<PagedResponse<UserDto>>()
        .then(r => r.items),
    enabled: !!tenantId,
  })
}

export function useUser(tenantId: string, userId: string) {
  return useQuery({
    queryKey: queryKeys.user(tenantId, userId),
    queryFn: () =>
      apiClient.get(`api/tenant/users/${userId}`).json<UserDto>(),
    enabled: !!(tenantId && userId),
  })
}

export function useCreateUser(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateUserBody) =>
      apiClient.post('api/tenant/users', { json: body }).json<UserDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.tenant(tenantId) })
    },
  })
}

export function useUpdateUser(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, patch }: { userId: string; patch: UpdateUserBody }) =>
      apiClient.patch(`api/tenant/users/${userId}`, { json: patch }).json<UserDto>(),
    onSuccess: (_data, { userId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.user(tenantId, userId) })
    },
  })
}

export function useDeleteUser(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) =>
      apiClient.delete(`api/tenant/users/${userId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users(tenantId) })
    },
  })
}
