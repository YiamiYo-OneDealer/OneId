import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { useUsers } from '@/queries/hooks'
import type { User } from '@/mocks/types'

const columns: ColumnDef<User, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    accessorKey: 'email',
    header: 'Email',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.email}</span>
    ),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.status === 'active' ? 'default' : 'secondary'}>
        {row.original.status === 'active' ? 'Active' : 'Inactive'}
      </Badge>
    ),
  },
  {
    id: 'groups',
    header: 'Groups',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.groupIds.length}</span>
    ),
  },
  {
    accessorKey: 'lastLogin',
    header: 'Last Login',
    cell: ({ row }) => {
      const v = row.original.lastLogin
      if (!v) return <span className="text-muted-foreground">Never</span>
      return (
        <span className="text-muted-foreground">
          {new Date(v).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
          })}
        </span>
      )
    },
  },
]

export function TenantUsersPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: users = [], isLoading } = useUsers(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Users</h1>
      {!isLoading && users.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No users"
          description="Users will appear here once provisioned."
        />
      ) : (
        <DataTable
          columns={columns}
          data={users}
          isLoading={isLoading}
          aria-label="Users list"
        />
      )}
    </div>
  )
}
