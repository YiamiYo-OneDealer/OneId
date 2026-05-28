import { z } from 'zod'

export const dimensionAxisSchema = z.enum([
  'Company',
  'Location',
  'Branch',
  'Make',
  'MarketSegment',
])
export type DimensionAxis = z.infer<typeof dimensionAxisSchema>

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
  diffStatus?: 'added' | 'removed' | 'unchanged'
}

export interface PreviewPayload {
  groupIds?: string[]
  roleSets?: string[]
  overrides?: Array<{ permissionId: string; effect: 'ALLOW' | 'DENY' }>
}

export interface EffectivePermissionsPreviewResponse extends EffectivePermissionsResponse {
  permissions: PermissionEntry[]
}

export interface DenyOverride {
  id: string
  permissionId: string
  overrideType: 'DENY'
  reason?: string
  appliedByName: string
  appliedAt: string
  expiresAt?: string
}

export interface EffectivePermissionsResponse {
  userId: string
  /** ISO 8601 timestamp of when permissions were last resolved */
  resolvedAt: string
  /** False when user has no group assignments at all */
  hasGroupAssignments: boolean
  permissions: PermissionEntry[]
}
