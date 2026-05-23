import { render, screen, fireEvent } from '@testing-library/react'
import { axe } from 'vitest-axe'
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

  it('renders CTA button when action provided', () => {
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

  it('allows custom icon override', () => {
    const CustomIcon = () => <svg data-testid="custom-icon" />
    render(<EmptyState variant="empty" icon={CustomIcon} title="Custom" />)
    expect(screen.getByTestId('custom-icon')).toBeInTheDocument()
  })

  it('has no axe violations', async () => {
    const { container } = render(<EmptyState variant="no-data" title="No users" />)
    const results = await axe(container)
    expect(results).toHaveNoViolations()
  })
})
