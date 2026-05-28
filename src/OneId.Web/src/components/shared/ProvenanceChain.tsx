import * as React from 'react'
import { Link } from 'react-router'
import type { ProvenanceNode } from '@/features/users/schemas'
import { cn } from '@/lib/utils'

const NODE_LABELS: Record<string, string> = {
  user: 'User',
  group: 'Group',
  roleSet: 'Role Set',
  role: 'Role',
  permission: 'Permission',
}

interface ProvenanceChainProps {
  chain: ProvenanceNode[]
  collapsed?: boolean
  className?: string
}

function ProvenanceNodeLink({ node }: { node: ProvenanceNode }) {
  if (node.href) {
    return (
      <Link
        to={node.href}
        className="text-primary underline-offset-2 hover:underline"
      >
        {node.label}
      </Link>
    )
  }
  return <span>{node.label}</span>
}

export function ProvenanceChain({ chain, collapsed = false, className }: ProvenanceChainProps) {
  const [expanded, setExpanded] = React.useState(false)

  if (collapsed) {
    // Collapsed: show only the most proximate named source (first group node)
    const groupNode = chain.find((n) => n.nodeType === 'group')
    if (!groupNode) return null
    return (
      <span className={cn('text-xs text-muted-foreground', className)}>
        via {NODE_LABELS[groupNode.nodeType]}:{' '}
        <ProvenanceNodeLink node={groupNode} />
      </span>
    )
  }

  // Expanded mode: full chain
  const LONG_CHAIN_THRESHOLD = 5
  const isLong = chain.length >= LONG_CHAIN_THRESHOLD
  const showAll = !isLong || expanded

  const visibleNodes = showAll
    ? chain
    : [chain[0], chain[chain.length - 1]]

  return (
    <div className={cn('flex flex-wrap items-center gap-1 text-xs text-muted-foreground', className)}>
      {visibleNodes.map((node, idx) => (
        <React.Fragment key={`${node.nodeType}-${node.id}`}>
          {idx > 0 && (
            <span aria-hidden="true" className="text-muted-foreground/60">
              →
            </span>
          )}
          {!showAll && idx === 1 && (
            <>
              <button
                type="button"
                onClick={() => setExpanded(true)}
                className="text-primary text-xs underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                Show full chain ↓
              </button>
              <span aria-hidden="true" className="text-muted-foreground/60">
                →
              </span>
            </>
          )}
          <span className="inline-flex items-center gap-0.5">
            <span className="text-muted-foreground/60">
              {NODE_LABELS[node.nodeType]}:
            </span>{' '}
            <ProvenanceNodeLink node={node} />
          </span>
        </React.Fragment>
      ))}
    </div>
  )
}
