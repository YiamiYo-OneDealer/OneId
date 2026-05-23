export const queryKeys = {
  tenants: () => ['tenants'] as const,
  tenant: (tenantId: string) => ['tenants', tenantId] as const,

  users: (tenantId: string) => ['tenants', tenantId, 'users'] as const,
  user: (tenantId: string, userId: string) => ['tenants', tenantId, 'users', userId] as const,

  groups: (tenantId: string) => ['tenants', tenantId, 'groups'] as const,
  group: (tenantId: string, groupId: string) => ['tenants', tenantId, 'groups', groupId] as const,

  roles: (tenantId: string) => ['tenants', tenantId, 'roles'] as const,
  role: (tenantId: string, roleId: string) => ['tenants', tenantId, 'roles', roleId] as const,

  roleSets: (tenantId: string) => ['tenants', tenantId, 'role-sets'] as const,
  roleSet: (tenantId: string, roleSetId: string) =>
    ['tenants', tenantId, 'role-sets', roleSetId] as const,

  effectivePermissions: (userId: string) => ['effectivePermissions', userId] as const,
  effectivePermissionsPreview: () => ['effectivePermissions', 'preview'] as const,

  seatUsage: (tenantId: string) => ['tenants', tenantId, 'seatUsage'] as const,
} as const
