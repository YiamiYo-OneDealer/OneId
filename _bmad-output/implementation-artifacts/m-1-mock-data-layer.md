# Story M-1: Mock Data Layer

Status: done

## Story

As a developer,
I want a fully typed in-memory mock data layer with React Query hooks for all domain entities,
So that Epic 5c admin pages can be built and demoed without any backend dependency.

## Acceptance Criteria

1. **Typed entities** — TypeScript interfaces exist for `Tenant`, `User`, `Group`, `Role`, `RoleSet`, `Permission`, `License`, `TenantAdmin`, `Paginated<T>`. Interfaces match the API response shapes from architecture.md (ISO timestamps, nullable fields, relational ID references).

2. **Fixture data** — Static seed data covers: 3 tenants (active/active/suspended), 5 users per tenant, 4 groups per tenant, 6 roles per tenant, 3 role sets per tenant, 25 global permissions in `od.*` namespace. All relational IDs are internally consistent (e.g. `group.roleIds` reference real role IDs in the same tenant).

3. **In-memory store** — A module-level mutable store holds a deep copy of fixtures and is the single source of truth for all reads and mutations. Mutations (create/update/delete) modify the store synchronously. The store resets on page reload — no persistence needed.

4. **Query hooks** — `useTenants`, `useTenant`, `useUsers`, `useUser`, `useGroups`, `useGroup`, `useRoles`, `useRole`, `useRoleSets`, `useRoleSet`, `usePermissions` exist in `src/queries/hooks/`. Each wraps `useQuery` with the correct `queryKeys` key, returns `{ data, isLoading, isError }`, and simulates 400 ms network latency so DataTable skeleton states are visible.

5. **Mutation hooks** — `useCreateRole`, `useUpdateRole`, `useDeleteRole`, `useCreateRoleSet`, `useUpdateRoleSet`, `useDeleteRoleSet`, `useCreateGroup`, `useUpdateGroup`, `useCreateUser`, `useUpdateUser` exist. Each wraps `useMutation`, writes to the mock store, then calls `queryClient.invalidateQueries` with the correct key so the list re-fetches.

6. **queryKeys extended** — `queryKeys.permissions` and `queryKeys.tenantAdmins(tenantId)` are added to `src/queries/keys.ts`.

7. **Barrel export** — `src/queries/hooks/index.ts` re-exports all hooks so consumers import from `@/queries/hooks`.

8. **Tests** — `useTenants.test.ts` passes: (a) returns all 3 mock tenants, (b) `useRoles(tenantId)` returns only the roles for the specified tenant, (c) `useCreateRole` mutation adds a role and the subsequent `useRoles` query returns the new item.

## Tasks / Subtasks

