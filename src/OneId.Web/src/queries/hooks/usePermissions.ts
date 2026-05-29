import { useQuery, useMutation, useQueryClient, queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { PermissionDto, PagedResponse, CreatePermissionBody, UpdatePermissionBody } from '@/api/types'

export function usePermissions() {
  return useQuery({
    queryKey: queryKeys.permissions(),
    queryFn: () =>
      apiClient
        .get('api/internal/permissions', { searchParams: { pageSize: 200, status: 'All' } })
        .json<PagedResponse<PermissionDto>>()
        .then(r => r.items),
  })
}

export function useCreatePermission() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreatePermissionBody) =>
      apiClient.post('api/internal/permissions', { json: body }).json<PermissionDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.permissions() })
    },
  })
}

export function useUpdatePermission() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ permissionId, body }: { permissionId: string; body: UpdatePermissionBody }) =>
      apiClient.patch(`api/internal/permissions/${permissionId}`, { json: body }).json<PermissionDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.permissions() })
    },
  })
}

export function useDeactivatePermission() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (permissionId: string) =>
      apiClient.delete(`api/internal/permissions/${permissionId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.permissions() })
    },
  })
}

export function useActivatePermission() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (permissionId: string) =>
      apiClient.post(`api/internal/permissions/${permissionId}/activate`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.permissions() })
    },
  })
}

export const getCurrentUserPermissionsOptions = () =>
  queryOptions({
    queryKey: queryKeys.currentUserPermissions(),
    queryFn: () =>
      apiClient
        .get('api/account/permissions')
        .json<{ permissions: string[] }>()
        .then((r) => r.permissions),
  })

export function useCurrentUserPermissions() {
  return useQuery(getCurrentUserPermissionsOptions())
}
