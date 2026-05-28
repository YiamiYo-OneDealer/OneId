import { TriangleAlert, AlertCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

type SeatState = 'unlimited' | 'normal' | 'warning' | 'limit'

function getSeatState(used: number, max: number | null): SeatState {
  if (max === null) return 'unlimited'
  if (used >= max) return 'limit'
  if (used / max >= 0.8) return 'warning'
  return 'normal'
}

export function isSeatLimitReached(used: number, max: number | null): boolean {
  return max !== null && used >= max
}

export function SeatUsageIndicator({ used, max }: { used: number; max: number | null }) {
  const state = getSeatState(used, max)
  const label = max === null ? `${used} seats used` : `${used} of ${max} seats used`

  return (
    <span
      aria-label={label}
      className={cn(
        'inline-flex items-center gap-1 text-xs font-medium',
        (state === 'normal' || state === 'unlimited') && 'text-zinc-400',
        state === 'warning' && 'text-amber-400',
        state === 'limit' && 'text-red-400',
      )}
    >
      {state === 'warning' && <TriangleAlert className="h-3.5 w-3.5" aria-hidden="true" />}
      {state === 'limit' && <AlertCircle className="h-3.5 w-3.5" aria-hidden="true" />}
      <span>{label}</span>
    </span>
  )
}
