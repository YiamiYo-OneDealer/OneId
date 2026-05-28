import type { Tenant, User, Group, Role, RoleSet, Permission, AuditLogEntry, Paginated } from './types'
import type { EffectivePermissionsResponse, PreviewPayload } from '@/features/users/schemas'
import { fixtures } from './fixtures'

export const mockDelay = (ms = 400): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, ms))

const state = {
  auditLog: [...fixtures.auditLog],
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
  deleteGroup: (tenantId: string, groupId: string): void => {
    state.groups = state.groups.filter((g) => !(g.id === groupId && g.tenantId === tenantId))
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
    const usedByGroup = state.groups.some(
      (g) => g.tenantId === tenantId && g.roleSetIds.includes(roleSetId),
    )
    if (usedByGroup) {
      const groups = state.groups.filter(
        (g) => g.tenantId === tenantId && g.roleSetIds.includes(roleSetId),
      )
      throw Object.assign(new Error('RoleSet is assigned to groups'), {
        status: 409,
        assignedTo: groups.map((g) => g.name),
      })
    }
    state.roleSets = state.roleSets.filter(
      (rs) => !(rs.id === roleSetId && rs.tenantId === tenantId),
    )
  },

  // ── Permissions ──────────────────────────────────────────────────────────
  getPermissions: (): Permission[] => state.permissions,
  getCurrentUserPermissions: (): string[] => state.permissions.map((p) => p.id),

  // ── Effective Permissions ────────────────────────────────────────────────
  getEffectivePermissions: (userId: string): EffectivePermissionsResponse => {
    const user = state.users.find((u) => u.id === userId)
    if (!user) {
      return {
        userId,
        resolvedAt: new Date().toISOString(),
        hasGroupAssignments: false,
        permissions: [],
      }
    }
    const tenantId = user.tenantId
    const userGroups = state.groups.filter(
      (g) => g.tenantId === tenantId && user.groupIds.includes(g.id),
    )

    if (userGroups.length === 0) {
      return {
        userId,
        resolvedAt: new Date().toISOString(),
        hasGroupAssignments: false,
        permissions: [],
      }
    }

    const permissionMap = new Map<string, { groupName: string; groupId: string; roleId: string; roleName: string; roleSetId?: string; roleSetName?: string }>()

    for (const group of userGroups) {
      // Direct roles
      for (const roleId of group.roleIds) {
        const role = state.roles.find((r) => r.id === roleId)
        if (!role) continue
        for (const permId of role.permissionIds) {
          if (!permissionMap.has(permId)) {
            permissionMap.set(permId, { groupName: group.name, groupId: group.id, roleId, roleName: role.name })
          }
        }
      }
      // Roles via role sets
      for (const rsId of group.roleSetIds) {
        const rs = state.roleSets.find((r) => r.id === rsId)
        if (!rs) continue
        for (const roleId of rs.roleIds) {
          const role = state.roles.find((r) => r.id === roleId)
          if (!role) continue
          for (const permId of role.permissionIds) {
            if (!permissionMap.has(permId)) {
              permissionMap.set(permId, { groupName: group.name, groupId: group.id, roleId, roleName: role.name, roleSetId: rs.id, roleSetName: rs.name })
            }
          }
        }
      }
    }

    // Build the permissions list; mark od.users.deactivate as DENY-overridden for demo purposes
    const DEMO_DENY_IDS = new Set(['od.users.deactivate'])

    const permissions = Array.from(permissionMap.entries()).map(([permId, source]) => {
      const chain = [
        { nodeType: 'user' as const, id: userId, label: user.name, href: '' },
        { nodeType: 'group' as const, id: source.groupId, label: source.groupName, href: `/tenant/groups/${source.groupId}` },
        ...(source.roleSetId ? [{ nodeType: 'roleSet' as const, id: source.roleSetId, label: source.roleSetName!, href: `/tenant/role-sets/${source.roleSetId}` }] : []),
        { nodeType: 'role' as const, id: source.roleId, label: source.roleName, href: `/tenant/roles/${source.roleId}` },
        { nodeType: 'permission' as const, id: permId, label: permId, href: '' },
      ]
      return {
        id: permId,
        label: permId,
        isDenied: DEMO_DENY_IDS.has(permId),
        provenanceChain: chain,
      }
    })

    return {
      userId,
      resolvedAt: new Date().toISOString(),
      hasGroupAssignments: true,
      permissions,
    }
  },

  getEffectivePermissionsPreview: (userId: string, payload: PreviewPayload): EffectivePermissionsResponse => {
    // Build a simulated preview response based on the payload
    const hasGroupIds = (payload.groupIds?.length ?? 0) > 0

    if (!hasGroupIds) {
      return {
        userId,
        resolvedAt: new Date().toISOString(),
        hasGroupAssignments: false,
        permissions: [],
      }
    }

    // Get the current live permissions as a baseline
    const live = mockStore.getEffectivePermissions(userId)
    const liveIds = new Set(live.permissions.map((p) => p.id))

    // Simulate: first permission becomes 'added' (new from payload), one becomes 'removed'
    const permissions = live.permissions.map((p, i) => ({
      ...p,
      diffStatus: (i === 0 ? 'removed' : 'unchanged') as 'added' | 'removed' | 'unchanged',
    }))

    // Add a synthetic 'added' permission to demonstrate diff
    const addedPerm: EffectivePermissionsResponse['permissions'][number] = {
      id: 'od.reports.export',
      label: 'od.reports.export',
      isDenied: false,
      provenanceChain: [],
      diffStatus: 'added',
    }
    if (!liveIds.has('od.reports.export')) {
      permissions.push(addedPerm)
    }

    return {
      userId,
      resolvedAt: new Date().toISOString(),
      hasGroupAssignments: true,
      permissions,
    }
  },

  // ── Audit Log ────────────────────────────────────────────────────────────
  getAuditLog: (
    tenantId: string | null,
    pageIndex: number,
    pageSize: number,
  ): Paginated<AuditLogEntry> => {
    const filtered = tenantId
      ? state.auditLog.filter((e) => e.tenantId === tenantId)
      : [...state.auditLog]
    filtered.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
    const start = pageIndex * pageSize
    return {
      items: filtered.slice(start, start + pageSize),
      totalCount: filtered.length,
      pageIndex,
      pageSize,
    }
  },
}
