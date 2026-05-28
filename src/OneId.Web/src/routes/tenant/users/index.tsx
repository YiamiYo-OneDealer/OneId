import { useNavigate } from 'react-router'
import type { ColumnDef } from '@tanstack/react-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { SeatUsageIndicator, isSeatLimitReached } from '@/components/shared/SeatUsageIndicator'
import { useUsers } from '@/queries/hooks'
import { useTenant } from '@/queries/hooks/useTenants'
import { useTenantStore } from '@/store/tenant-store'
import type { User } from '@/mocks/types'

const columns: ColumnDef<User, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
  },
  {
    accessorKey: 'email',
    header: 'Email',
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ getValue }) => {
      const status = getValue() as string
      return (
        <Badge variant={status === 'active' ? 'default' : 'secondary'}>
          {status === 'active' ? 'Active' : 'Inactive'}
        </Badge>
      )
    },
  },
  {
    accessorKey: 'lastLogin',
    header: 'Last Login',
    cell: ({ getValue }) => {
      const v = getValue() as string | null
      if (!v) return <span className="text-muted-foreground">Never</span>
      return new Date(v).toLocaleDateString()
    },
  },
]

export function TenantUsersListPage() {
  const navigate = useNavigate()
  const activeTenantId = useTenantStore((s) => s.activeTenantId)
  const tenantId = activeTenantId ?? ''
  const { data: users, isLoading, isError } = useUsers(tenantId)
  const { data: tenant } = useTenant(tenantId)

  const { used, max } = tenant?.seatUsage ?? { used: 0, max: null }
  const atSeatLimit = isSeatLimitReached(used, max)

  if (isError) return <EmptyState variant="error" />

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-foreground">Users</h1>
          <SeatUsageIndicator used={used} max={max} />
        </div>
        {atSeatLimit ? (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <span tabIndex={0}>
                  <Button disabled>New User</Button>
                </span>
              </TooltipTrigger>
              <TooltipContent>Seat limit reached — upgrade your license to add users</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ) : (
          <Button onClick={() => navigate('/tenant/users/new')}>New User</Button>
        )}
      </div>

      {!isLoading && users?.length === 0 ? (
        <EmptyState variant="empty" />
      ) : (
        <DataTable
          columns={columns}
          data={users ?? []}
          isLoading={isLoading}
          aria-label="Users"
          onRowClick={(row) => navigate(`/tenant/users/${row.id}/permissions`)}
        />
      )}
    </div>
  )
}
