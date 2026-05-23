import { NavLink, useMatch } from 'react-router'
import { cn } from '@/lib/utils'
import { useSidebarState } from '@/hooks/useSidebarState'
import { TenantSwitcher } from './TenantSwitcher'
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
} from 'lucide-react'

interface NavConfig {
  to: string
  label: string
  icon: React.ElementType
  exact?: boolean
}

const TENANT_ADMIN_NAV: NavConfig[] = [
  { to: '/tenant/users', label: 'Users', icon: Users },
  { to: '/tenant/groups', label: 'Groups', icon: Users2 },
  { to: '/tenant/roles', label: 'Roles', icon: Shield },
  { to: '/tenant/role-sets', label: 'Role Sets', icon: ShieldPlus },
  { to: '/tenant/audit-log', label: 'Audit Log', icon: ScrollText },
]

const INTERNAL_ADMIN_NAV: NavConfig[] = [
  { to: '/internal', label: 'Tenants', icon: Building2, exact: true },
  { to: '/internal/permissions', label: 'Permissions', icon: Key },
  { to: '/internal/licenses', label: 'Licenses', icon: CreditCard },
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
          ? 'border-l-2 border-primary bg-sidebar text-foreground'
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
  const navItems = tier === 'internal' ? INTERNAL_ADMIN_NAV : TENANT_ADMIN_NAV

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
          {!collapsed && <span className="text-sm font-semibold text-foreground">OneId</span>}
        </div>

        <div className="flex flex-1 flex-col gap-1 p-2">
          {navItems.map((item) => (
            <NavItem key={item.to} item={item} collapsed={collapsed} />
          ))}
        </div>

        <Separator />

        <div className="flex flex-col gap-1 p-2">
          {tier === 'internal' && <TenantSwitcher collapsed={collapsed} />}
          {!collapsed && (
            <div className="px-3 py-2 text-xs text-muted-foreground">Press ⌘K to search</div>
          )}
        </div>

        <div className="border-t border-border p-2">
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
