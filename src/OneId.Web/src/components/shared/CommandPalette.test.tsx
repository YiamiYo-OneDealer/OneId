import { render } from '@testing-library/react'
import { expect, describe, it } from 'vitest'
import { axe } from 'vitest-axe'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CommandPalette } from './CommandPalette'

function renderPalette(open: boolean, tier: 'internal' | 'tenant' = 'internal') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [{ path: '*', element: <CommandPalette open={open} onOpenChange={() => {}} tier={tier} tenantId={null} /> }],
    { initialEntries: ['/internal/tenants'] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('CommandPalette', () => {
  it('closed — no axe violations', async () => {
    const { container } = renderPalette(false)
    expect(await axe(container)).toHaveNoViolations()
  })

  it('open — no axe violations', async () => {
    const { container } = renderPalette(true)
    expect(await axe(container)).toHaveNoViolations()
  })

  it('open tenant tier — no axe violations', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const router = createMemoryRouter(
      [{ path: '*', element: <CommandPalette open={true} onOpenChange={() => {}} tier="tenant" tenantId="acme-corp" /> }],
      { initialEntries: ['/tenant/users'] },
    )
    const { container } = render(
      <QueryClientProvider client={qc}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    )
    expect(await axe(container)).toHaveNoViolations()
  })
})
