import { useQuery, queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'

export function usePermissions() {
  return useQuery({
    queryKey: queryKeys.permissions(),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getPermissions()
    },
  })
}

export const getCurrentUserPermissionsOptions = () =>
  queryOptions({
    queryKey: queryKeys.currentUserPermissions(),
    queryFn: async (): Promise<string[]> => {
      await mockDelay()
      return mockStore.getCurrentUserPermissions()
    },
  })

export function useCurrentUserPermissions() {
  return useQuery(getCurrentUserPermissionsOptions())
}
