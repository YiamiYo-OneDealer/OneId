import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { UserDimensionsDto, AllDimensionValuesDto, SetUserDimensionsBody } from '@/api/types'

export function useUserDimensions(tenantId: string, userId: string) {
  return useQuery({
    queryKey: queryKeys.userDimensions(tenantId, userId),
    queryFn: () =>
      apiClient.get(`api/tenant/users/${userId}/dimensions`).json<UserDimensionsDto>(),
    enabled: !!(tenantId && userId),
  })
}

export function useSetUserDimensions(tenantId: string, userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: SetUserDimensionsBody) =>
      apiClient.put(`api/tenant/users/${userId}/dimensions`, { json: body }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.userDimensions(tenantId, userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
    },
  })
}

export function useDimensionValues(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.dimensionValues(tenantId),
    queryFn: () =>
      apiClient.get('api/tenant/dimensions').json<AllDimensionValuesDto>(),
    enabled: !!tenantId,
  })
}
