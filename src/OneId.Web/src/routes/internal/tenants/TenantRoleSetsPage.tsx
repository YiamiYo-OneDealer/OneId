import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useRoleSets } from '@/queries/hooks'
import type { RoleSet } from '@/mocks/types'

const columns: ColumnDef<RoleSet, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    id: 'roles',
    header: 'Roles',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.roleIds.length}</span>
    ),
  },
]

export function TenantRoleSetsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: roleSets = [], isLoading } = useRoleSets(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Role Sets</h1>
      {!isLoading && roleSets.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No role sets"
          description="Role sets will appear here once created."
        />
      ) : (
        <DataTable
          columns={columns}
          data={roleSets}
          isLoading={isLoading}
          aria-label="Role Sets list"
        />
      )}
    </div>
  )
}
