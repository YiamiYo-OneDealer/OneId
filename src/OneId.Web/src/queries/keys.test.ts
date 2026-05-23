import { QueryClient } from '@tanstack/react-query'
import { queryKeys } from './keys'

describe('TenantSwitchQueryInvalidation', () => {
  let queryClient: QueryClient

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
  })

  it('invalidates all Tenant A cache entries when switching to Tenant B', async () => {
    const tenantA = 'tenant-a'
    const tenantB = 'tenant-b'

    // Seed Tenant A data into cache
    queryClient.setQueryData(queryKeys.users(tenantA), [{ id: 'u1', name: 'Alice' }])
    queryClient.setQueryData(queryKeys.groups(tenantA), [{ id: 'g1', name: 'Admins' }])
    queryClient.setQueryData(queryKeys.seatUsage(tenantA), { used: 5, max: 10 })

    // Seed Tenant B data
    queryClient.setQueryData(queryKeys.users(tenantB), [{ id: 'u2', name: 'Bob' }])

    // Simulate tenant switch: invalidate all Tenant A keys by their shared prefix
    await queryClient.invalidateQueries({ queryKey: ['tenants', tenantA] })

    // Tenant A queries should be invalidated (stale)
    const tenantAUsersQuery = queryClient.getQueryState(queryKeys.users(tenantA))
    expect(tenantAUsersQuery?.isInvalidated).toBe(true)

    const tenantAGroupsQuery = queryClient.getQueryState(queryKeys.groups(tenantA))
    expect(tenantAGroupsQuery?.isInvalidated).toBe(true)

    const tenantASeatUsageQuery = queryClient.getQueryState(queryKeys.seatUsage(tenantA))
    expect(tenantASeatUsageQuery?.isInvalidated).toBe(true)

    // Tenant B data must be untouched
    const tenantBUsers = queryClient.getQueryData(queryKeys.users(tenantB))
    expect(tenantBUsers).toEqual([{ id: 'u2', name: 'Bob' }])

    const tenantBUsersQuery = queryClient.getQueryState(queryKeys.users(tenantB))
    expect(tenantBUsersQuery?.isInvalidated).toBe(false)
  })

  it('queryKeys.users produces correct key shape', () => {
    const key = queryKeys.users('tenant-x')
    expect(key).toEqual(['tenants', 'tenant-x', 'users'])
  })

  it('queryKeys.user produces correct key shape', () => {
    const key = queryKeys.user('tenant-x', 'user-1')
    expect(key).toEqual(['tenants', 'tenant-x', 'users', 'user-1'])
  })

  it('tenant-scoped keys all nest under ["tenants", tenantId] for bulk invalidation', () => {
    const tenantId = 'tenant-z'
    expect(queryKeys.users(tenantId)[1]).toBe(tenantId)
    expect(queryKeys.groups(tenantId)[1]).toBe(tenantId)
    expect(queryKeys.roles(tenantId)[1]).toBe(tenantId)
    expect(queryKeys.roleSets(tenantId)[1]).toBe(tenantId)
    expect(queryKeys.seatUsage(tenantId)[1]).toBe(tenantId)
    expect(queryKeys.tenant(tenantId)[1]).toBe(tenantId)
    expect(queryKeys.user(tenantId, 'uid')[1]).toBe(tenantId)
  })

  it('effectivePermissions keys are not tenant-scoped (user-level)', () => {
    const key = queryKeys.effectivePermissions('user-1')
    expect(key[0]).toBe('effectivePermissions')
    expect(key[1]).toBe('user-1')
  })

  it('invalidating ["tenants", tenantA] does not affect non-tenant-scoped keys', async () => {
    const tenantA = 'tenant-a'
    queryClient.setQueryData(queryKeys.effectivePermissions('user-1'), { permissions: [] })

    await queryClient.invalidateQueries({ queryKey: ['tenants', tenantA] })

    const effectivePermsState = queryClient.getQueryState(
      queryKeys.effectivePermissions('user-1'),
    )
    expect(effectivePermsState?.isInvalidated).toBe(false)
  })
})