- [ ] Extend `queryKeys` in `src/queries/keys.ts` (AC: #6)
  - [ ] Add `permissions: () => ['permissions'] as const`
  - [ ] Add `tenantAdmins: (tenantId: string) => ['tenants', tenantId, 'admins'] as const`

- [ ] Create `src/mocks/types.ts` — all TypeScript interfaces (AC: #1)
  - [ ] `Tenant`, `TenantStatus`, `SeatUsage`, `License`
  - [ ] `User`, `UserStatus`
  - [ ] `Group`
  - [ ] `Role`
  - [ ] `RoleSet`
  - [ ] `Permission`
  - [ ] `TenantAdmin`
  - [ ] `Paginated<T>` generic wrapper

- [ ] Create `src/mocks/fixtures.ts` — static seed data (AC: #2)
  - [ ] 3 tenants with internally-consistent IDs
  - [ ] 5 users × 3 tenants (15 users) with `groupIds` referencing real group IDs
  - [ ] 4 groups × 3 tenants with `roleIds` and `roleSetIds` referencing real IDs
  - [ ] 6 roles × 3 tenants with `permissionIds` referencing global permission IDs
  - [ ] 3 role sets × 3 tenants with `roleIds` referencing real role IDs
  - [ ] 25 global permissions in `od.*` namespace covering all domains

- [ ] Create `src/mocks/store.ts` — mutable in-memory store + CRUD operations (AC: #3)
  - [ ] Deep-copy fixtures on module init
  - [ ] Expose typed CRUD methods for each entity
  - [ ] `mockDelay(ms?)` utility function for simulating latency

- [ ] Create query hooks in `src/queries/hooks/` (AC: #4)
  - [ ] `useTenants.ts` — `useTenants()`, `useTenant(tenantId)`
  - [ ] `useUsers.ts` — `useUsers(tenantId)`, `useUser(tenantId, userId)`
  - [ ] `useGroups.ts` — `useGroups(tenantId)`, `useGroup(tenantId, groupId)`
  - [ ] `useRoles.ts` — `useRoles(tenantId)`, `useRole(tenantId, roleId)`
  - [ ] `useRoleSets.ts` — `useRoleSets(tenantId)`, `useRoleSet(tenantId, roleSetId)`
  - [ ] `usePermissions.ts` — `usePermissions()`

- [ ] Create mutation hooks in `src/queries/hooks/` (AC: #5)
  - [ ] Add `useCreateRole`, `useUpdateRole`, `useDeleteRole` to `useRoles.ts`
  - [ ] Add `useCreateRoleSet`, `useUpdateRoleSet`, `useDeleteRoleSet` to `useRoleSets.ts`
  - [ ] Add `useCreateGroup`, `useUpdateGroup` to `useGroups.ts`
  - [ ] Add `useCreateUser`, `useUpdateUser` to `useUsers.ts`

- [ ] Create `src/queries/hooks/index.ts` barrel export (AC: #7)

- [ ] Write `src/queries/hooks/useTenants.test.ts` (AC: #8)
  - [ ] Test: `useTenants()` returns all 3 tenants
  - [ ] Test: `useRoles(tenantId)` returns only roles for that tenant
  - [ ] Test: `useCreateRole` mutation updates the roles list

- [ ] Verify `npm run build`, `npm run lint`, `npm test` pass

---

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**Existing `queryKeys`** — already at `src/queries/keys.ts`. ADD to it, do NOT rewrite it. The file currently has keys for tenants, users, groups, roles, roleSets, effectivePermissions, seatUsage. You need to add `permissions` and `tenantAdmins`.

**No API client exists yet** — `ky` is the planned client (architecture) but not installed. This story uses mock data only. Do NOT install `ky` or create a real API client here.

**No `src/mocks/` directory exists** — create it from scratch.

**No `src/queries/hooks/` directory exists** — create it from scratch.

**`queryClient` singleton** is at `src/lib/query-client.ts` — import it in mutation hooks to call `invalidateQueries`.

**Testing pattern** — tests use `renderHook` from `@testing-library/react`. Hooks that use React Query need a `QueryClientProvider` wrapper. Create a `createWrapper()` helper in the test file — do NOT import from a shared util (none exists yet).

**ESLint design-token rule** — only applies to JSX className. This story has no JSX — no design token concerns.

---

### queryKeys Extension

**File: `src/queries/keys.ts`** — ADD these two entries to the existing `queryKeys` object:

```typescript
permissions: () => ['permissions'] as const,
tenantAdmins: (tenantId: string) => ['tenants', tenantId, 'admins'] as const,
```

Final keys.ts should export an object with: `tenants`, `tenant`, `users`, `user`, `groups`, `group`, `roles`, `role`, `roleSets`, `roleSet`, `effectivePermissions`, `effectivePermissionsPreview`, `seatUsage`, `permissions`, `tenantAdmins`.

---

### TypeScript Interfaces — Full Spec

**File: `src/mocks/types.ts`** — NEW

```typescript
export type TenantStatus = 'active' | 'suspended'
export type UserStatus = 'active' | 'inactive'

export interface SeatUsage {
  used: number
  max: number | null  // null = unlimited
}

export interface Tenant {
  id: string
  name: string
  status: TenantStatus
  seatUsage: SeatUsage
  createdAt: string  // ISO 8601 UTC
}

export interface User {
  id: string
  tenantId: string
  name: string
  email: string
  status: UserStatus
  groupIds: string[]
  lastLogin: string | null  // ISO 8601 UTC or null
  createdAt: string
}

export interface Group {
  id: string
  tenantId: string
  name: string
  memberCount: number
  roleIds: string[]
  roleSetIds: string[]
}

export interface Role {
  id: string
  tenantId: string
  name: string
  permissionIds: string[]  // references Permission.id (global)
}

export interface RoleSet {
  id: string
  tenantId: string
  name: string
  roleIds: string[]  // references Role.id within same tenant
}

export interface Permission {
  id: string          // e.g. 'od.users.read'
  domain: string      // e.g. 'users'
  description: string
  isActive: boolean
}

export interface TenantAdmin {
  userId: string
  tenantId: string
  name: string
  email: string
}

export interface License {
  tenantId: string
  maxSeats: number | null
  effectiveDate: string
}

export interface Paginated<T> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}
```

---

### Fixtures — Full Spec

**File: `src/mocks/fixtures.ts`** — NEW

Use these exact IDs so downstream stories can hardcode navigation links for demos.

**Tenant IDs:**
- Acme Corp: `'acme-corp'`
- BetaTech: `'betatech'`
- Gamma Industries: `'gamma-industries'`

**Permissions (global, 25 entries covering all `od.*` domains):**

| id | domain | description |
|---|---|---|
| `od.tenants.read` | tenants | View tenant list and details |
| `od.tenants.write` | tenants | Create and edit tenants |
| `od.tenants.suspend` | tenants | Suspend and reinstate tenants |
| `od.users.read` | users | View users |
| `od.users.write` | users | Create and edit users |
| `od.users.deactivate` | users | Deactivate user accounts |
| `od.groups.read` | groups | View groups |
| `od.groups.write` | groups | Create and edit groups |
| `od.roles.read` | roles | View roles |
| `od.roles.write` | roles | Create and edit roles |
| `od.role-sets.read` | role-sets | View role sets |
| `od.role-sets.write` | role-sets | Create and edit role sets |
| `od.permissions.read` | permissions | View permission catalog |
| `od.permissions.write` | permissions | Create and deactivate permissions |
| `od.licenses.read` | licenses | View license details |
| `od.licenses.write` | licenses | Create and update licenses |
| `od.audit-log.read` | audit-log | View audit log |
| `od.dimensions.read` | dimensions | View dimension reference lists |
| `od.dimensions.write` | dimensions | Manage dimension values |
| `od.dimension-assignments.read` | dimension-assignments | View user dimension assignments |
| `od.dimension-assignments.write` | dimension-assignments | Assign dimensions to users |
| `od.user-overrides.read` | user-overrides | View permission overrides |
| `od.user-overrides.write` | user-overrides | Create and remove overrides |
| `od.idp.read` | idp | View IDP configuration |
| `od.idp.write` | idp | Configure IDP federation |

**Roles per tenant** — define for Acme Corp; replicate with tenant-scoped IDs for BetaTech/Gamma.

For Acme Corp (prefix `r-acme-`):
1. `r-acme-user-viewer` — User Viewer — `[od.users.read]`
2. `r-acme-user-manager` — User Manager — `[od.users.read, od.users.write, od.users.deactivate]`
3. `r-acme-group-manager` — Group Manager — `[od.groups.read, od.groups.write]`
4. `r-acme-role-manager` — Role Manager — `[od.roles.read, od.roles.write, od.role-sets.read, od.role-sets.write]`
5. `r-acme-auditor` — Auditor — `[od.audit-log.read]`
6. `r-acme-full-admin` — Full Admin — all 25 permission IDs

Repeat with prefix `r-beta-` and `r-gamma-` for the other tenants.

**Role Sets per tenant** (prefix `rs-acme-` etc.):
1. `rs-acme-read-only` — Read Only Bundle — roles: `[r-acme-user-viewer]`
2. `rs-acme-managers` — Managers Bundle — roles: `[r-acme-user-manager, r-acme-group-manager]`
3. `rs-acme-admin` — Admin Bundle — roles: `[r-acme-full-admin]`

**Groups per tenant** (prefix `g-acme-` etc.):
1. `g-acme-administrators` — Administrators — roleIds: `[r-acme-full-admin]`, roleSetIds: `[]`, memberCount: 2
2. `g-acme-hr-team` — HR Team — roleIds: `[r-acme-user-manager]`, roleSetIds: `[rs-acme-read-only]`, memberCount: 3
3. `g-acme-it-staff` — IT Staff — roleIds: `[]`, roleSetIds: `[rs-acme-managers]`, memberCount: 4
4. `g-acme-auditors` — Auditors — roleIds: `[r-acme-auditor]`, roleSetIds: `[]`, memberCount: 1

**Users for Acme Corp** (prefix `u-acme-`):
1. `u-acme-alice` — Alice Johnson — alice@acme.test — active — groupIds: `['g-acme-hr-team', 'g-acme-it-staff']`
2. `u-acme-bob` — Bob Smith — bob@acme.test — active — groupIds: `['g-acme-administrators']`
3. `u-acme-carol` — Carol White — carol@acme.test — active — groupIds: `['g-acme-hr-team']`
4. `u-acme-david` — David Brown — david@acme.test — inactive — groupIds: `['g-acme-it-staff']`
5. `u-acme-eve` — Eve Davis — eve@acme.test — active — groupIds: `['g-acme-auditors']`

Replicate users for BetaTech (`u-beta-`) and Gamma (`u-gamma-`), adapting names/emails and group/role IDs to the tenant prefix.

**Tenant fixture values:**
- Acme Corp: status `'active'`, seatUsage `{ used: 8, max: 25 }`, createdAt `'2025-01-15T09:00:00.000Z'`
- BetaTech: status `'active'`, seatUsage `{ used: 3, max: 10 }`, createdAt `'2025-03-01T14:30:00.000Z'`
- Gamma Industries: status `'suspended'`, seatUsage `{ used: 12, max: 20 }`, createdAt `'2024-11-20T11:00:00.000Z'`

**Export:**
```typescript
export const fixtures = {
  tenants: Tenant[],
  users: User[],
  groups: Group[],
  roles: Role[],
  roleSets: RoleSet[],
  permissions: Permission[],
}
```

---

### Mock Store — Full Spec

**File: `src/mocks/store.ts`** — NEW

```typescript
import type { Tenant, User, Group, Role, RoleSet, Permission } from './types'
import { fixtures } from './fixtures'

// Simulate network latency — makes DataTable loading skeletons visible
export const mockDelay = (ms = 400): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, ms))

// Deep-copy fixtures so mutations don't affect the original arrays
const state = {
  tenants: fixtures.tenants.map((t) => ({ ...t, seatUsage: { ...t.seatUsage } })),
  users: fixtures.users.map((u) => ({ ...u, groupIds: [...u.groupIds] })),
  groups: fixtures.groups.map((g) => ({
    ...g,
    roleIds: [...g.roleIds],
    roleSetIds: [...g.roleSetIds],
  })),
  roles: fixtures.roles.map((r) => ({ ...r, permissionIds: [...r.permissionIds] })),
  roleSets: fixtures.roleSets.map((rs) => ({ ...rs, roleIds: [...rs.roleIds] })),
  permissions: [...fixtures.permissions],
}

export const mockStore = {
  // ── Tenants ──────────────────────────────────────────────────────────────
  getTenants: (): Tenant[] => state.tenants,
  getTenant: (id: string): Tenant | undefined => state.tenants.find((t) => t.id === id),
  createTenant: (data: Omit<Tenant, 'id' | 'createdAt'>): Tenant => {
    const tenant: Tenant = {
      ...data,
      id: `tenant-${Date.now()}`,
      createdAt: new Date().toISOString(),
    }
    state.tenants.push(tenant)
    return tenant
  },
  updateTenant: (id: string, patch: Partial<Tenant>): Tenant => {
    const idx = state.tenants.findIndex((t) => t.id === id)
    if (idx === -1) throw new Error(`Tenant ${id} not found`)
    state.tenants[idx] = { ...state.tenants[idx], ...patch }
    return state.tenants[idx]
  },
  deleteTenant: (id: string): void => {
    state.tenants = state.tenants.filter((t) => t.id !== id)
  },

  // ── Users ────────────────────────────────────────────────────────────────
  getUsers: (tenantId: string): User[] => state.users.filter((u) => u.tenantId === tenantId),
  getUser: (tenantId: string, userId: string): User | undefined =>
    state.users.find((u) => u.id === userId && u.tenantId === tenantId),
  createUser: (data: Omit<User, 'id' | 'createdAt'>): User => {
    const user: User = { ...data, id: `user-${Date.now()}`, createdAt: new Date().toISOString() }
    state.users.push(user)
    return user
  },
  updateUser: (tenantId: string, userId: string, patch: Partial<User>): User => {
    const idx = state.users.findIndex((u) => u.id === userId && u.tenantId === tenantId)
    if (idx === -1) throw new Error(`User ${userId} not found`)
    state.users[idx] = { ...state.users[idx], ...patch }
    return state.users[idx]
  },

  // ── Groups ───────────────────────────────────────────────────────────────
  getGroups: (tenantId: string): Group[] => state.groups.filter((g) => g.tenantId === tenantId),
  getGroup: (tenantId: string, groupId: string): Group | undefined =>
    state.groups.find((g) => g.id === groupId && g.tenantId === tenantId),
  createGroup: (data: Omit<Group, 'id'>): Group => {
    const group: Group = { ...data, id: `group-${Date.now()}` }
    state.groups.push(group)
    return group
  },
  updateGroup: (tenantId: string, groupId: string, patch: Partial<Group>): Group => {
    const idx = state.groups.findIndex((g) => g.id === groupId && g.tenantId === tenantId)
    if (idx === -1) throw new Error(`Group ${groupId} not found`)
    state.groups[idx] = { ...state.groups[idx], ...patch }
    return state.groups[idx]
  },

  // ── Roles ────────────────────────────────────────────────────────────────
  getRoles: (tenantId: string): Role[] => state.roles.filter((r) => r.tenantId === tenantId),
  getRole: (tenantId: string, roleId: string): Role | undefined =>
    state.roles.find((r) => r.id === roleId && r.tenantId === tenantId),
  createRole: (data: Omit<Role, 'id'>): Role => {
    const role: Role = { ...data, id: `role-${Date.now()}` }
    state.roles.push(role)
    return role
  },
  updateRole: (tenantId: string, roleId: string, patch: Partial<Role>): Role => {
    const idx = state.roles.findIndex((r) => r.id === roleId && r.tenantId === tenantId)
    if (idx === -1) throw new Error(`Role ${roleId} not found`)
    state.roles[idx] = { ...state.roles[idx], ...patch }
    return state.roles[idx]
  },
  deleteRole: (tenantId: string, roleId: string): void => {
    // Check if any group uses this role — mock the 409 scenario
    const usedByGroup = state.groups.some(
      (g) => g.tenantId === tenantId && g.roleIds.includes(roleId),
    )
    if (usedByGroup) {
      const groups = state.groups.filter(
        (g) => g.tenantId === tenantId && g.roleIds.includes(roleId),
      )
      throw Object.assign(new Error('Role is assigned to groups'), {
        status: 409,
        assignedTo: groups.map((g) => g.name),
      })
    }
    state.roles = state.roles.filter((r) => !(r.id === roleId && r.tenantId === tenantId))
  },

  // ── RoleSets ─────────────────────────────────────────────────────────────
  getRoleSets: (tenantId: string): RoleSet[] =>
    state.roleSets.filter((rs) => rs.tenantId === tenantId),
  getRoleSet: (tenantId: string, roleSetId: string): RoleSet | undefined =>
    state.roleSets.find((rs) => rs.id === roleSetId && rs.tenantId === tenantId),
  createRoleSet: (data: Omit<RoleSet, 'id'>): RoleSet => {
    const roleSet: RoleSet = { ...data, id: `roleset-${Date.now()}` }
    state.roleSets.push(roleSet)
    return roleSet
  },
  updateRoleSet: (tenantId: string, roleSetId: string, patch: Partial<RoleSet>): RoleSet => {
    const idx = state.roleSets.findIndex(
      (rs) => rs.id === roleSetId && rs.tenantId === tenantId,
    )
    if (idx === -1) throw new Error(`RoleSet ${roleSetId} not found`)
    state.roleSets[idx] = { ...state.roleSets[idx], ...patch }
    return state.roleSets[idx]
  },
  deleteRoleSet: (tenantId: string, roleSetId: string): void => {
    state.roleSets = state.roleSets.filter(
      (rs) => !(rs.id === roleSetId && rs.tenantId === tenantId),
    )
  },

  // ── Permissions ──────────────────────────────────────────────────────────
  getPermissions: (): Permission[] => state.permissions,
}
```

---

### Query Hooks Pattern

All query hooks follow this pattern — zero real API calls:

```typescript
// src/queries/hooks/useRoles.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/queries/keys'
import { mockStore, mockDelay } from '@/mocks/store'
import type { Role } from '@/mocks/types'

export function useRoles(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.roles(tenantId),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getRoles(tenantId)
    },
    enabled: !!tenantId,
  })
}

export function useRole(tenantId: string, roleId: string) {
  return useQuery({
    queryKey: queryKeys.role(tenantId, roleId),
    queryFn: async () => {
      await mockDelay()
      const role = mockStore.getRole(tenantId, roleId)
      if (!role) throw new Error(`Role ${roleId} not found`)
      return role
    },
    enabled: !!(tenantId && roleId),
  })
}

export function useCreateRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (data: Omit<Role, 'id' | 'tenantId'>) => {
      await mockDelay(200)
      return mockStore.createRole({ ...data, tenantId })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
    },
  })
}

export function useUpdateRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ roleId, patch }: { roleId: string; patch: Partial<Role> }) => {
      await mockDelay(200)
      return mockStore.updateRole(tenantId, roleId, patch)
    },
    onSuccess: (_data, { roleId }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.role(tenantId, roleId) })
    },
  })
}

export function useDeleteRole(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (roleId: string) => {
      await mockDelay(200)
      mockStore.deleteRole(tenantId, roleId) // throws with { status: 409, assignedTo: [] } if in use
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles(tenantId) })
    },
  })
}
```

Apply the same pattern for `useTenants.ts`, `useUsers.ts`, `useGroups.ts`, `useRoleSets.ts`, `usePermissions.ts`.

**`usePermissions.ts` — no tenantId, global catalog:**
```typescript
export function usePermissions() {
  return useQuery({
    queryKey: queryKeys.permissions(),
    queryFn: async () => {
      await mockDelay()
      return mockStore.getPermissions()
    },
  })
}
```

---

### Barrel Export

**File: `src/queries/hooks/index.ts`** — NEW

```typescript
export * from './useTenants'
export * from './useUsers'
export * from './useGroups'
export * from './useRoles'
export * from './useRoleSets'
export * from './usePermissions'
```

---

### Test Spec

**File: `src/queries/hooks/useTenants.test.ts`** — NEW

```typescript
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import { useTenants } from './useTenants'
import { useRoles } from './useRoles'
import { useCreateRole } from './useRoles'

// Fresh QueryClient per test — prevents cache bleed between tests
function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
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
```

**Note on `mockDelay` in tests:** The default 400ms delay will slow tests. In tests, vitest uses fake timers — but since we're using `waitFor`, the real timer is fine. Alternatively, set `mockDelay` to 0ms by mocking it: `vi.mock('@/mocks/store', async (importOriginal) => { ... })`. For now, just let `waitFor` handle it — tests will be slightly slow (< 2s total) but correct.

---

### File Structure Summary

```
src/OneId.Web/src/
  mocks/
    types.ts          ← NEW — TypeScript interfaces
    fixtures.ts       ← NEW — static seed data
    store.ts          ← NEW — mutable in-memory store + mockDelay
  queries/
    keys.ts           ← MODIFY — add permissions, tenantAdmins keys
    hooks/
      index.ts        ← NEW — barrel export
      useTenants.ts   ← NEW — query + mutation hooks
      useUsers.ts     ← NEW — query + mutation hooks
      useGroups.ts    ← NEW — query + mutation hooks
      useRoles.ts     ← NEW — query + mutation hooks
      useRoleSets.ts  ← NEW — query + mutation hooks
      usePermissions.ts ← NEW — query hooks only
      useTenants.test.ts ← NEW — tests
```

---

### Important Implementation Notes

1. **`mockDelay` value** — 400ms is the default. Adjust lower (100ms) if demo feels too slow. The delay is intentionally visible to prove `isLoading` skeletons work.

2. **`enabled` guard on queries** — Always use `enabled: !!tenantId` on tenant-scoped queries. `TenantContextLayout` sets `activeTenantId` but there's a brief moment during route transition where `tenantId` is `undefined`.

3. **Mutation input types** — Omit `id` and `tenantId` from create inputs (store generates them). Patch inputs for updates are `Partial<Entity>` minus `id`/`tenantId`.

4. **409 delete guard pattern** — `deleteRole` throws a custom error with `{ status: 409, assignedTo: string[] }`. Future pages will catch this and display the inline error "This role is assigned to: [names]".

5. **No `staleTime` or `gcTime` customization** — use React Query defaults. Mock data never goes stale, but keeping defaults makes swapping to real API easier.

6. **`crypto.randomUUID()`** — available in Vite dev environment (modern browsers + Node 18+). Use it for generated IDs in create mutations.

---

## References

- [queryKeys factory](src/OneId.Web/src/queries/keys.ts) — extend, do not rewrite
- [query-client singleton](src/OneId.Web/src/lib/query-client.ts) — import in mutation hooks
- [5a-2 story](./5a-2-app-shell-routing-tenant-context-and-query-key-factory.md) — TanStack Query v5 patterns
- [5a-4 story](./5a-4-datatable-and-emptystate-components.md) — DataTable/EmptyState (will consume these hooks)
- Architecture: API response shape patterns (paginated, error, timestamps)
- Plan: [sprint-change-proposal-2026-05-23.md](../planning-artifacts/sprint-change-proposal-2026-05-23.md)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Fixed unused `Link` import in `src/routes/internal/index.tsx` (leftover from Epic 5a session)
- All 38 tests pass; build clean

### Completion Notes List

- Mock store is module-level singleton — resets on page reload as designed
- `deleteRole` throws `{ status: 409, assignedTo: string[] }` when role is used by a group
- `mockDelay` defaults to 400ms — DataTable skeleton states are visible during demo
- `useCreateRole` mutation test shares a single wrapper instance to verify cache invalidation works

### File List

- `src/mocks/types.ts` — NEW
- `src/mocks/fixtures.ts` — NEW
- `src/mocks/store.ts` — NEW
- `src/queries/keys.ts` — MODIFIED (added `permissions`, `tenantAdmins`)
- `src/queries/hooks/index.ts` — NEW
- `src/queries/hooks/useTenants.ts` — NEW
- `src/queries/hooks/useUsers.ts` — NEW
- `src/queries/hooks/useGroups.ts` — NEW
- `src/queries/hooks/useRoles.ts` — NEW
- `src/queries/hooks/useRoleSets.ts` — NEW
- `src/queries/hooks/usePermissions.ts` — NEW
- `src/queries/hooks/useTenants.test.ts` — NEW
- `src/routes/internal/index.tsx` — MODIFIED (removed unused `Link` import)

## Change Log

- 2026-05-23: Story created — mock data layer for Epic 5c demo (no backend dependency).
