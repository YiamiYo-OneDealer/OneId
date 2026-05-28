import * as React from 'react'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import type { DimensionAxis } from '@/features/users/schemas'

const AXIS_SINGULAR: Record<DimensionAxis, string> = {
  Company: 'Company',
  Location: 'Location',
  Branch: 'Branch',
  Make: 'Make',
  MarketSegment: 'Market Segment',
}

const AXIS_PLURAL: Record<DimensionAxis, string> = {
  Company: 'Companies',
  Location: 'Locations',
  Branch: 'Branches',
  Make: 'Makes',
  MarketSegment: 'Market Segments',
}

const AXIS_ORDER: DimensionAxis[] = [
  'Company',
  'Location',
  'Branch',
  'Make',
  'MarketSegment',
]

function isAllValues(values: string[]): boolean {
  return values.length === 1 && values[0] === '*'
}

interface AxisSegmentProps {
  axis: DimensionAxis
  values: string[]
}

function AxisSegment({ axis, values }: AxisSegmentProps) {
  if (isAllValues(values)) {
    const plural = AXIS_PLURAL[axis].toLowerCase()
    return (
      <span>
        {AXIS_PLURAL[axis]}: all {plural}
      </span>
    )
  }

  const label = values.length === 1 ? AXIS_SINGULAR[axis] : AXIS_PLURAL[axis]

  if (values.length <= 3) {
    return (
      <span>
        {label}: {values.join(', ')}
      </span>
    )
  }

  const visible = values.slice(0, 3)
  const hidden = values.slice(3)
  const remaining = hidden.length

  return (
    <span>
      {label}: {visible.join(', ')}{' '}
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <button
              type="button"
              className="text-indigo-400 underline-offset-2 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 rounded"
              aria-label={`Show all ${AXIS_PLURAL[axis]} values`}
            >
              +{remaining} more
            </button>
          </TooltipTrigger>
          <TooltipContent>
            <ul className="text-sm">
              {values.map((v) => (
                <li key={v}>{v}</li>
              ))}
            </ul>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    </span>
  )
}

export interface DimensionalScopeSummaryProps {
  roleName: string
  restrictions: Partial<Record<DimensionAxis, string[]>>
}

export function DimensionalScopeSummary({
  roleName,
  restrictions,
}: DimensionalScopeSummaryProps) {
  const activeAxes = AXIS_ORDER.filter(
    (axis) => (restrictions[axis]?.length ?? 0) > 0,
  )

  if (activeAxes.length === 0) {
    return (
      <p className="text-zinc-300 text-sm">
        {roleName} — no dimensional restrictions (full scope)
      </p>
    )
  }

  const segments = activeAxes.map((axis) => (
    <AxisSegment key={axis} axis={axis} values={restrictions[axis]!} />
  ))

  return (
    <p className="text-zinc-300 text-sm">
      {roleName} — restricted to{' '}
      {segments.reduce<React.ReactNode[]>((acc, seg, i) => {
        if (i === 0) return [seg]
        return [...acc, ' and ', seg]
      }, [])}
    </p>
  )
}
