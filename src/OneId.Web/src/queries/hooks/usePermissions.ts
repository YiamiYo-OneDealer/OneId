import { useQuery } from '@tanstack/react-query'
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
