import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Inbox, SearchX, AlertCircle } from 'lucide-react'

type EmptyStateVariant = 'no-data' | 'no-results' | 'error' | 'empty'

const VARIANT_DEFAULTS: Record<
  EmptyStateVariant,
  { icon: React.ElementType; title: string; description: string }
> = {
  'no-data': {
    icon: Inbox,
    title: 'Nothing here yet',
    description: 'Add your first item to get started.',
  },
  'no-results': {
    icon: SearchX,
    title: 'No results found',
    description: 'Try adjusting your search or filters.',
  },
  error: {
    icon: AlertCircle,
    title: 'Something went wrong',
    description: 'An error occurred while loading data. Try again.',
  },
  empty: {
    icon: Inbox,
    title: 'Nothing to show',
    description: '',
  },
}

interface EmptyStateProps {
  variant?: EmptyStateVariant
  title?: string
  description?: string
  icon?: React.ElementType
  action?: {
    label: string
    onClick: () => void
  }
  className?: string
}

export function EmptyState({
  variant = 'empty',
  title,
  description,
  icon,
  action,
  className,
}: EmptyStateProps) {
  const defaults = VARIANT_DEFAULTS[variant]
  const Icon = icon ?? defaults.icon
  const resolvedTitle = title ?? defaults.title
  const resolvedDescription = description ?? defaults.description

  return (
    <div
      role="status"
      className={cn(
        'flex flex-col items-center justify-center gap-3 py-12 text-center',
        className,
      )}
    >
      <Icon size={48} className="text-muted-foreground" aria-hidden="true" />
      <div className="flex flex-col gap-1">
        <p className="text-sm font-semibold text-foreground">{resolvedTitle}</p>
        {resolvedDescription && (
          <p className="text-sm text-muted-foreground">{resolvedDescription}</p>
        )}
      </div>
      {action && (
        <Button size="sm" onClick={action.onClick}>
          {action.label}
        </Button>
      )}
    </div>
  )
}
