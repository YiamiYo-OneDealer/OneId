# Story 5a.4: DataTable and EmptyState Components

Status: review

## Story

As a developer,
I want reusable `DataTable` and `EmptyState` components wired with loading states and ARIA from day one,
So that every list view in the application has consistent behaviour and accessibility without per-page implementation.

## Acceptance Criteria

1. **DataTable loading skeleton** — `DataTable` renders with `isLoading: true`. Skeleton rows appear in place of data rows; column count matches real column definitions. `aria-busy="true"` on the table container during initial fetch. When data loads, `aria-busy` is removed and real rows replace Skeleton rows without layout shift.

2. **DataTable client-side sorting** — Clicking a column header activates client-side sorting (`getSortedRowModel`). Rows re-order without a network request. Server-side sorting is opt-in via `onSortingChange` + `manualSorting` props — both modes work without modifying the component.

3. **DataTable pagination** — When `pagination` prop is provided, `onPaginationChange` fires with the new pagination state on page change. Filtering is injected from the page level via props — no filter input is rendered inside `DataTable` itself.

4. **EmptyState rendering** — `EmptyState` renders a centered lucide icon (`text-muted-foreground`), bold title, description naming a next action, and an optional primary CTA button. `<div role="status">` wraps the component so screen readers announce the state change when it replaces a table. The four required variants work: `no-data` (with CTA), `no-results` (no CTA), `error`, `empty`.

5. **DataTable vitest test** — A test asserts Skeleton rows are present during `isLoading=true` and absent after `isLoading=false`. `aria-busy` is asserted present during loading and absent after data loads.

## Tasks / Subtasks

- [x] Install `@tanstack/react-table` and shadcn `skeleton` component (AC: all)
  - [x] Inside `src/OneId.Web/`: `npm install @tanstack/react-table`
  - [x] Inside `src/OneId.Web/`: `npx shadcn add skeleton` (then move to `src/components/ui/` if needed — see Story 5a-3 debug note)
  - [x] Verify `@tanstack/react-table` appears in `package.json` dependencies
  - [x] Verify `src/components/ui/skeleton.tsx` exists
