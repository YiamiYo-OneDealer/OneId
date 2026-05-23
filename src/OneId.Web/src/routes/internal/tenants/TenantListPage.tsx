import { Link } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useTenants } from '@/queries/hooks'
import type { Tenant } from '@/mocks/types'

const columns: ColumnDef<Tenant, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <Link
        to={`/internal/tenants/${row.original.id}`}
        className="font-medium text-foreground hover:underline"
      >
        {row.original.name}
      </Link>
    ),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.status === 'active' ? 'default' : 'destructive'}>
        {row.original.status === 'active' ? 'Active' : 'Suspended'}
      </Badge>
    ),
  },
  {
    accessorKey: 'seatUsage',
    header: 'Seat Usage',
    cell: ({ row }) => {
      const { used, max } = row.original.seatUsage
      return (
        <span className="text-muted-foreground">
          {used} / {max === null ? '∞' : max}
        </span>
      )
    },
  },
  {
    accessorKey: 'createdAt',
    header: 'Created',
    cell: ({ row }) =>
      new Date(row.original.createdAt).toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      }),
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => (
      <Button variant="outline" size="sm" asChild>
        <Link to={`/internal/tenants/${row.original.id}`}>View</Link>
      </Button>
    ),
  },
]

export function TenantListPage() {
  const { data: tenants = [], isLoading } = useTenants()

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-foreground">Tenants</h1>
        <Button size="sm" asChild>
          <Link to="/internal/tenants/new">New Tenant</Link>
        </Button>
      </div>

      {!isLoading && tenants.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No tenants yet"
          description="Tenants will appear here once provisioned."
        />
      ) : (
        <DataTable
          columns={columns}
          data={tenants}
          isLoading={isLoading}
          aria-label="Tenants list"
        />
      )}
    </div>
  )
}
