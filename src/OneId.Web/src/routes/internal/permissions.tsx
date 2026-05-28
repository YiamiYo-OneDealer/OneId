import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { usePermissions } from '@/queries/hooks'
import type { PermissionDto } from '@/api/types'

const columns: ColumnDef<PermissionDto, unknown>[] = [
  {
    accessorKey: 'permissionId',
    header: 'Permission ID',
    cell: ({ row }) => (
      <span className="font-mono text-sm text-foreground">{row.original.permissionId}</span>
    ),
  },
  {
    id: 'domain',
    header: 'Domain',
    cell: ({ row }) => (
      <span className="capitalize text-muted-foreground">
        {row.original.permissionId.split('.')[0]}
      </span>
    ),
  },
  {
    accessorKey: 'label',
    header: 'Description',
    cell: ({ row }) => <span className="text-foreground">{row.original.label}</span>,
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <Badge variant={row.original.status === 'Active' ? 'default' : 'secondary'}>
        {row.original.status}
      </Badge>
    ),
  },
]

export function PermissionsPage() {
  const { data: permissions = [], isLoading } = usePermissions()

  const sorted = [...permissions].sort((a, b) => {
    const aDomain = a.permissionId.split('.')[0]
    const bDomain = b.permissionId.split('.')[0]
    return aDomain !== bDomain
      ? aDomain.localeCompare(bDomain)
      : a.permissionId.localeCompare(b.permissionId)
  })

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