- [x] Create `DataTable` component (AC: #1, #2, #3)
  - [x] Create `src/OneId.Web/src/components/shared/DataTable.tsx` (see full spec below)
  - [x] Generic `DataTable<TData extends object, TValue>` with `columns: ColumnDef<TData, TValue>[]`, `data: TData[]`, `isLoading?: boolean`, `pagination?: PaginationConfig`, `onSortingChange?: OnChangeFn<SortingState>`, `manualSorting?: boolean`
  - [x] `isLoading=true` → render N (default 5) skeleton rows; each cell renders a `<Skeleton>` that matches cell width; `aria-busy="true"` on `<table>`
  - [x] `isLoading=false` → real rows, no `aria-busy`
  - [x] Client-side sorting via internal `sorting` state + `getSortedRowModel()`; header click toggles sort direction
  - [x] Sort indicator: ChevronUp / ChevronDown / ChevronsUpDown icon in header for active/inactive columns
- [x] Create `EmptyState` component (AC: #4)
  - [x] Create `src/OneId.Web/src/components/shared/EmptyState.tsx` (see full spec below)
  - [x] Props: `variant: 'no-data' | 'no-results' | 'error' | 'empty'`, `title?: string`, `description?: string`, `icon?: React.ElementType`, `action?: { label: string; onClick: () => void }`
  - [x] Default icons per variant: `no-data` → `Inbox`, `no-results` → `SearchX`, `error` → `AlertCircle`, `empty` → `Inbox`
  - [x] `<div role="status">` as outer wrapper
  - [x] `text-muted-foreground` on icon (size 48px)
- [x] Write `DataTable.test.tsx` (AC: #1, #5)
  - [x] Create `src/OneId.Web/src/components/shared/DataTable.test.tsx`
  - [x] Test: `isLoading=true` → skeleton rows visible, `aria-busy="true"` on table
  - [x] Test: `isLoading=false` with data → real rows visible, no `aria-busy`
  - [x] Test: column header click toggles sort icon / row order
- [x] Write `EmptyState.test.tsx` (AC: #4)
  - [x] Create `src/OneId.Web/src/components/shared/EmptyState.test.tsx`
  - [x] Test: `role="status"` present on wrapper
  - [x] Test: `no-data` variant renders icon + title + CTA
  - [x] Test: `no-results` variant renders without CTA
  - [x] Test: `error` variant renders AlertCircle icon
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: all)

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**`@tanstack/react-table` is NOT installed.** This story installs it. The correct version for this project is `@tanstack/react-table@^8` (v8 is TanStack Table v8, not v9). Import from `@tanstack/react-table` (NOT `react-table`).

**shadcn `skeleton` is NOT installed.** Run `npx shadcn add skeleton` from `src/OneId.Web/`. The installer creates files at `@/components/ui/` literally — you MUST move the generated file to `src/components/ui/skeleton.tsx` (same bug as Story 5a-3). Add `src/components/ui/**` is already in `globalIgnores` in `eslint.config.js` — no change needed.

**`src/components/shared/` already exists** with: `GlobalNav.tsx`, `AdminTierBanner.tsx`, `TenantSwitcher.tsx`, `Breadcrumbs.tsx` and their tests. `DataTable.tsx` and `EmptyState.tsx` are the NEW files this story creates.

**ESLint design-token rule** bans raw Tailwind colors (`text-zinc-600`, `bg-zinc-800`, etc.) in JSX className. Use semantic tokens: `text-muted-foreground` for dimmed content (not `text-zinc-400`), `text-foreground` for primary text, `bg-card` for row hover.

**Previous story debug (5a-3):** shadcn `npx shadcn add` writes files to `./@/components/ui/` relative to current directory, not resolving the `@` alias. Always move installed files to `src/components/ui/` manually.

---

### Install Commands (Exact)

```bash
# From src/OneId.Web/ directory:
npm install @tanstack/react-table

# Install shadcn skeleton:
npx shadcn add skeleton --yes

# Then move if wrongly placed (check first):
find . -name "skeleton.tsx" 2>/dev/null
# If found at ./@/components/ui/skeleton.tsx:
mv @/components/ui/skeleton.tsx src/components/ui/skeleton.tsx
rmdir -p @/components/ui 2>/dev/null || true
```

---

### DataTable Component — Full Implementation Spec

**File: `src/OneId.Web/src/components/shared/DataTable.tsx`** — NEW

```tsx
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
}

export function DataTable<TData extends object, TValue>({
  columns,
  data,
  isLoading = false,
  pagination,
  onSortingChange,
  manualSorting = false,
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
      pageCount: Math.ceil(pagination.total / pagination.pageSize),
      onPaginationChange: pagination.onPaginationChange,
    }),
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  return (
    <div className="w-full overflow-auto rounded-md border border-border">
      <table
        aria-busy={isLoading || undefined}
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
                  {columns.map((col, colIdx) => (
                    <td key={colIdx} className="px-4 py-3">
                      <Skeleton className="h-4 w-full" />
                    </td>
                  ))}
                </tr>
              ))
            : table.getRowModel().rows.map((row) => (
                <tr
                  key={row.id}
                  className="border-b border-border transition-colors hover:bg-card"
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
```

**Key implementation notes:**
- `aria-busy={isLoading || undefined}` — React renders `aria-busy="true"` when `true`, omits attribute when `undefined` (not `false`). This is the correct React idiom for boolean ARIA attributes.
- `data: isLoading ? [] : data` — pass empty array to TanStack Table during loading to avoid rendering empty real rows under skeletons.
- Skeleton rows use the actual `columns` array length to match column count — satisfies AC #1.
- `manualSorting` + `onSortingChange` passthrough supports server-side sort. When false (default), `getSortedRowModel()` handles client sorting internally.
- `getPaginationRowModel()` is NOT included — pagination is controlled externally (`manualPagination: true`).

---

### EmptyState Component — Full Implementation Spec

**File: `src/OneId.Web/src/components/shared/EmptyState.tsx`** — NEW

```tsx
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Inbox, SearchX, AlertCircle } from 'lucide-react'

type EmptyStateVariant = 'no-data' | 'no-results' | 'error' | 'empty'

const VARIANT_DEFAULTS: Record<
  EmptyStateVariant,
  { icon: React.ElementType; title: string; description: string }
> = {
  'no-data': {
    icon: Inbox,
    title: 'Nothing here yet',
    description: 'Add your first item to get started.',
  },
  'no-results': {
    icon: SearchX,
    title: 'No results found',
    description: 'Try adjusting your search or filters.',
  },
  error: {
    icon: AlertCircle,
    title: 'Something went wrong',
    description: 'An error occurred while loading data. Try again.',
  },
  empty: {
    icon: Inbox,
    title: 'Nothing to show',
    description: '',
  },
}

interface EmptyStateProps {
  variant?: EmptyStateVariant
  title?: string
  description?: string
  icon?: React.ElementType
  action?: {
    label: string
    onClick: () => void
  }
  className?: string
}

export function EmptyState({
  variant = 'empty',
  title,
  description,
  icon,
  action,
  className,
}: EmptyStateProps) {
  const defaults = VARIANT_DEFAULTS[variant]
  const Icon = icon ?? defaults.icon
  const resolvedTitle = title ?? defaults.title
  const resolvedDescription = description ?? defaults.description

  return (
    <div
      role="status"
      className={cn(
        'flex flex-col items-center justify-center gap-3 py-12 text-center',
        className,
      )}
    >
      <Icon size={48} className="text-muted-foreground" aria-hidden="true" />
      <div className="flex flex-col gap-1">
        <p className="text-sm font-semibold text-foreground">{resolvedTitle}</p>
        {resolvedDescription && (
          <p className="text-sm text-muted-foreground">{resolvedDescription}</p>
        )}
      </div>
      {action && (
        <Button size="sm" onClick={action.onClick}>
          {action.label}
        </Button>
      )}
    </div>
  )
}
```

**Key implementation notes:**
- `role="status"` on outer div — screen readers announce when EmptyState replaces a DataTable (AC #4).
- `icon` prop overrides variant default icon — allows per-page customization.
- `title` and `description` props override variant defaults — consumers can customize copy without new variants.
- `no-results` variant intentionally has no action prop usage even if consumer passes one — the action is to modify the search, not navigate. However, the component still accepts `action` for the `error` variant (retry button). Don't block `action` rendering for any variant.
- Icon size 48px (not 18px like nav icons) — EmptyState is the focal point of the view.
- `aria-hidden="true"` on icon — icon is decorative; description carries the meaning.

---

### Skeleton Component (shadcn generated)

**File: `src/OneId.Web/src/components/ui/skeleton.tsx`** — Generated by `npx shadcn add skeleton`

The generated file uses:
```typescript
import { cn } from "@/lib/utils"

function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="skeleton"
      className={cn("bg-accent animate-pulse rounded-md", className)}
      {...props}
    />
  )
}

export { Skeleton }
```

This component is already sized by the parent className. In DataTable, use `<Skeleton className="h-4 w-full" />` for full-width row cells.

---

### Test Spec — DataTable.test.tsx

**File: `src/OneId.Web/src/components/shared/DataTable.test.tsx`** — NEW

```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from './DataTable'

interface TestRow {
  id: string
  name: string
  age: number
}

const columns: ColumnDef<TestRow, string>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'age', header: 'Age' },
]

const data: TestRow[] = [
  { id: '1', name: 'Alice', age: 30 },
  { id: '2', name: 'Bob', age: 25 },
  { id: '3', name: 'Charlie', age: 35 },
]

describe('DataTable', () => {
  it('renders skeleton rows when isLoading is true', () => {
    render(<DataTable columns={columns} data={[]} isLoading />)
    // Skeleton rows use data-slot="skeleton"
    const skeletons = document.querySelectorAll('[data-slot="skeleton"]')
    // 5 rows × 2 columns = 10 skeleton cells
    expect(skeletons.length).toBe(10)
  })

  it('sets aria-busy="true" on table when loading', () => {
    render(<DataTable columns={columns} data={[]} isLoading />)
    const table = screen.getByRole('table')
    expect(table).toHaveAttribute('aria-busy', 'true')
  })

  it('does NOT set aria-busy when data is loaded', () => {
    render(<DataTable columns={columns} data={data} />)
    const table = screen.getByRole('table')
    expect(table).not.toHaveAttribute('aria-busy')
  })

  it('renders real rows when isLoading is false', () => {
    render(<DataTable columns={columns} data={data} />)
    expect(screen.getByText('Alice')).toBeInTheDocument()
    expect(screen.getByText('Bob')).toBeInTheDocument()
    expect(screen.getByText('Charlie')).toBeInTheDocument()
  })

  it('does NOT render data rows during loading', () => {
    render(<DataTable columns={columns} data={data} isLoading />)
    expect(screen.queryByText('Alice')).not.toBeInTheDocument()
  })

  it('renders column headers', () => {
    render(<DataTable columns={columns} data={data} />)
    expect(screen.getByText('Name')).toBeInTheDocument()
    expect(screen.getByText('Age')).toBeInTheDocument()
  })
})
```

---

### Test Spec — EmptyState.test.tsx

**File: `src/OneId.Web/src/components/shared/EmptyState.test.tsx`** — NEW

```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { EmptyState } from './EmptyState'

describe('EmptyState', () => {
  it('has role="status" on the wrapper', () => {
    render(<EmptyState variant="no-data" title="No users" />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders title text', () => {
    render(<EmptyState variant="no-data" title="No users yet" />)
    expect(screen.getByText('No users yet')).toBeInTheDocument()
  })

  it('renders description when provided', () => {
    render(<EmptyState variant="no-data" description="Add your first user." />)
    expect(screen.getByText('Add your first user.')).toBeInTheDocument()
  })

  it('renders CTA button for no-data variant when action provided', () => {
    const onClick = vi.fn()
    render(<EmptyState variant="no-data" action={{ label: 'Add User', onClick }} />)
    const btn = screen.getByRole('button', { name: 'Add User' })
    expect(btn).toBeInTheDocument()
    fireEvent.click(btn)
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('renders no CTA button when action is not provided', () => {
    render(<EmptyState variant="no-results" title="No results" />)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('renders default title from variant when title prop omitted', () => {
    render(<EmptyState variant="no-results" />)
    expect(screen.getByText('No results found')).toBeInTheDocument()
  })

  it('renders error variant with default title', () => {
    render(<EmptyState variant="error" />)
    expect(screen.getByText('Something went wrong')).toBeInTheDocument()
  })
})
```

**Test gotcha:** `vi.fn()` is available globally because `vitest/globals: true` in vite.config.ts — no import needed.

---

### TanStack Table v8 — API Quick Reference

```typescript
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  type ColumnDef,        // Column definition type
  type SortingState,     // [{ id: string; desc: boolean }]
  type OnChangeFn,       // Updater<T> → void
  type PaginationState,  // { pageIndex: number; pageSize: number }
} from '@tanstack/react-table'
```

**`useReactTable` config summary:**
- `data: TData[]` — the data array (empty during loading)
- `columns: ColumnDef<TData, TValue>[]` — column definitions from consumer
- `state.sorting` — controlled sort state
- `onSortingChange` — called when header clicked
- `manualSorting: true` — disables built-in sort (for server-sort); omit for client-sort
- `getSortedRowModel()` — required for client-side sort to work; include even in manual mode (no-op when manualSorting)
- `getCoreRowModel()` — always required
- `table.getHeaderGroups()` — returns header groups for rendering `<thead>`
- `table.getRowModel().rows` — returns sorted+filtered rows for rendering `<tbody>`
- `header.column.getCanSort()` — true if column has `enableSorting !== false` in columnDef
- `header.column.getIsSorted()` — returns `false | 'asc' | 'desc'`
- `header.column.getToggleSortingHandler()` — click handler for sorting

**Column definition pattern:**
```typescript
const columns: ColumnDef<User, string>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => <span>{row.original.name}</span>,
    enableSorting: true,  // default true
  },
  {
    id: 'actions',
    header: () => null,
    cell: ({ row }) => <ActionMenu user={row.original} />,
    enableSorting: false,  // no sort on action column
  },
]
```

**Important v8 gotchas:**
- `flexRender(columnDef.header, context)` must be used for both static strings and render functions — don't call header directly
- `row.id` is auto-generated by TanStack Table (index by default) — always use `row.id` as React key
- `header.id` is the column id — always stable for React keys
- `getPaginationRowModel()` — only include if doing client-side pagination; for server-side, use `manualPagination: true` and omit `getPaginationRowModel()`

---

### Skeleton in DataTable — Key Details

During loading, `isLoading=true` means:
1. `data: isLoading ? [] : data` — TanStack Table gets empty data (no real rows)
2. We render skeleton rows manually by iterating `Array.from({ length: SKELETON_ROW_COUNT })`
3. For each skeleton row, we iterate `columns.map((col, colIdx) => ...)` — this matches column count from consumer
4. Each skeleton cell: `<td><Skeleton className="h-4 w-full" /></td>`

This ensures column count matches without rendering any column accessor logic.

---

### Design System Tokens Reference

| Utility | Semantic Meaning |
|---|---|
| `text-muted-foreground` | Dimmed text, icon color in EmptyState |
| `text-foreground` | Primary text in cells and titles |
| `bg-card` | Row hover background |
| `bg-background` | Page background |
| `border-border` | Table/row borders |
| `bg-sidebar` | Not used in DataTable |
| `text-primary` | Active sort indicator (optional) |

**Do NOT use:** `text-zinc-*`, `bg-zinc-*`, `text-gray-*`, `bg-gray-*`, `text-slate-*`, `text-amber-*`, `text-indigo-*`.

---

### ARIA Requirements Summary

| Element | ARIA Requirement |
|---|---|
| `<table>` | `aria-busy="true"` when `isLoading=true`; attribute absent when loaded |
| `<th>` | Native table heading semantics — no extra ARIA needed |
| EmptyState outer `<div>` | `role="status"` |
| EmptyState icon | `aria-hidden="true"` |
| Sort icons | `aria-hidden="true"` on `<span>` wrapper |

---

### Files NOT Created in This Story

- `DataTable` pagination UI (page selector, page size picker) — story only wires `onPaginationChange` callback; the actual pagination UI can be a separate component in Epic 5c when the real list views are built
- `useActiveTenant` hook — referenced in some docs but not needed for these components
- `lib/api-client.ts` — Epic 2 dependency; not needed here
- `PageSkeleton.tsx` — architecture mentions it; deferred to Epic 5c
- Any real data fetching — components accept `data: TData[]` prop; fetching done by consumers

---

### Previous Story Learnings (from Stories 5a-1 through 5a-3)

1. **shadcn install bug** — `npx shadcn add <component>` creates files at `./@/components/ui/` literally. Always run `find . -name "skeleton.tsx"` after install and move if needed.
2. **`eslint.config.js` ignores `src/components/ui/**`** — already set in Story 5a-3; shadcn generated files are excluded. No further ESLint config changes needed.
3. **Design-token ESLint rule** — bans `bg-zinc-*`, `text-zinc-*`, `bg-amber-*`, `text-indigo-*`, etc. in JSX className. Use semantic aliases only.
4. **`react-refresh/only-export-components`** — each file must export ONLY components OR ONLY non-components. `DataTable.tsx` exports only `DataTable` (function component). `EmptyState.tsx` exports only `EmptyState`. The `VARIANT_DEFAULTS` object is not exported — keep it module-private.
5. **`aria-busy` boolean** — in React, `aria-busy={isLoading || undefined}` correctly renders the attribute only when true. `aria-busy={false}` renders the string `"false"` in HTML which is technically wrong for the use case.
6. **Vitest `vi.fn()`** — globally available, no import needed.
7. **Test pattern** — co-locate test files: `DataTable.test.tsx` next to `DataTable.tsx`. Use `createMemoryRouter` only if router context is needed (DataTable and EmptyState don't use routing — no router wrapper needed in tests).

---

### References

- Story 5a.1: [5a-1-design-system-foundation.md](./5a-1-design-system-foundation.md) — ESLint rule, Tailwind v4
- Story 5a.2: [5a-2-app-shell-routing-tenant-context-and-query-key-factory.md](./5a-2-app-shell-routing-tenant-context-and-query-key-factory.md) — query patterns
- Story 5a.3: [5a-3-globalnav-and-admintierbanner.md](./5a-3-globalnav-and-admintierbanner.md) — shadcn install bug, component patterns
- Architecture: [architecture.md](../planning-artifacts/architecture.md) — DataTable/EmptyState specs
- UX design: [ux-design-specification.md](../planning-artifacts/ux-design-specification.md) — DataTable anatomy
- Epics: [epics.md](../planning-artifacts/epics.md) — Story 5a.4 ACs
- TanStack Table v8 docs: `https://tanstack.com/table/v8/docs` (v8 stable — do NOT use v9 alpha APIs)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6 (create-story workflow, 2026-05-23)

### Debug Log References

- `npm install @tanstack/react-table` ran from project root the first time, installing to `OneId/node_modules/` instead of `src/OneId.Web/node_modules/`. Caused "Invalid hook call" errors due to duplicate React instances. Fixed by running install from inside `src/OneId.Web/`.
- shadcn `npx shadcn add skeleton` again created file at `./@/components/ui/skeleton.tsx` literally. Moved to `src/components/ui/skeleton.tsx` manually (same bug as Story 5a-3).
- `react-hooks/incompatible-library` warning from ESLint on `useReactTable()`. This is expected — React Compiler skips memoizing components that use TanStack Table's hook. Warning only (exit code 0), not an error. No fix needed.
- `aria-busy={isLoading || undefined}` — React renders attribute as `"true"` only when value is `true`; renders nothing when `undefined`. This is the correct idiom (not `aria-busy={false}`).

### Completion Notes List

- Installed `@tanstack/react-table` (v8) in `src/OneId.Web/`. Installed shadcn `skeleton` component, moved to correct path.
- Created `DataTable<TData extends object, TValue>` generic component. Loading: 5 skeleton rows × column count (matching via `columns.map()`). `aria-busy="true"` when loading, attribute absent when loaded. Client-side sorting via `getSortedRowModel()` + internal `sorting` state. Server-side sorting opt-in via `onSortingChange` + `manualSorting`. Sort indicators: ChevronUp/Down/ChevronsUpDown.
- Created `EmptyState` with 4 variants (`no-data`, `no-results`, `error`, `empty`). All props overridable. `role="status"` wrapper. Icon 48px with `text-muted-foreground`. Optional CTA via `action` prop.
- Tests: 7 DataTable tests (skeleton count, aria-busy, real rows, loading guard, headers, sort toggle) + 8 EmptyState tests (role, title, description, CTA fire, no CTA, variant defaults, custom icon). All 32 tests pass.
- Build: ✅ `npm run build` passes. Lint: ✅ `npm run lint` clean (1 expected warning). Tests: ✅ 32/32 pass.

### File List

- `src/OneId.Web/src/components/ui/skeleton.tsx` (new — shadcn generated)
- `src/OneId.Web/src/components/shared/DataTable.tsx` (new)
- `src/OneId.Web/src/components/shared/DataTable.test.tsx` (new)
- `src/OneId.Web/src/components/shared/EmptyState.tsx` (new)
- `src/OneId.Web/src/components/shared/EmptyState.test.tsx` (new)
- `src/OneId.Web/package.json` (modified — added `@tanstack/react-table`)
- `src/OneId.Web/package-lock.json` (modified — lockfile updated)

## Change Log

- 2026-05-23: Story implemented — DataTable (TanStack Table v8) and EmptyState components; shadcn Skeleton installed; 32 tests pass, build and lint clean.

### Review Findings

- [x] [Review][Dismiss] DataTable renders empty `<tbody>` when `!isLoading && data.length === 0` — consumer responsibility; each list view has different copy/CTA; consumers render `EmptyState` when needed
- [x] [Review][Patch] `<table>` has no `aria-label` — fixed: added `aria-label?: string` prop, forwarded to `<table>` element [`src/OneId.Web/src/components/shared/DataTable.tsx`]
- [x] [Review][Patch] `pageCount` division by zero when `pageSize=0` — fixed: guarded with `pagination.pageSize > 0 ? Math.ceil(...) : 0` [`src/OneId.Web/src/components/shared/DataTable.tsx`]
- [x] [Review][Dismiss] `handleSortingChange` calls `setSorting` when `manualSorting=true` — same root as D1; dual-state is intentional (optimistic sort-icon update)
- [x] [Review][Patch] Breadcrumbs UUID filter applied to computed label, not raw segment — fixed: UUID regex now tested against `lastSegment` before `segmentToLabel()` transformation [`src/OneId.Web/src/components/shared/Breadcrumbs.tsx`]
- [x] [Review][Defer] `getSortedRowModel()` unconditionally included even with `manualSorting=true` — runs a row model registration each render for rows that won't be client-sorted; intentional per spec note; deferred until server-side sort is actively used in Epic 5c [`src/OneId.Web/src/components/shared/DataTable.tsx:69`]
- [x] [Review][Defer] `Breadcrumbs.tsx` has no test file — `Breadcrumbs.test.tsx` does not exist; no coverage of UUID filter logic, breadcrumb rendering, or separator placement; deferred, add alongside P6 patch [`src/OneId.Web/src/components/shared/Breadcrumbs.tsx`]
- [x] [Review][Defer] Custom `toHaveNoViolations` matcher in `test-setup.ts` may conflict when `vitest-axe` ships an official one — defined manually because `vitest-axe@0.1.0` does not export it; monitor on `vitest-axe` upgrade [`src/OneId.Web/src/test-setup.ts`]
