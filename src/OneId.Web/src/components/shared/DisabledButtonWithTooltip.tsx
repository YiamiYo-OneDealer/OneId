import * as React from 'react'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'

interface DisabledButtonWithTooltipProps {
  tooltip: string
  children: React.ReactElement<React.ButtonHTMLAttributes<HTMLButtonElement>>
}

export function DisabledButtonWithTooltip({
  tooltip,
  children,
}: DisabledButtonWithTooltipProps) {
  const tooltipId = React.useId()

  const disabledChild = React.cloneElement(children, {
    disabled: true,
    'aria-disabled': 'true',
    'aria-describedby': tooltipId,
  } as React.HTMLAttributes<HTMLButtonElement>)

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          {/* span captures pointer events that the disabled button swallows */}
          <span
            tabIndex={0}
            style={{ display: 'inline-block', cursor: 'not-allowed' }}
          >
            {disabledChild}
          </span>
        </TooltipTrigger>
        <TooltipContent id={tooltipId} role="tooltip">
          {tooltip}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}
