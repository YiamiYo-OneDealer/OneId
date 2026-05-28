import * as React from 'react'
import { useQuery, queryOptions } from '@tanstack/react-query'
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
