import type { Tenant, User, Group, Role, RoleSet, Permission, AuditLogEntry, Paginated } from './types'
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

  // ── Audit Log ────────────────────────────────────────────────────────────
  getAuditLog: (
    tenantId: string | null,
    pageIndex: number,
    pageSize: number,
  ): Paginated<AuditLogEntry> => {
    const filtered = tenantId
      ? state.auditLog.filter((e) => e.tenantId === tenantId)
      : [...state.auditLog]
    filtered.sort((a, b) => b.timestamp.localeCompare(a.timestamp))
    const start = pageIndex * pageSize
    return {
      items: filtered.slice(start, start + pageSize),
      totalCount: filtered.length,
      pageIndex,
      pageSize,
    }
  },
}
