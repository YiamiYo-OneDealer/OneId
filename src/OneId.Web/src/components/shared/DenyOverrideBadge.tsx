interface DenyOverrideBadgeProps {
  permissionLabel: string
  onReview?: () => void
}

export function DenyOverrideBadge({ permissionLabel, onReview }: DenyOverrideBadgeProps) {
  if (onReview) {
    return (
      <button
        type="button"
        onClick={onReview}
        aria-label={`DENY override on ${permissionLabel} — click to review`}
        className="inline-flex items-center rounded px-1.5 py-0.5 text-[13px] font-semibold leading-none bg-red-950 text-red-500 hover:bg-red-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500 focus-visible:ring-offset-1"
      >
        DENY
      </button>
    )
  }

  return (
    <span
      role="status"
      aria-label={`DENY override on ${permissionLabel}`}
      className="inline-flex items-center rounded px-1.5 py-0.5 text-[13px] font-semibold leading-none bg-red-950 text-red-500"
    >
      DENY
    </span>
  )
}
