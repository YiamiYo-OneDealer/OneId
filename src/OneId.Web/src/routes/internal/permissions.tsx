import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { usePermissions } from '@/queries/hooks'
import type { Permission } from '@/mocks/types'

const columns: ColumnDef<Permission, unknown>[] = [
  {
    accessorKey: 'id',
    header: 'Permission ID',
    cell: ({ row }) => (
      <span className="font-mono text-sm text-foreground">{row.original.id}</span>
    ),
  },
  {
    accessorKey: 'domain',
    header: 'Domain',
    cell: ({ row }) => (
      <span className="capitalize text-muted-foreground">{row.original.domain}</span>
    ),
  },
  {
    accessorKey: 'description',
    header: 'Description',
    cell: ({ row }) => <span className="text-foreground">{row.original.description}</span>,
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

export function PermissionsPage() {
  const { data: permissions = [], isLoading } = usePermissions()

  const sorted = [...permissions].sort((a, b) =>
    a.domain !== b.domain ? a.domain.localeCompare(b.domain) : a.id.localeCompare(b.id),
  )

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-foreground">Permissions</h1>

      {!isLoading && sorted.length === 0 ? (
        <EmptyState variant="no-data" title="No permissions defined" />
      ) : (
        <DataTable
          columns={columns}
          data={sorted}
          isLoading={isLoading}
          aria-label="Global permissions catalog"
        />
      )}
    </div>
  )
}
