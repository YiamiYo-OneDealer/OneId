import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { useGroups } from '@/queries/hooks'
import type { Group } from '@/mocks/types'

const columns: ColumnDef<Group, unknown>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => (
      <span className="font-medium text-foreground">{row.original.name}</span>
    ),
  },
  {
    accessorKey: 'memberCount',
    header: 'Members',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.memberCount}</span>
    ),
  },
  {
    id: 'roles',
    header: 'Roles',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.roleIds.length}</span>
    ),
  },
  {
    id: 'roleSets',
    header: 'Role Sets',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.original.roleSetIds.length}</span>
    ),
  },
]

export function TenantGroupsPage() {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const { data: groups = [], isLoading } = useGroups(tenantId)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-foreground">Groups</h1>
      {!isLoading && groups.length === 0 ? (
        <EmptyState
          variant="no-data"
          title="No groups"
          description="Groups will appear here once created."
        />
      ) : (
        <DataTable
          columns={columns}
          data={groups}
          isLoading={isLoading}
          aria-label="Groups list"
        />
      )}
    </div>
  )
}
