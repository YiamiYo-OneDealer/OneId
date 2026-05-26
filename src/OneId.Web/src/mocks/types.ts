export type TenantStatus = 'active' | 'suspended'
export type UserStatus = 'active' | 'inactive'

export interface SeatUsage {
  used: number
  max: number | null
}

export interface Tenant {
  id: string
  name: string
  status: TenantStatus
  seatUsage: SeatUsage
  createdAt: string
}

export interface User {
  id: string
  tenantId: string
  name: string
  email: string
  status: UserStatus
  groupIds: string[]
  lastLogin: string | null
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
  permissionIds: string[]
}

export interface RoleSet {
  id: string
  tenantId: string
  name: string
  roleIds: string[]
}

export interface Permission {
  id: string
  domain: string
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

export interface AuditLogEntry {
  id: string
  tenantId: string
  actorUserId: string | null
  actorName: string | null
  actorEmail: string | null
  action: string
  entityType: string
  entityId: string
  payload: Record<string, unknown> | null
  timestamp: string
}

export interface Paginated<T> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}
