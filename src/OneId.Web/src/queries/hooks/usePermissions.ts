import { useQuery, queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { apiClient } from '@/lib/api-client'
import type { PermissionDto, PagedResponse } from '@/api/types'

export function usePermissions() {
  return useQuery({
    queryKey: queryKeys.permissions(),
    queryFn: () =>
      apiClient
        .get('api/internal/permissions', { searchParams: { pageSize: 200 } })
        .json<PagedResponse<PermissionDto>>()
        .then(r => r.items),
  })
}

// Current user's effective permissions are served via /connect/introspect (confidential client only).
// The SPA cannot call introspection directly — return empty until a dedicated /api/account/permissions
// endpoint is added in a future story.
export const getCurrentUserPermissionsOptions = () =>
  queryOptions({
    queryKey: queryKeys.currentUserPermissions(),
    queryFn: (): Promise<string[]> => Promise.resolve([]),
  })

export function useCurrentUserPermissions() {
  return useQuery(getCurrentUserPermissionsOptions())
}
