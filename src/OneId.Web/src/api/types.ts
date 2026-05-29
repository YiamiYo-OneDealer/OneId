// API response types — match backend DTOs exactly (ASP.NET Core camelCase serialization).

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UserDto {
  id: string
  email: string
  displayName: string | null
  tenantId: string
  isActive: boolean
  isTenantAdmin: boolean
  createdAt: string
  updatedAt: string
}

export interface TenantDto {
  id: string
  name: string
  status: 'Active' | 'Suspended'
  createdAt: string
  updatedAt: string
  version: number
}

export interface RoleSummaryDto {
  id: string
  name: string
}

export interface RoleSetSummaryDto {
  id: string
  name: string
}

export interface GroupDto {
  id: string
  name: string
  roles: RoleSummaryDto[]
  roleSets: RoleSetSummaryDto[]
  createdAt: string
  updatedAt: string
  version: number
}

export interface RoleDto {
  id: string
  name: string
  permissionIds: string[]
  createdAt: string
  updatedAt: string
  version: number
}

export interface RoleSetDto {
  id: string
  name: string
  roles: RoleSummaryDto[]
  createdAt: string
  updatedAt: string
  version: number
}

export interface PermissionDto {
  id: string
  permissionId: string
  label: string
  status: string
  createdAt: string
  updatedAt: string
  version: number
}

export interface AuditLogDto {
  id: string
  tenantId: string
  actorUserId: string | null
  action: string
  entityType: string
  entityId: string
  payload: string | null
  timestamp: string
}

export interface UserOverrideDto {
  id: string
  permissionId: string
  overrideType: string
  reason: string
  expiresAt: string | null
  createdAt: string
  isExpired: boolean
}

// Mutation request bodies

export interface CreateUserBody {
  email: string
  displayName?: string | null
  password?: string | null
}

export interface UpdateUserBody {
  displayName?: string | null
  email?: string | null
  isActive?: boolean
}

export interface CreateTenantBody {
  name: string
}

export interface UpdateTenantBody {
  name: string
  status?: 'Active' | 'Suspended'
}

export interface CreateGroupBody {
  name: string
  roleIds?: string[]
  roleSetIds?: string[]
}

export interface UpdateGroupBody {
  name: string
  roleIds?: string[]
  roleSetIds?: string[]
  version: number
}

export interface CreateRoleBody {
  name: string
  permissionIds: string[]
}

export interface UpdateRoleBody {
  name: string
  permissionIds: string[]
  version: number
}

export interface CreateRoleSetBody {
  name: string
  roleIds?: string[]
}

export interface UpdateRoleSetBody {
  name: string
  roleIds?: string[]
  version?: number
}

export interface CreateOverrideBody {
  permissionId: string
  overrideType: string
  reason: string
  expiresAt?: string | null
}

export interface CreatePermissionBody {
  permissionId: string
  label: string
}

export interface UpdatePermissionBody {
  label: string
  version: number
}

export interface DimensionValueDto {
  id: string
  axis: string
  value: string
  version: number
}

export interface UserDimensionValueDto {
  id: string
  value: string
}

export interface UserDimensionsDto {
  Company: UserDimensionValueDto[]
  Location: UserDimensionValueDto[]
  Branch: UserDimensionValueDto[]
  Make: UserDimensionValueDto[]
  MarketSegment: UserDimensionValueDto[]
}

export type AllDimensionValuesDto = Record<string, DimensionValueDto[]>

export interface SetUserDimensionsBody {
  valueIds: string[]
}
