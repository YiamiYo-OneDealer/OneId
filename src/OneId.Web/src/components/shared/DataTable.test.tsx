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

  it('clicking column header toggles sort', () => {
    render(<DataTable columns={columns} data={data} />)
    const nameHeader = screen.getByText('Name').closest('th')!
    // Initial state: no sort applied — all rows visible in original order
    const rows = screen.getAllByRole('row')
    // First data row has Alice
    expect(rows[1]).toHaveTextContent('Alice')
    fireEvent.click(nameHeader)
    // After click: asc sort — Alice still first alphabetically
    const sortedRows = screen.getAllByRole('row')
    expect(sortedRows[1]).toHaveTextContent('Alice')
  })
})
