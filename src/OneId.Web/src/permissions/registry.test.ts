import { describe, it, expect } from 'vitest'
import { PERMISSION_GROUPS, getPermissionLabel } from './registry'

// Mirror of backend Permissions.cs constants — update when backend adds new constants
const BACKEND_PERMISSION_IDS = [
  'od.admin.tenants.view',
  'od.admin.tenants.create',
  'od.admin.tenants.update',
  'od.admin.tenants.suspend',
  'od.admin.permissions.view',
  'od.admin.permissions.create',
  'od.admin.permissions.update',
  'od.admin.permissions.deactivate',
  'od.admin.licenses.view',
  'od.admin.licenses.create',
  'od.admin.licenses.update',
  'od.admin.idp.view',
  'od.admin.idp.configure',
  'od.admin.users.view',
  'od.admin.users.create',
  'od.admin.users.update',
  'od.admin.users.deactivate',
  'od.admin.users.revoke',
  'od.admin.roles.view',
  'od.admin.roles.create',
  'od.admin.roles.update',
  'od.admin.roles.delete',
  'od.admin.rolesets.view',
  'od.admin.rolesets.create',
  'od.admin.rolesets.update',
  'od.admin.rolesets.delete',
  'od.admin.groups.view',
  'od.admin.groups.create',
  'od.admin.groups.update',
  'od.admin.groups.delete',
  'od.admin.groups.members.manage',
  'od.admin.dimensions.view',
  'od.admin.dimensions.assign',
  'od.admin.audit.view',
  'od.crm.read',
  'od.crm.write',
  'od.crm.invoice.create',
  'od.crm.invoice.approve',
  'od.finance.read',
  'od.finance.write',
  'od.finance.approve',
] as const

describe('Permission Registry sync', () => {
  const allRegisteredIds = new Set(
    PERMISSION_GROUPS.flatMap((g) => g.permissions.map((p) => p.id))
  )

  it('every backend Permission constant has a PERMISSION_GROUPS entry', () => {
    const missing = BACKEND_PERMISSION_IDS.filter((id) => !allRegisteredIds.has(id))
    expect(
      missing,
      `Missing PERMISSION_GROUPS entries for: ${missing.join(', ')}`
    ).toHaveLength(0)
  })

  it('getPermissionLabel returns label for known id', () => {
    expect(getPermissionLabel('od.admin.tenants.view')).toBe('View Tenants')
  })

  it('getPermissionLabel returns raw id as fallback for unknown id', () => {
    const unknownId = 'od.unknown.permission'
    expect(getPermissionLabel(unknownId)).toBe(unknownId)
  })
})
