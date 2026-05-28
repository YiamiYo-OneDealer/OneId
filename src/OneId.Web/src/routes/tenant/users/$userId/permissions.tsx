import { useParams } from 'react-router'
import { EffectivePermissionsPanel } from '@/features/users/components/EffectivePermissions'

export function UserPermissionsPage() {
  const { userId } = useParams<{ userId: string }>()

  if (!userId) return null

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-foreground">Effective Permissions</h1>
      <p className="text-sm text-muted-foreground">
        Resolved authorization state for this user, showing where each permission originates.
      </p>
      <EffectivePermissionsPanel mode="live" userId={userId} />
    </div>
  )
}
