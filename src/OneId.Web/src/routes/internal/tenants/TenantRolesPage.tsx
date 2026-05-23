import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useRoles } from '@/queries/hooks'
import type { Role } from '@/mocks/types'

const columns: ColumnDef<Role, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    id: 'permissions',
    header: 'Permissions',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.permissionIds.length}</span>
    ),
  },
]

export function TenantRolesPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: roles = [], isLoading } = useRoles(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Roles</h1>
      {!isLoading && roles.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No roles"
          description="Roles will appear here once created."
        />
      ) : (
        <DataTable
          columns={columns}
          data={roles}
          isLoading={isLoading}
          aria-label="Roles list"
        />
      )}
    </div>
  )
}
