import { useCurrentUserPermissions } from '@/queries/hooks/usePermissions'

export function useHasPermission(permissionId: string): { permitted: boolean; isLoading: boolean } {
  const { data: permissions, isLoading } = useCurrentUserPermissions()
  if (isLoading || !permissions) return { permitted: false, isLoading: true }
  return { permitted: permissions.includes(permissionId), isLoading: false }
}
