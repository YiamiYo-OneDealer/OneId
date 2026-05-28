import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { NewUserPage } from './new'
import { mockStore } from '@/mocks/store'
import { useTenantStore } from '@/store/tenant-store'

function renderPage(extraRoutes?: { path: string; element: React.ReactElement }[]) {
  mockStore.resetState()
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(
    [
      { path: '/tenant/users/new', element: <NewUserPage /> },
      ...(extraRoutes ?? []),
    ],
    { initialEntries: ['/tenant/users/new'] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('NewUserPage', () => {
  beforeEach(() => {
    useTenantStore.setState({ activeTenantId: 'acme-corp' })
  })

  afterEach(() => {
    useTenantStore.setState({ activeTenantId: null })
    mockStore.resetState()
  })

  it('renders stepper with all 4 steps visible in the sidebar', () => {
    renderPage()
    expect(screen.getByText('User Details')).toBeInTheDocument()
    expect(screen.getByText('Group Assignments')).toBeInTheDocument()
    expect(screen.getByText('Dimension Assignments')).toBeInTheDocument()
    expect(screen.getByText('Review & Confirm')).toBeInTheDocument()
  })

  it('"Next" on step 1 with empty name shows inline error and does not advance', () => {
    renderPage()
    // Step 1 is the default — try to advance without filling name
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByText('Name is required.')).toBeInTheDocument()
    // Should still show name input (didn't advance)
    expect(screen.getByLabelText(/^Name/)).toBeInTheDocument()
  })

  it('"Next" on step 1 with valid name + email advances to step 2', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText(/^Name/), { target: { value: 'Jane Smith' } })
    fireEvent.change(screen.getByLabelText(/^Email/), { target: { value: 'jane@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => {
      expect(screen.getByPlaceholderText('Search groups…')).toBeInTheDocument()
    })
  })

  it('step 2 renders group checkbox list and EffectivePermissionsPanel preview area', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText(/^Name/), { target: { value: 'Jane' } })
    fireEvent.change(screen.getByLabelText(/^Email/), { target: { value: 'jane@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => {
      expect(screen.getByPlaceholderText('Search groups…')).toBeInTheDocument()
    })
    expect(screen.getByText('Permission Preview')).toBeInTheDocument()
    // Groups from acme tenant should be listed — wait for async query
    const groups = mockStore.getGroups('acme-corp')
    if (groups.length > 0) {
      await waitFor(() => {
        expect(screen.getByText(groups[0].name)).toBeInTheDocument()
      })
    }
  })

  it('"Create User" on step 4 calls createUser and navigates on success', async () => {
    renderPage([{ path: '/tenant/users', element: <div>Users List</div> }])
    // Advance through all steps
    fireEvent.change(screen.getByLabelText(/^Name/), { target: { value: 'Jane Smith' } })
    fireEvent.change(screen.getByLabelText(/^Email/), { target: { value: 'jane@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByPlaceholderText('Search groups…')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    // Step 3: Dimension Assignments
    await waitFor(() =>
      expect(screen.getByText('Dimension assignments can be configured after user creation.')).toBeInTheDocument()
    )
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    // Step 4: Review & Confirm
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Create User' })).toBeInTheDocument()
    )
    const initialCount = mockStore.getUsers('acme-corp').length
    fireEvent.click(screen.getByRole('button', { name: 'Create User' }))
    await waitFor(() => {
      expect(mockStore.getUsers('acme-corp').length).toBe(initialCount + 1)
    })
    await waitFor(() => {
      expect(screen.getByText('Users List')).toBeInTheDocument()
    })
  })
})
