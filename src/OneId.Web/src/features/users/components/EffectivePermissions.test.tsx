import * as React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, describe, it, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import { EffectivePermissionsPanel } from './EffectivePermissions'
import type { EffectivePermissionsResponse } from '@/features/users/schemas'
import { queryKeys } from '@/queries/keys'

// Seed a QueryClient with pre-loaded data (bypasses mockDelay)
function makeQueryClient(data: EffectivePermissionsResponse) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } })
  qc.setQueryData(queryKeys.effectivePermissions(data.userId), data)
  return qc
}

function renderWithData(data: EffectivePermissionsResponse) {
  const qc = makeQueryClient(data)
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <EffectivePermissionsPanel mode="live" userId={data.userId} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const USER_ID = 'u-acme-alice'

// Use IDs that exist in the permission registry so getPermissionLabel returns proper labels
const BASE_DATA: EffectivePermissionsResponse = {
  userId: USER_ID,
  resolvedAt: new Date().toISOString(),
  hasGroupAssignments: true,
  permissions: [
    {
      id: 'od.admin.users.view',
      label: 'View Users',
      isDenied: false,
      provenanceChain: [
        { nodeType: 'user', id: USER_ID, label: 'Alice', href: '' },
        { nodeType: 'group', id: 'g-1', label: 'HR Team', href: '/tenant/groups/g-1' },
        { nodeType: 'role', id: 'r-1', label: 'User Manager', href: '/tenant/roles/r-1' },
        { nodeType: 'permission', id: 'od.admin.users.view', label: 'od.admin.users.view', href: '' },
      ],
    },
    {
      id: 'od.admin.users.deactivate',
      label: 'Deactivate Users',
      isDenied: true,
      provenanceChain: [
        { nodeType: 'user', id: USER_ID, label: 'Alice', href: '' },
        { nodeType: 'group', id: 'g-1', label: 'HR Team', href: '/tenant/groups/g-1' },
        { nodeType: 'role', id: 'r-1', label: 'User Manager', href: '/tenant/roles/r-1' },
        { nodeType: 'permission', id: 'od.admin.users.deactivate', label: 'od.admin.users.deactivate', href: '' },
      ],
    },
  ],
}

const NO_GROUPS_DATA: EffectivePermissionsResponse = {
  userId: USER_ID,
  resolvedAt: new Date().toISOString(),
  hasGroupAssignments: false,
  permissions: [],
}

const NO_PERMISSIONS_DATA: EffectivePermissionsResponse = {
  userId: USER_ID,
  resolvedAt: new Date().toISOString(),
  hasGroupAssignments: true,
  permissions: [],
}

const ALL_DENIED_DATA: EffectivePermissionsResponse = {
  userId: USER_ID,
  resolvedAt: new Date().toISOString(),
  hasGroupAssignments: true,
  permissions: [
    {
      id: 'od.admin.users.view',
      label: 'View Users',
      isDenied: true,
      provenanceChain: [
        { nodeType: 'user', id: USER_ID, label: 'Alice', href: '' },
        { nodeType: 'group', id: 'g-1', label: 'HR Team', href: '/tenant/groups/g-1' },
        { nodeType: 'role', id: 'r-1', label: 'User Manager', href: '/tenant/roles/r-1' },
        { nodeType: 'permission', id: 'od.admin.users.view', label: 'od.admin.users.view', href: '' },
      ],
    },
  ],
}

describe('EffectivePermissionsPanel', () => {
  describe('loading state', () => {
    it('renders Skeleton rows and aria-busy during initial fetch', () => {
      // Don't pre-seed — staleTime:0 will trigger a fetch
      const qc = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } })
      const { container } = render(
        <QueryClientProvider client={qc}>
          <MemoryRouter>
            <EffectivePermissionsPanel mode="live" userId={USER_ID} />
          </MemoryRouter>
        </QueryClientProvider>,
      )
      const busyEl = container.querySelector('[aria-busy="true"]')
      expect(busyEl).not.toBeNull()
    })
  })

  describe('Capabilities tab (default)', () => {
    it('shows human-readable permission labels', () => {
      renderWithData(BASE_DATA)
      expect(screen.getByText('View Users')).toBeInTheDocument()
    })

    it('shows DENY badge for denied permissions', () => {
      renderWithData(BASE_DATA)
      const badges = screen.getAllByText('DENY')
      expect(badges.length).toBeGreaterThan(0)
    })

    it('shows collapsed provenance chip "via Group: HR Team"', () => {
      renderWithData(BASE_DATA)
      expect(screen.getAllByText('HR Team')[0]).toBeInTheDocument()
    })
  })

  describe('search', () => {
    it('filters permissions by label', async () => {
      const user = userEvent.setup()
      renderWithData(BASE_DATA)
      const input = screen.getByRole('searchbox', { name: /search permissions/i })
      await user.type(input, 'view')
      expect(screen.getByText('View Users')).toBeInTheDocument()
      expect(screen.queryByText('Deactivate Users')).not.toBeInTheDocument()
    })

    it('filters permissions by id', async () => {
      const user = userEvent.setup()
      renderWithData(BASE_DATA)
      const input = screen.getByRole('searchbox', { name: /search permissions/i })
      await user.type(input, 'od.admin.users.deactivate')
      expect(screen.getByText('Deactivate Users')).toBeInTheDocument()
      expect(screen.queryByText('View Users')).not.toBeInTheDocument()
    })
  })

  describe('empty states', () => {
    it('shows "No group assignments" empty state when user has no groups', () => {
      renderWithData(NO_GROUPS_DATA)
      expect(screen.getByText('No group assignments')).toBeInTheDocument()
    })

    it('shows "No permissions in groups" when groups exist but no permissions', () => {
      renderWithData(NO_PERMISSIONS_DATA)
      expect(screen.getByText('No permissions in groups')).toBeInTheDocument()
    })

    it('shows "All permissions DENY-overridden" when all permissions are denied', () => {
      renderWithData(ALL_DENIED_DATA)
      expect(screen.getByText('All permissions DENY-overridden')).toBeInTheDocument()
    })
  })

  describe('propagation dimming', () => {
    it('applies opacity-60 class when isFetching without isLoading (background refetch)', async () => {
      // Pre-seed with staleTime:0 so refetch triggers immediately
      const qc = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } })
      qc.setQueryData(queryKeys.effectivePermissions(USER_ID), BASE_DATA)

      const { container } = render(
        <QueryClientProvider client={qc}>
          <MemoryRouter>
            <EffectivePermissionsPanel mode="live" userId={USER_ID} />
          </MemoryRouter>
        </QueryClientProvider>,
      )

      // With staleTime:0, data is immediately stale → query starts fetching in background
      await waitFor(() => {
        const dimmed = container.querySelector('.opacity-60')
        expect(dimmed).not.toBeNull()
      }, { timeout: 2000 })
    })
  })

  describe('preview mode', () => {
    it('renders placeholder for preview mode', () => {
      const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
      render(
        <QueryClientProvider client={qc}>
          <MemoryRouter>
            <EffectivePermissionsPanel mode="preview" userId={USER_ID} previewPayload={{}} />
          </MemoryRouter>
        </QueryClientProvider>,
      )
      expect(screen.getByText(/preview mode coming in story 5b-4/i)).toBeInTheDocument()
    })
  })
})
