import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import { useTenants } from './useTenants'
import { useRoles, useCreateRole } from './useRoles'

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) => (
    React.createElement(QueryClientProvider, { client: qc }, children)
  )
}

describe('useTenants', () => {
  it('returns all 3 mock tenants', async () => {
    const { result } = renderHook(() => useTenants(), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(3)
  })

  it('returns tenants with correct shape', async () => {
    const { result } = renderHook(() => useTenants(), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    const tenant = result.current.data![0]
    expect(tenant).toMatchObject({
      id: expect.any(String),
      name: expect.any(String),
      status: expect.stringMatching(/^(active|suspended)$/),
      seatUsage: { used: expect.any(Number) },
      createdAt: expect.any(String),
    })
  })
})

describe('useRoles', () => {
  it('returns only roles for the specified tenant', async () => {
    const { result } = renderHook(() => useRoles('acme-corp'), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    const roles = result.current.data!
    expect(roles.length).toBeGreaterThan(0)
    expect(roles.every((r) => r.tenantId === 'acme-corp')).toBe(true)
  })

  it('does not return roles from other tenants', async () => {
    const { result } = renderHook(() => useRoles('betatech'), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data!.every((r) => r.tenantId === 'betatech')).toBe(true)
  })
})

describe('useCreateRole', () => {
  it('adds the new role to the list after mutation', async () => {
    const wrapper = createWrapper()
    const listHook = renderHook(() => useRoles('acme-corp'), { wrapper })
    const createHook = renderHook(() => useCreateRole('acme-corp'), { wrapper })

    await waitFor(() => expect(listHook.result.current.isSuccess).toBe(true))
    const initialCount = listHook.result.current.data!.length

    createHook.result.current.mutate({
      name: 'New Test Role',
      permissionIds: ['od.users.read'],
    })

    await waitFor(() => expect(createHook.result.current.isSuccess).toBe(true))
    await waitFor(() => expect(listHook.result.current.data!.length).toBe(initialCount + 1))

    const newRole = listHook.result.current.data!.find((r) => r.name === 'New Test Role')
    expect(newRole).toBeDefined()
    expect(newRole!.permissionIds).toEqual(['od.users.read'])
  })
})
