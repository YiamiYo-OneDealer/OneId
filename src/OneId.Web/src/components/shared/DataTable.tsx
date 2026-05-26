import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  type ColumnDef,
  type SortingState,
  type OnChangeFn,
  type PaginationState,
} from '@tanstack/react-table'
import { useState } from 'react'
import { cn } from '@/lib/utils'
import { Skeleton } from '@/components/ui/skeleton'
import { ChevronUp, ChevronDown, ChevronsUpDown } from 'lucide-react'

const SKELETON_ROW_COUNT = 5

interface PaginationConfig {
  pageIndex: number
  pageSize: number
  total: number
  onPaginationChange: OnChangeFn<PaginationState>
}

interface DataTableProps<TData extends object, TValue> {
  columns: ColumnDef<TData, TValue>[]
  data: TData[]
  isLoading?: boolean
  pagination?: PaginationConfig
  onSortingChange?: OnChangeFn<SortingState>
  manualSorting?: boolean
  onRowClick?: (row: TData) => void
  'aria-label'?: string
}

export function DataTable<TData extends object, TValue>({
  columns,
  data,
  isLoading = false,
  pagination,
  onSortingChange,
  manualSorting = false,
  onRowClick,
  'aria-label': ariaLabel,
}: DataTableProps<TData, TValue>) {
  const [sorting, setSorting] = useState<SortingState>([])

  const handleSortingChange: OnChangeFn<SortingState> = (updater) => {
    setSorting(updater)
    onSortingChange?.(updater)
  }

  const table = useReactTable({
    data: isLoading ? [] : data,
    columns,
    state: {
      sorting,
      ...(pagination && {
        pagination: {
          pageIndex: pagination.pageIndex,
          pageSize: pagination.pageSize,
        },
      }),
    },
    manualSorting,
    onSortingChange: handleSortingChange,
    ...(pagination && {
      manualPagination: true,
      pageCount: pagination.pageSize > 0 ? Math.ceil(pagination.total / pagination.pageSize) : 0,
      onPaginationChange: pagination.onPaginationChange,
    }),
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  return (
    <div className="w-full overflow-auto rounded-md border border-border">
      <table
        aria-busy={isLoading || undefined}
        aria-label={ariaLabel}
        className="w-full caption-bottom text-sm"
      >
        <thead className="border-b border-border bg-card">
          {table.getHeaderGroups().map((headerGroup) => (
            <tr key={headerGroup.id}>
              {headerGroup.headers.map((header) => {
                const canSort = header.column.getCanSort()
                const sortDir = header.column.getIsSorted()
                return (
                  <th
                    key={header.id}
                    className={cn(
                      'h-10 px-4 text-left align-middle text-xs font-medium text-muted-foreground',
                      canSort && 'cursor-pointer select-none hover:text-foreground',
                    )}
                    onClick={canSort ? header.column.getToggleSortingHandler() : undefined}
                  >
                    {header.isPlaceholder ? null : (
                      <div className="flex items-center gap-1">
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {canSort && (
                          <span aria-hidden="true">
                            {sortDir === 'asc' ? (
                              <ChevronUp size={14} />
                            ) : sortDir === 'desc' ? (
                              <ChevronDown size={14} />
                            ) : (
                              <ChevronsUpDown size={14} className="opacity-40" />
                            )}
                          </span>
                        )}
                      </div>
                    )}
                  </th>
                )
              })}
            </tr>
          ))}
        </thead>
        <tbody>
          {isLoading
            ? Array.from({ length: SKELETON_ROW_COUNT }).map((_, rowIdx) => (
                <tr key={`skeleton-${rowIdx}`} className="border-b border-border">
                  {columns.map((_, colIdx) => (
                    <td key={colIdx} className="px-4 py-3">
                      <Skeleton className="h-4 w-full" />
                    </td>
                  ))}
                </tr>
              ))
            : table.getRowModel().rows.map((row) => (
                <tr
                  key={row.id}
                  onClick={onRowClick ? () => onRowClick(row.original) : undefined}
                  className={cn(
                    'border-b border-border transition-colors hover:bg-card',
                    onRowClick && 'cursor-pointer',
                  )}
                >
                  {row.getVisibleCells().map((cell) => (
                    <td key={cell.id} className="px-4 py-3 text-foreground">
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))}
        </tbody>
      </table>
    </div>
  )
}
