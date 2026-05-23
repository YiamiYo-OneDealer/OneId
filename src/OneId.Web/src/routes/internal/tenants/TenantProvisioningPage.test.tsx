import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TenantProvisioningPage } from './TenantProvisioningPage'
import { mockStore } from '@/mocks/store'

function renderPage() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(
    [{ path: '*', element: <TenantProvisioningPage /> }],
    { initialEntries: ['/internal/tenants/new'] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('TenantProvisioningPage', () => {
  it('renders step 1 (Tenant Details) by default', () => {
    renderPage()
    expect(screen.getByText('Tenant Details')).toBeInTheDocument()
    expect(screen.getByLabelText('Name *')).toBeVisible()
  })

  it('step 1 validation: clicking Next with empty name shows error and stays on step 1', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByText('Tenant name is required.')).toBeInTheDocument()
    expect(screen.getByLabelText('Name *')).toBeVisible()
  })

  it('step 1 advance: fill in name and click Next renders License Configuration step', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => {
      expect(screen.getByText('License Configuration')).toBeInTheDocument()
      expect(screen.getByLabelText('Max Seats')).toBeVisible()
    })
  })

  it('back navigation: advance to step 2 then click Back renders step 1', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByLabelText('Name *')).toBeVisible()
  })

  it('step 2 live preview: typing a number shows seat preview', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.change(screen.getByLabelText('Max Seats'), { target: { value: '5' } })
    expect(screen.getByText('This tenant will allow up to 5 active users.')).toBeInTheDocument()
  })

  it('step 2 blank maxSeats: preview shows no seat limit', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    expect(screen.getByText('This tenant will have no seat limit.')).toBeInTheDocument()
  })

  it('step 2 invalid maxSeats: Next shows error and does not advance', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    // '0' is numeric but fails the >= 1 check (JSDOM sanitises non-numeric input to '' on number fields)
    fireEvent.change(screen.getByLabelText('Max Seats'), { target: { value: '0' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(
      screen.getByText('Enter a positive number, or leave blank for no seat limit.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Max Seats')).toBeVisible()
  })

  it('step 3 skip admin: checking Skip hides name/email and Next advances', async () => {
    renderPage()
    // Advance to step 3
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(
        screen.getByText('Skip for now — designate Tenant Admin later'),
      ).toBeInTheDocument(),
    )
    const checkbox = screen.getByRole('checkbox')
    fireEvent.click(checkbox)
    expect(screen.queryByLabelText('Name *')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByText('Review & Confirm')).toBeInTheDocument())
  })

  it('step 3 validation: with skip unchecked, Next with empty fields shows errors', async () => {
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(
        screen.getByText('Skip for now — designate Tenant Admin later'),
      ).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByText('Name is required.')).toBeInTheDocument()
    expect(screen.getByText('Email is required.')).toBeInTheDocument()
  })

  it('review step shows entered name, status, maxSeats, admin name/email', async () => {
    renderPage()
    // Step 1
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    // Step 2
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.change(screen.getByLabelText('Max Seats'), { target: { value: '25' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    // Step 3
    await waitFor(() =>
      expect(screen.getByText('Skip for now — designate Tenant Admin later')).toBeInTheDocument(),
    )
    fireEvent.change(screen.getByLabelText(/^Name \*$/), { target: { value: 'Jane Doe' } })
    fireEvent.change(screen.getByLabelText(/^Email \*$/), {
      target: { value: 'jane@example.com' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    // Review — wait for the "Create Tenant" button which is unique to step 4
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Create Tenant' })).toBeInTheDocument(),
    )
    expect(screen.getByText('Acme Corp')).toBeInTheDocument()
    expect(screen.getByText('active')).toBeInTheDocument()
    expect(screen.getByText('25')).toBeInTheDocument()
    expect(screen.getByText('Jane Doe')).toBeInTheDocument()
    expect(screen.getByText('jane@example.com')).toBeInTheDocument()
  })

  it('submit happy path: Create Tenant navigates to the new tenant detail page', async () => {
    const qc = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    const router = createMemoryRouter(
      [
        { path: '/internal/tenants/new', element: <TenantProvisioningPage /> },
        { path: '/internal/tenants/:tenantId', element: <div>Tenant Created</div> },
      ],
      { initialEntries: ['/internal/tenants/new'] },
    )
    render(
      <QueryClientProvider client={qc}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    )
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(screen.getByText('Skip for now — designate Tenant Admin later')).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('checkbox'))
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Create Tenant' })).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Create Tenant' }))
    await waitFor(() => expect(screen.getByText('Tenant Created')).toBeInTheDocument())
  })

  it('submit error: failed mutation shows error message', async () => {
    vi.spyOn(mockStore, 'createTenant').mockImplementationOnce(() => {
      throw new Error('Server error')
    })
    renderPage()
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(screen.getByText('Skip for now — designate Tenant Admin later')).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('checkbox'))
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Create Tenant' })).toBeInTheDocument(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Create Tenant' }))
    await waitFor(() =>
      expect(
        screen.getByText('Failed to create tenant. Please try again.'),
      ).toBeInTheDocument(),
    )
    expect(screen.getByRole('button', { name: 'Create Tenant' })).toBeInTheDocument()
  })

  it('review Edit link for Tenant Details navigates back to step 1', async () => {
    renderPage()
    // Advance to review
    fireEvent.change(screen.getByLabelText('Name *'), { target: { value: 'Acme Corp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(screen.getByLabelText('Max Seats')).toBeVisible())
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(screen.getByText('Skip for now — designate Tenant Admin later')).toBeInTheDocument(),
    )
    const checkbox = screen.getByRole('checkbox')
    fireEvent.click(checkbox)
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    // Wait for "Create Tenant" button — unique to step 4
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Create Tenant' })).toBeInTheDocument(),
    )
    // Click first Edit button (Tenant Details section)
    const editButtons = screen.getAllByRole('button', { name: 'Edit' })
    fireEvent.click(editButtons[0])
    expect(screen.getByLabelText('Name *')).toBeVisible()
  })
})
