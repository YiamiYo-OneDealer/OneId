import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TenantUsersListPage } from './index'
import { mockStore } from '@/mocks/store'
import { useTenantStore } from '@/store/tenant-store'

function renderPage(routes?: { path: string; element: React.ReactElement }[], skipReset?: boolean) {
  if (!skipReset) mockStore.resetState()
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(
    routes ?? [
      { path: '*', element: <TenantUsersListPage /> },
    ],
    { initialEntries: ['/tenant/users'] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('TenantUsersListPage', () => {
  beforeEach(() => {
    useTenantStore.setState({ activeTenantId: 'acme-corp' })
  })

  afterEach(() => {
    useTenantStore.setState({ activeTenantId: null })
    mockStore.resetState()
  })

  it('renders user list from mock store', async () => {
    renderPage()
    // fixture data: Acme tenant has users — wait for async query
    const users = mockStore.getUsers('acme-corp')
    if (users.length > 0) {
      await waitFor(() => {
        expect(screen.getByText(users[0].name)).toBeInTheDocument()
      })
    }
    expect(screen.getByText('Users')).toBeInTheDocument()
  })

  it('shows EmptyState when no users', async () => {
    // Point to a non-existent tenant so getUsers returns []
    useTenantStore.setState({ activeTenantId: 'tenant-empty-99999' })
    renderPage()
    await waitFor(() => {
      expect(screen.getByText('Nothing to show')).toBeInTheDocument()
    })
  })

  it('"New User" button navigates to /tenant/users/new when not at seat limit', async () => {
    // Acme tenant has 8/25 seats (not at limit)
    renderPage([
      { path: '/tenant/users', element: <TenantUsersListPage /> },
      { path: '/tenant/users/new', element: <div>New User Page</div> },
    ])
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'New User' })).toBeEnabled()
    })
    fireEvent.click(screen.getByRole('button', { name: 'New User' }))
    await waitFor(() => {
      expect(screen.getByText('New User Page')).toBeInTheDocument()
    })
  })

  it('disables "New User" button when at seat limit', async () => {
    // Reset first, then set seatUsage to at limit BEFORE rendering (skipReset=true to preserve)
    mockStore.resetState()
    const tenant = mockStore.getTenant('acme-corp')
    if (tenant && tenant.seatUsage.max !== null) {
      tenant.seatUsage.used = tenant.seatUsage.max
    } else if (tenant) {
      tenant.seatUsage.max = 10
      tenant.seatUsage.used = 10
    }
    renderPage(undefined, true) // skipReset=true to preserve our mutation
    await waitFor(() => {
      const btn = screen.getByRole('button', { name: 'New User' })
      expect(btn).toBeDisabled()
    }, { timeout: 3000 })
  })
})
