import * as React from 'react'
import { useQuery, useMutation, useQueryClient, queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { EffectivePermissionsResponse, PreviewPayload } from './schemas'

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

    return () => clearTimeout(timer)
  }, [userId, previewPayload])

  return { data, isLoading }
}

export function useUserOverrides(tenantId: string, userId: string) {
  return useQuery({
    queryKey: queryKeys.userOverrides(tenantId, userId),
    queryFn: async () => {
      await mockDelay(200)
      return mockStore.getDenyOverridesForUser(userId)
    },
    enabled: !!(tenantId && userId),
  })
}

export function useDeleteOverride(tenantId: string, userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (overrideId: string) => {
      await mockDelay(200)
      mockStore.deleteOverride(userId, overrideId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.effectivePermissions(userId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.userOverrides(tenantId, userId) })
    },
  })
}

export function useRevokeUserTokens(userId: string) {
  return useMutation({
    mutationFn: async () => {
      await mockDelay(200)
      mockStore.revokeUserTokens(userId)
    },
  })
}
