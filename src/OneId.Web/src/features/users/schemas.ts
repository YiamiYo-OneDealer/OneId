export type ProvenanceNodeType = 'user' | 'group' | 'roleSet' | 'role' | 'permission'

export interface ProvenanceNode {
  nodeType: ProvenanceNodeType
  id: string
  label: string
  /** Navigation URL to the entity's management page. Empty string if no page exists. */
  href: string
}

export interface PermissionEntry {
  id: string
  label: string
  isDenied: boolean
  provenanceChain: ProvenanceNode[]
}

export interface EffectivePermissionsResponse {
  userId: string
  /** ISO 8601 timestamp of when permissions were last resolved */
  resolvedAt: string
  /** False when user has no group assignments at all */
  hasGroupAssignments: boolean
  permissions: PermissionEntry[]
}
