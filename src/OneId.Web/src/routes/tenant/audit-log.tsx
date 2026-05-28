import { useState } from 'react'
import { type ColumnDef, type PaginationState, type OnChangeFn } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { AuditEventSheet } from '@/components/shared/AuditEventSheet'
import { useAuditLog } from '@/queries/hooks'
import { useTenantStore } from '@/store/tenant-store'
import type { AuditLogDto } from '@/api/types'
import { formatTimestamp } from '@/routes/_audit-log-columns'

const columns: ColumnDef<AuditLogDto, unknown>[] = [
  {
    accessorKey: 'timestamp',
    header: 'Timestamp',
    cell: ({ row }) => (
      <span className="text-sm text-muted-foreground font-mono whitespace-nowrap">
        {formatTimestamp(row.original.timestamp)}
      </span>
    ),
  },
  {
    id: 'actor',
    header: 'Actor',
    cell: ({ row }) => {
      const { actorUserId } = row.original
      if (!actorUserId) return <span className="text-muted-foreground text-sm italic">System</span>
      return (
        <span className="text-sm font-mono text-foreground">{actorUserId}</span>
      )
    },
  },
  {
    accessorKey: 'action',
    header: 'Action',
    cell: ({ row }) => (
      <span className="font-mono text-sm text-foreground">{row.original.action}</span>
    ),
  },
  {
    accessorKey: 'entityType',
    header: 'Entity Type',
    cell: ({ row }) => (
      <span className="text-sm text-muted-foreground">{row.original.entityType}</span>
    ),
  },
  {
    accessorKey: 'entityId',
    header: 'Entity ID',
    cell: ({ row }) => (
      <span className="font-mono text-xs text-muted-foreground">{row.original.entityId}</span>
    ),
  },
]

const PAGE_SIZE = 25

export function TenantAuditLogPage() {
  const tenantId = useTenantStore((s) => s.activeTenantId)

  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: PAGE_SIZE,
  })
  const [selectedEntry, setSelectedEntry] = useState<AuditLogDto | null>(null)

  const { data, isLoading } = useAuditLog(tenantId, pagination.pageIndex, pagination.pageSize)

  const handlePaginationChange: OnChangeFn<PaginationState> = (updater) => {
    setPagination((prev) => (typeof updater === 'function' ? updater(prev) : updater))
  }

  if (!tenantId) return null

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-foreground">Audit Log</h1>

      {!isLoading && data?.totalCount === 0 ? (
        <EmptyState
          variant="no-data"
          title="No audit events"
          description="Management actions will appear here."
        />
      ) : (
        <DataTable
          columns={columns}
          data={data?.items ?? []}
          isLoading={isLoading}
          aria-label="Audit log"
          onRowClick={(entry) => setSelectedEntry(entry)}
          pagination={
            data
              ? {
                  pageIndex: data.pageIndex,
                  pageSize: data.pageSize,
                  total: data.totalCount,
                  onPaginationChange: handlePaginationChange,
                }
              : undefined
          }
        />
      )}

      <AuditEventSheet entry={selectedEntry} onClose={() => setSelectedEntry(null)} />
    </div>
  )
}
