import { useNavigate } from 'react-router'
import type { ColumnDef } from '@tanstack/react-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useUsers } from '@/queries/hooks'
import { useTenantStore } from '@/store/tenant-store'
import type { UserDto } from '@/api/types'

const columns: ColumnDef<UserDto, unknown>[] = [
  {
    accessorKey: 'displayName',
    header: 'Name',
    cell: ({ row }) => (
      <span>{row.original.displayName ?? row.original.email}</span>
    ),
  },
  {
    accessorKey: 'email',
    header: 'Email',
  },
  {
    accessorKey: 'isActive',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.isActive ? 'default' : 'secondary'}>
        {row.original.isActive ? 'Active' : 'Inactive'}
      </Badge>
    ),
  },
]

export function TenantUsersListPage() {
  const navigate = useNavigate()
  const activeTenantId = useTenantStore((s) => s.activeTenantId)
  const tenantId = activeTenantId ?? ''
  const { data: users, isLoading, isError } = useUsers(tenantId)

  if (isError) return <EmptyState variant="error" />

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Users</h1>
        <Button onClick={() => navigate('/tenant/users/new')}>New User</Button>
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
