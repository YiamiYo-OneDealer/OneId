interface OneIdLogoProps {
  collapsed?: boolean
}

export function OneIdLogo({ collapsed }: OneIdLogoProps) {
  return (
    <div className="flex items-center gap-2.5">
      <svg
        width="28"
        height="28"
        viewBox="0 0 32 32"
        xmlns="http://www.w3.org/2000/svg"
        aria-hidden="true"
        className="shrink-0"
      >
        <circle cx="16" cy="11.5" r="7" fill="#00B5EC" />
        <circle cx="16" cy="11.5" r="3.5" style={{ fill: 'hsl(var(--sidebar))' }} />
        <rect x="13.75" y="20.5" width="4.5" height="8" rx="2" fill="#00B5EC" />
      </svg>
      {!collapsed && (
        <span
          className="font-sans text-base font-semibold tracking-tight"
          aria-label="OneId"
        >
          <span className="text-foreground">One</span>
          <span className="text-primary">Id</span>
        </span>
      )}
    </div>
  )
}
