import { useQuery, queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { EffectivePermissionsResponse } from './schemas'

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
