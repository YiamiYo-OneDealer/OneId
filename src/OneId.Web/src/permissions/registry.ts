export interface PermissionGroup {
  domain: string
  label: string
  permissions: {
    id: string
    label: string
    description?: string
  }[]
}

export const PERMISSION_GROUPS: PermissionGroup[] = [
  {
    domain: 'admin.tenants',
    label: 'Tenants',
    permissions: [
      { id: 'od.admin.tenants.view',    label: 'View Tenants' },
      { id: 'od.admin.tenants.create',  label: 'Create Tenants' },
      { id: 'od.admin.tenants.update',  label: 'Update Tenants' },
      { id: 'od.admin.tenants.suspend', label: 'Suspend Tenants' },
    ],
  },
  {
    domain: 'admin.permissions',
    label: 'Permissions',
    permissions: [
      { id: 'od.admin.permissions.view',       label: 'View Permissions' },
      { id: 'od.admin.permissions.create',     label: 'Create Permissions' },
      { id: 'od.admin.permissions.update',     label: 'Update Permissions' },
      { id: 'od.admin.permissions.deactivate', label: 'Deactivate Permissions' },
    ],
  },
  {
    domain: 'admin.licenses',
    label: 'Licenses',
    permissions: [
      { id: 'od.admin.licenses.view',   label: 'View Licenses' },
      { id: 'od.admin.licenses.create', label: 'Create Licenses' },
      { id: 'od.admin.licenses.update', label: 'Update Licenses' },
    ],
  },
  {
    domain: 'admin.idp',
    label: 'Identity Providers',
    permissions: [
      { id: 'od.admin.idp.view',      label: 'View Identity Providers' },
      { id: 'od.admin.idp.configure', label: 'Configure Identity Providers' },
    ],
  },
  {
    domain: 'admin.users',
    label: 'Users',
    permissions: [
      { id: 'od.admin.users.view',       label: 'View Users' },
      { id: 'od.admin.users.create',     label: 'Create Users' },
      { id: 'od.admin.users.update',     label: 'Update Users' },
      { id: 'od.admin.users.deactivate', label: 'Deactivate Users' },
      { id: 'od.admin.users.revoke',     label: 'Revoke User Sessions' },
    ],
  },
  {
    domain: 'admin.roles',
    label: 'Roles',
    permissions: [
      { id: 'od.admin.roles.view',   label: 'View Roles' },
      { id: 'od.admin.roles.create', label: 'Create Roles' },
      { id: 'od.admin.roles.update', label: 'Update Roles' },
      { id: 'od.admin.roles.delete', label: 'Delete Roles' },
    ],
  },
  {
    domain: 'admin.rolesets',
    label: 'Role Sets',
    permissions: [
      { id: 'od.admin.rolesets.view',   label: 'View Role Sets' },
      { id: 'od.admin.rolesets.create', label: 'Create Role Sets' },
      { id: 'od.admin.rolesets.update', label: 'Update Role Sets' },
      { id: 'od.admin.rolesets.delete', label: 'Delete Role Sets' },
    ],
  },
  {
    domain: 'admin.groups',
    label: 'Groups',
    permissions: [
      { id: 'od.admin.groups.view',           label: 'View Groups' },
      { id: 'od.admin.groups.create',         label: 'Create Groups' },
      { id: 'od.admin.groups.update',         label: 'Update Groups' },
      { id: 'od.admin.groups.delete',         label: 'Delete Groups' },
      { id: 'od.admin.groups.members.manage', label: 'Manage Group Members' },
    ],
  },
  {
    domain: 'admin.dimensions',
    label: 'Dimensions',
    permissions: [
      { id: 'od.admin.dimensions.view',   label: 'View Dimensions' },
      { id: 'od.admin.dimensions.assign', label: 'Assign Dimensions' },
    ],
  },
  {
    domain: 'admin.audit',
    label: 'Audit',
    permissions: [
      { id: 'od.admin.audit.view', label: 'View Audit Log' },
    ],
  },
  {
    domain: 'crm',
    label: 'CRM',
    permissions: [
      { id: 'od.crm.read',            label: 'Read CRM Data' },
      { id: 'od.crm.write',           label: 'Write CRM Data' },
      { id: 'od.crm.invoice.create',  label: 'Create Invoices' },
      { id: 'od.crm.invoice.approve', label: 'Approve Invoices' },
    ],
  },
  {
    domain: 'finance',
    label: 'Finance',
    permissions: [
      { id: 'od.finance.read',    label: 'Read Finance Data' },
      { id: 'od.finance.write',   label: 'Write Finance Data' },
      { id: 'od.finance.approve', label: 'Approve Finance Operations' },
    ],
  },
]

// Derived flat lookup — never maintain separately
export const PERMISSION_LABELS: Record<string, string> = Object.fromEntries(
  PERMISSION_GROUPS.flatMap((g) => g.permissions.map((p) => [p.id, p.label]))
)

export function getPermissionLabel(id: string): string {
  return PERMISSION_LABELS[id] ?? id
}
