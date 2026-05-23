import { Sun, Moon } from 'lucide-react'
import { useTheme } from '@/hooks/useTheme'
import { cn } from '@/lib/utils'

interface ThemeToggleProps {
  collapsed?: boolean
}

export function ThemeToggle({ collapsed }: ThemeToggleProps) {
  const { theme, toggle } = useTheme()
  const isDark = theme === 'dark'

  return (
    <button
      onClick={toggle}
      aria-label={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
      className={cn(
        'flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-card hover:text-foreground',
        collapsed && 'justify-center px-2',
      )}
    >
      {isDark ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
      {!collapsed && <span>{isDark ? 'Light mode' : 'Dark mode'}</span>}
    </button>
  )
}
