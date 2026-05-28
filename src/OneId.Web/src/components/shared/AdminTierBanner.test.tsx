import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClientProvider, QueryClient } from '@tanstack/react-query'
import { axe } from 'vitest-axe'
import { useTenantStore } from '@/store/tenant-store'
import { useUiStore } from '@/store/ui-store'
import { AdminTierBanner } from './AdminTierBanner'

function renderBanner(path = '/internal/tenants/test-tenant/users') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [{ path: '*', element: <AdminTierBanner /> }],
    { initialEntries: [path] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('AdminTierBanner', () => {
  afterEach(() => {
    useTenantStore.setState({ activeTenantId: null })
    useUiStore.setState({ isFormDirty: false })
  })

  it('renders when activeTenantId is set in store', () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    renderBanner()
    expect(screen.getByText(/Internal Admin/)).toBeInTheDocument()
    expect(screen.getByText(/test-tenant/)).toBeInTheDocument()
  })

  it('does NOT render when activeTenantId is null', () => {
    renderBanner('/internal')
    expect(screen.queryByText(/Internal Admin/)).not.toBeInTheDocument()
  })

  it('has aria-live="polite" and NOT role="alert"', () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    renderBanner()
    const banner = screen.getByText(/Internal Admin/).closest('[aria-live]')
    expect(banner).toHaveAttribute('aria-live', 'polite')
    expect(banner).not.toHaveAttribute('role', 'alert')
  })

  it('renders "All Tenants" navigation link', () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    renderBanner()
    expect(screen.getByText(/All Tenants/)).toBeInTheDocument()
  })

  it('has no axe violations', async () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    const { container } = renderBanner()
    expect(await axe(container)).toHaveNoViolations()
  })
})
