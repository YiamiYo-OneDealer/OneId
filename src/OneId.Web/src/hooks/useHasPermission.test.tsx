import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useHasPermission } from './useHasPermission'

vi.mock('@/queries/hooks/usePermissions', () => ({
  useCurrentUserPermissions: vi.fn(),
}))

import { useCurrentUserPermissions } from '@/queries/hooks/usePermissions'
const mockUseCurrentUserPermissions = vi.mocked(useCurrentUserPermissions)

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children)
}

function TestButton({ permissionId }: { permissionId: string }) {
  const { permitted, isLoading } = useHasPermission(permissionId)
  return (
    <button disabled={isLoading || !permitted} data-testid="btn">
      Action
    </button>
  )
}

describe('useHasPermission', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders disabled button while isLoading is true', () => {
    mockUseCurrentUserPermissions.mockReturnValue({
      data: undefined,
      isLoading: true,
    } as ReturnType<typeof useCurrentUserPermissions>)

    render(
      React.createElement(
        createWrapper(),
        null,
        React.createElement(TestButton, { permissionId: 'od.admin.users.view' }),
      ),
    )

    expect(screen.getByTestId('btn')).toBeDisabled()
  })

  it('renders enabled button when user has the permission', () => {
    mockUseCurrentUserPermissions.mockReturnValue({
      data: ['od.admin.users.view', 'od.admin.tenants.view'],
      isLoading: false,
    } as ReturnType<typeof useCurrentUserPermissions>)

    render(
      React.createElement(
        createWrapper(),
        null,
        React.createElement(TestButton, { permissionId: 'od.admin.users.view' }),
      ),
    )

    expect(screen.getByTestId('btn')).not.toBeDisabled()
  })

  it('renders disabled button when user lacks the permission', () => {
    mockUseCurrentUserPermissions.mockReturnValue({
      data: ['od.admin.tenants.view'],
      isLoading: false,
    } as ReturnType<typeof useCurrentUserPermissions>)

    render(
      React.createElement(
        createWrapper(),
        null,
        React.createElement(TestButton, { permissionId: 'od.admin.users.view' }),
      ),
    )

    expect(screen.getByTestId('btn')).toBeDisabled()
  })
})
