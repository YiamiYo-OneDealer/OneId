import { render, screen, fireEvent } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { axe } from 'vitest-axe'
import { GlobalNav } from './GlobalNav'

function renderNav(tier: 'internal' | 'tenant', initialPath = '/tenant/users') {
  const router = createMemoryRouter(
    [{ path: '*', element: <GlobalNav tier={tier} /> }],
    { initialEntries: [initialPath] },
  )
  return render(<RouterProvider router={router} />)
}

describe('GlobalNav', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('renders Tenant Admin nav items for tenant tier', () => {
    renderNav('tenant')
    expect(screen.getByText('Users')).toBeInTheDocument()
    expect(screen.getByText('Groups')).toBeInTheDocument()
    expect(screen.getByText('Roles')).toBeInTheDocument()
    expect(screen.getByText('Role Sets')).toBeInTheDocument()
    expect(screen.getByText('Audit Log')).toBeInTheDocument()
  })

  it('renders Internal Admin nav items for internal tier', () => {
    renderNav('internal', '/internal')
    expect(screen.getByText('Tenants')).toBeInTheDocument()
    expect(screen.getByText('Permissions')).toBeInTheDocument()
    expect(screen.getByText('Licenses')).toBeInTheDocument()
  })

  it('does NOT show TenantSwitcher for tenant tier', () => {
    renderNav('tenant')
    expect(screen.queryByText('Select tenant')).not.toBeInTheDocument()
  })

  it('shows TenantSwitcher for internal tier', () => {
    renderNav('internal', '/internal')
    expect(screen.getByText('Select tenant')).toBeInTheDocument()
  })

  it('collapses and expands on toggle button click', () => {
    renderNav('tenant')
    const toggle = screen.getByRole('button', { name: /collapse sidebar/i })
    fireEvent.click(toggle)
    expect(screen.queryByText('Users')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /expand sidebar/i })).toBeInTheDocument()
  })

  it('persists collapsed state to localStorage', () => {
    renderNav('tenant')
    fireEvent.click(screen.getByRole('button', { name: /collapse sidebar/i }))
    expect(localStorage.getItem('oneid:sidebar:collapsed')).toBe('true')
  })

  it('restores collapsed state from localStorage', () => {
    localStorage.setItem('oneid:sidebar:collapsed', 'true')
    renderNav('tenant')
    expect(screen.getByRole('button', { name: /expand sidebar/i })).toBeInTheDocument()
  })

  it('has no axe violations (tenant tier)', async () => {
    const { container } = renderNav('tenant')
    expect(await axe(container)).toHaveNoViolations()
  })
})
