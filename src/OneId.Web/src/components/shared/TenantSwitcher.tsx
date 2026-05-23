import { Link } from 'react-router'
import { useTenantStore } from '@/store/tenant-store'
import { cn } from '@/lib/utils'
import { Building2 } from 'lucide-react'

interface TenantSwitcherProps {
  collapsed: boolean
}

export function TenantSwitcher({ collapsed }: TenantSwitcherProps) {
  const activeTenantId = useTenantStore((s) => s.activeTenantId)

  if (collapsed) {
    return (
      <Link
        to="/internal"
        aria-label="Switch tenant"
        className={cn(
          'flex items-center justify-center rounded-md p-2',
          'text-muted-foreground hover:bg-card hover:text-foreground',
        )}
      >
        <Building2 size={18} />
      </Link>
    )
  }

  return (
    <Link
      to="/internal"
      className={cn(
        'flex items-center gap-2 rounded-md px-3 py-2 text-sm',
        'text-muted-foreground hover:bg-card hover:text-foreground',
      )}
    >
      <Building2 size={18} aria-hidden="true" />
      <span className="truncate">{activeTenantId ?? 'Select tenant'}</span>
    </Link>
  )
}
