import * as React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, describe, it, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { axe } from 'vitest-axe'
import { DenyOverrideSheet } from './DenyOverrideSheet'
import type { DenyOverride } from '@/features/users/schemas'

vi.mock('@/hooks/useHasPermission', () => ({
  useHasPermission: vi.fn(() => ({ permitted: true, isLoading: false })),
}))

vi.mock('@/features/users/api', () => ({
  useDeleteOverride: vi.fn(() => ({
    mutateAsync: vi.fn().mockResolvedValue(undefined),
  })),
  useRevokeUserTokens: vi.fn(() => ({
    mutateAsync: vi.fn().mockResolvedValue(undefined),
  })),
}))

import { useHasPermission } from '@/hooks/useHasPermission'

const DEMO_OVERRIDE: DenyOverride = {
  id: 'override-1',
  permissionId: 'od.users.deactivate',
  overrideType: 'DENY',
  reason: 'Pending compliance review',
  appliedByName: 'System Admin',
  appliedAt: '2026-05-01T10:00:00.000Z',
}

function makeQc() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } })
}

function renderSheet(override: DenyOverride | null, onClose = vi.fn()) {
  return render(
    <QueryClientProvider client={makeQc()}>
      <DenyOverrideSheet
        userId="user-1"
        tenantId="tenant-1"
        override={override}
        onClose={onClose}
      />
    </QueryClientProvider>,
  )
}

describe('DenyOverrideSheet', () => {
  beforeEach(() => {
    vi.mocked(useHasPermission).mockReturnValue({ permitted: true, isLoading: false })
  })

  it('renders override details when override is non-null', () => {
    renderSheet(DEMO_OVERRIDE)
    expect(screen.getByText('DENY Override')).toBeInTheDocument()
    expect(screen.getByText('Pending compliance review')).toBeInTheDocument()
    expect(screen.getByText('System Admin')).toBeInTheDocument()
    expect(screen.getByText('od.users.deactivate')).toBeInTheDocument()
  })

  it('shows "No reason provided" when reason is absent', () => {
    const overrideNoReason = { ...DEMO_OVERRIDE, reason: undefined }
    renderSheet(overrideNoReason)
    expect(screen.getByText('No reason provided')).toBeInTheDocument()
  })

  it('hides Force Re-authenticate button when not permitted', () => {
    vi.mocked(useHasPermission).mockReturnValue({ permitted: false, isLoading: false })
    renderSheet(DEMO_OVERRIDE)
    expect(screen.queryByRole('button', { name: /force re-authenticate/i })).not.toBeInTheDocument()
  })

  it('disables Force Re-authenticate button when permission is loading', () => {
    vi.mocked(useHasPermission).mockReturnValue({ permitted: false, isLoading: true })
    renderSheet(DEMO_OVERRIDE)
    const btn = screen.getByRole('button', { name: /force re-authenticate/i })
    expect(btn).toBeDisabled()
  })

  it('clicking Remove Override opens confirmation dialog', async () => {
    const user = userEvent.setup()
    renderSheet(DEMO_OVERRIDE)
    await user.click(screen.getByRole('button', { name: /remove override/i }))
    expect(screen.getByText('Remove this DENY override?')).toBeInTheDocument()
  })

  it('axe: no violations when sheet is open', async () => {
    const { container } = renderSheet(DEMO_OVERRIDE)
    await waitFor(() => screen.getByText('DENY Override'))
    expect(await axe(container)).toHaveNoViolations()
  })
})
