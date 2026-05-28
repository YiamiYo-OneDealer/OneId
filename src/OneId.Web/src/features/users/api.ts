import * as React from 'react'
import { useQuery, useMutation, useQueryClient, queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { UserOverrideDto, CreateOverrideBody } from '@/api/types'
import type { EffectivePermissionsResponse, PreviewPayload } from './schemas'
import { mockStore, mockDelay } from '@/mocks/store'

// Effective permissions require a confidential-client introspection call that the SPA cannot
// make directly. These hooks remain on mock data until a dedicated /api/account/permissions
// endpoint is added.
export const effectivePermissionsLiveOptions = (userId: string) =>
  queryOptions({
    queryKey: queryKeys.effectivePermissions(userId),
    queryFn: async (): Promise<EffectivePermissionsResponse> => {
      await mockDelay()
      return mockStore.getEffectivePermissions(userId)
    },
    enabled: Boolean(userId),
  })

export function useEffectivePermissionsLive(userId: string) {
  return useQuery(effectivePermissionsLiveOptions(userId))
}

export function useEffectivePermissionsPreview(
  userId: string,
  previewPayload: PreviewPayload | null,
) {
  const [data, setData] = React.useState<EffectivePermissionsResponse | null>(null)
  const [isLoading, setIsLoading] = React.useState(false)
  const abortRef = React.useRef<AbortController | null>(null)

  React.useEffect(() => {
    if (!previewPayload || !userId) return
    const timer = setTimeout(async () => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      setIsLoading(true)
      try {
        const result = await mockStore.getEffectivePermissionsPreview(userId, previewPayload)
        if (!controller.signal.aborted) setData(result)
      } finally {
        if (!controller.signal.aborted) setIsLoading(false)
      }
    }, 350)

    return () => {
      clearTimeout(timer)
      abortRef.current?.abort()
    }
  }, [userId, previewPayload])

  return { data, isLoading }
}

export function useUserOverrides(tenantId: string, userId: string) {
  return useQuery({
    queryKey: queryKeys.userOverrides(tenantId, userId),
    queryFn: () =>
      apiClient
        .get(`api/tenant/users/${userId}/overrides`)
        .json<UserOverrideDto[]>(),
    enabled: !!(tenantId && userId),
  })
}

export function useCreateOverride(tenantId: string, userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateOverrideBody) =>
      apiClient
        .post(`api/tenant/users/${userId}/overrides`, { json: body })
        .json<UserOverrideDto>(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.userOverrides(tenantId, userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
    },
  })
}

export function useDeleteOverride(tenantId: string, userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (overrideId: string) =>
      apiClient.delete(`api/tenant/users/${userId}/overrides/${overrideId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.userOverrides(tenantId, userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
    },
  })
}

export function useRevokeUserTokens(userId: string) {
  return useMutation({
    mutationFn: () =>
      apiClient.post(`api/tenant/users/${userId}/revoke-tokens`),
  })
}
