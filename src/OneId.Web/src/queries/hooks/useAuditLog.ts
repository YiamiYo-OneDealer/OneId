import { useQuery } from '@tanstack/react-query'
import { mockStore, mockDelay } from '@/mocks/store'
import { queryKeys } from '@/queries/keys'

export function useAuditLog(
  tenantId: string | null,
  pageIndex: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: [...queryKeys.auditLog(tenantId), pageIndex, pageSize],
    queryFn: async () => {
      await mockDelay()
      return mockStore.getAuditLog(tenantId, pageIndex, pageSize)
    },
  })
}
