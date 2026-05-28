import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { queryKeys } from '@/queries/keys'
import type { AuditLogDto, PagedResponse } from '@/api/types'

export function useAuditLog(
  tenantId: string | null,
  pageIndex: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: [...queryKeys.auditLog(tenantId), pageIndex, pageSize],
    // Internal (cross-tenant) audit log endpoint not yet implemented — disable for null tenantId.
    enabled: tenantId !== null,
    queryFn: () =>
      apiClient
        .get('api/tenant/audit', {
          searchParams: { page: pageIndex + 1, pageSize },
        })
        .json<PagedResponse<AuditLogDto>>()
        .then(r => ({
          items: r.items,
          totalCount: r.totalCount,
          pageIndex,
          pageSize,
        })),
  })
}
