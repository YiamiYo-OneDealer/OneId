import { NavLink, useMatch, useNavigate } from 'react-router'
import { cn } from '@/lib/utils'
import { useSidebarState } from '@/hooks/useSidebarState'
import { TenantSwitcher } from './TenantSwitcher'
import { OneIdLogo } from './OneIdLogo'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { Separator } from '@/components/ui/separator'
import {
  Users,
  Users2,
  Shield,
  ShieldPlus,
  ScrollText,
  Building2,
  Key,
  CreditCard,
  PanelLeftClose,
  PanelLeftOpen,
  LogOut,
} from 'lucide-react'
import { ThemeToggle } from '@/components/ui/theme-toggle'
import { useAuthStore } from '@/store/auth-store'
import { revokeGrant } from '@/lib/auth'

function parseJwtEmail(token: string | null): string | null {
  if (!token) return null
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')))
    return payload.email ?? null
  } catch {
    return null
  }
}

interface NavConfig {
  to: string
  label: string
  icon: React.ElementType
  exact?: boolean
}

const TENANT_ADMIN_NAV: NavConfig[] = [
  { to: '/tenant/users', label: 'Users', icon: Users },
  { to: '/tenant/groups', label: 'Groups', icon: Users2 },
  { to: '/tenant/role-sets', label: 'Role Sets', icon: ShieldPlus },
  { to: '/tenant/roles', label: 'Roles', icon: Shield },
  { to: '/tenant/audit-log', label: 'Audit Log', icon: ScrollText },
]

const INTERNAL_ADMIN_NAV: NavConfig[] = [
  { to: '/internal/tenants', label: 'Tenants', icon: Building2 },
  { to: '/internal/permissions', label: 'Permissions', icon: Key },
  { to: '/internal/licenses', label: 'Licenses', icon: CreditCard },
  { to: '/internal/audit-log', label: 'Audit Log', icon: ScrollText },
]

interface GlobalNavProps {
  tier: 'internal' | 'tenant'
}

function NavItem({ item, collapsed }: { item: NavConfig; collapsed: boolean }) {
  const match = useMatch({ path: item.to, end: item.exact ?? false })
  const isActive = !!match

  const content = (
    <NavLink
      to={item.to}
      end={item.exact}
      aria-current={isActive ? 'page' : undefined}
      className={cn(
        'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
        collapsed && 'justify-center px-2',
        isActive
          ? 'border-l-2 border-primary bg-card text-foreground'
          : 'text-muted-foreground hover:bg-card hover:text-foreground',
      )}
    >
      <item.icon size={18} aria-hidden="true" />
      {!collapsed && <span>{item.label}</span>}
    </NavLink>
  )

  if (collapsed) {
    return (
      <Tooltip>
        <TooltipTrigger asChild>{content}</TooltipTrigger>
        <TooltipContent side="right">{item.label}</TooltipContent>
      </Tooltip>
    )
  }

  return content
}

export function GlobalNav({ tier }: GlobalNavProps) {
  const { collapsed, toggle } = useSidebarState()
  const { accessToken, refreshToken, clearTokens } = useAuthStore()
  const navigate = useNavigate()
  const navItems = tier === 'internal' ? INTERNAL_ADMIN_NAV : TENANT_ADMIN_NAV
  const userEmail = parseJwtEmail(accessToken)

  async function handleLogout() {
    if (refreshToken) {
      await revokeGrant(refreshToken).catch(() => {})
    }
    clearTokens()
    navigate('/login')
  }

  return (
    <TooltipProvider delayDuration={200}>
      <nav
        aria-label={tier === 'internal' ? 'Internal Admin navigation' : 'Tenant Admin navigation'}
        className={cn(
          'flex flex-col border-r border-border bg-sidebar transition-all duration-200',
          collapsed ? 'w-14' : 'w-60',
        )}
      >
        <div
          className={cn(
            'flex h-14 items-center border-b border-border px-3',
            collapsed && 'justify-center',
          )}
        >
          <OneIdLogo collapsed={collapsed} />
        </div>

        <div className="flex flex-1 flex-col gap-1 p-2">
          {navItems.map((item) => (
            <NavItem key={item.to} item={item} collapsed={collapsed} />
          ))}
        </div>

        <Separator />

        <div className="flex flex-col gap-1 p-2">
          {tier === 'internal' && <TenantSwitcher collapsed={collapsed} />}
          <ThemeToggle collapsed={collapsed} />
          {!collapsed && (
            <div className="px-3 py-2 text-xs text-muted-foreground">Press ⌘K to search</div>
          )}
        </div>

        <div className="border-t border-border p-2 flex flex-col gap-1">
          {collapsed ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  onClick={handleLogout}
                  aria-label="Log out"
                  className="flex w-full items-center justify-center rounded-md p-2 text-muted-foreground hover:bg-card hover:text-foreground"
                >
                  <LogOut size={18} />
                </button>
              </TooltipTrigger>
              <TooltipContent side="right">{userEmail ?? 'Log out'}</TooltipContent>
            </Tooltip>
          ) : (
            <div className="flex items-center gap-2 rounded-md px-3 py-2">
              <div className="flex-1 min-w-0">
                <p className="truncate text-xs text-muted-foreground">{userEmail ?? 'User'}</p>
              </div>
              <button
                onClick={handleLogout}
                aria-label="Log out"
                className="shrink-0 rounded-md p-1 text-muted-foreground hover:bg-card hover:text-foreground"
              >
                <LogOut size={15} />
              </button>
            </div>
          )}
          <button
            onClick={toggle}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            className="flex w-full items-center justify-center rounded-md p-2 text-muted-foreground hover:bg-card hover:text-foreground"
          >
            {collapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
            {!collapsed && <span className="ml-2 text-xs">Collapse</span>}
          </button>
        </div>
      </nav>
    </TooltipProvider>
  )
}
