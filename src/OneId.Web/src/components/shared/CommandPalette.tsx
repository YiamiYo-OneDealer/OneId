import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '@/components/ui/command'
import { mockStore, mockDelay } from '@/mocks/store'
import type { User, Group, Role } from '@/mocks/types'
import {
  Users,
  Users2,
  Shield,
  ShieldPlus,
  ScrollText,
  Building2,
  Key,
  CreditCard,
} from 'lucide-react'

// ── Action type registry — TypeScript-enforced (UX-DR3) ──────────────────────
// Only these three shapes are permitted. Any other discriminant causes a TS error.

export interface NavigationAction {
  type: 'navigation'
  id: string
  label: string
  group: string
  icon?: React.ElementType
  to: string
}

export interface EntitySearchAction {
  type: 'entity-search'
  id: string
  label: string
  group: string
  icon?: React.ElementType
  entityLabel: string
  search: (query: string) => Promise<EntitySearchResult[]>
}

export interface QuickAction {
  type: 'quick'
  id: string
  label: string
  group: string
  icon?: React.ElementType
  run: () => Promise<void>
}

export interface EntitySearchResult {
  id: string
  label: string
  sublabel?: string
  to: string
}

export type PaletteAction = NavigationAction | EntitySearchAction | QuickAction

// ── Registry builder ─────────────────────────────────────────────────────────

function buildRegistry(
  tier: 'internal' | 'tenant',
  tenantId: string | null,
): PaletteAction[] {
  const navActions: NavigationAction[] =
    tier === 'internal'
      ? [
          { type: 'navigation', id: 'nav-tenants', label: 'Tenants', group: 'Navigation', icon: Building2, to: '/internal/tenants' },
          { type: 'navigation', id: 'nav-permissions', label: 'Permissions', group: 'Navigation', icon: Key, to: '/internal/permissions' },
          { type: 'navigation', id: 'nav-licenses', label: 'Licenses', group: 'Navigation', icon: CreditCard, to: '/internal/licenses' },
          { type: 'navigation', id: 'nav-audit-log', label: 'Audit Log', group: 'Navigation', icon: ScrollText, to: '/internal/audit-log' },
        ]
      : [
          { type: 'navigation', id: 'nav-users', label: 'Users', group: 'Navigation', icon: Users, to: '/tenant/users' },
          { type: 'navigation', id: 'nav-groups', label: 'Groups', group: 'Navigation', icon: Users2, to: '/tenant/groups' },
          { type: 'navigation', id: 'nav-roles', label: 'Roles', group: 'Navigation', icon: Shield, to: '/tenant/roles' },
          { type: 'navigation', id: 'nav-role-sets', label: 'Role Sets', group: 'Navigation', icon: ShieldPlus, to: '/tenant/role-sets' },
          { type: 'navigation', id: 'nav-audit-log-tenant', label: 'Audit Log', group: 'Navigation', icon: ScrollText, to: '/tenant/audit-log' },
        ]

  const searchActions: EntitySearchAction[] = [
    {
      type: 'entity-search',
      id: 'search-users',
      label: 'Search Users',
      group: 'Search',
      icon: Users,
      entityLabel: 'User',
      search: async (query: string): Promise<EntitySearchResult[]> => {
        await mockDelay(200)
        const allUsers: User[] = tenantId
          ? mockStore.getUsers(tenantId)
          : mockStore.getTenants().flatMap((t) => mockStore.getUsers(t.id))
        return allUsers
          .filter(
            (u) =>
              u.name.toLowerCase().includes(query.toLowerCase()) ||
              u.email.toLowerCase().includes(query.toLowerCase()),
          )
          .slice(0, 5)
          .map((u) => ({
            id: u.id,
            label: u.name,
            sublabel: u.email,
            to: tier === 'internal'
              ? `/internal/tenants/${u.tenantId}/users`
              : '/tenant/users',
          }))
      },
    },
    {
      type: 'entity-search',
      id: 'search-groups',
      label: 'Search Groups',
      group: 'Search',
      icon: Users2,
      entityLabel: 'Group',
      search: async (query: string): Promise<EntitySearchResult[]> => {
        await mockDelay(200)
        const allGroups: Group[] = tenantId
          ? mockStore.getGroups(tenantId)
          : mockStore.getTenants().flatMap((t) => mockStore.getGroups(t.id))
        return allGroups
          .filter((g) => g.name.toLowerCase().includes(query.toLowerCase()))
          .slice(0, 5)
          .map((g) => ({
            id: g.id,
            label: g.name,
            to: tier === 'internal'
              ? `/internal/tenants/${g.tenantId}/groups`
              : '/tenant/groups',
          }))
      },
    },
    {
      type: 'entity-search',
      id: 'search-roles',
      label: 'Search Roles',
      group: 'Search',
      icon: Shield,
      entityLabel: 'Role',
      search: async (query: string): Promise<EntitySearchResult[]> => {
        await mockDelay(200)
        const allRoles: Role[] = tenantId
          ? mockStore.getRoles(tenantId)
          : mockStore.getTenants().flatMap((t) => mockStore.getRoles(t.id))
        return allRoles
          .filter((r) => r.name.toLowerCase().includes(query.toLowerCase()))
          .slice(0, 5)
          .map((r) => ({
            id: r.id,
            label: r.name,
            to: tier === 'internal'
              ? `/internal/tenants/${r.tenantId}/roles`
              : '/tenant/roles',
          }))
      },
    },
  ]

  return [...navActions, ...searchActions]
}

// ── Component ─────────────────────────────────────────────────────────────────

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  tier: 'internal' | 'tenant'
  tenantId: string | null
}

export function CommandPalette({ open, onOpenChange, tier, tenantId }: CommandPaletteProps) {
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [entityResults, setEntityResults] = useState<EntitySearchResult[]>([])

  const registry = useMemo(() => buildRegistry(tier, tenantId), [tier, tenantId])

  // Run entity searches — setState only called inside .then() (async callback)
  useEffect(() => {
    let cancelled = false

    const searchActions = registry.filter(
      (a): a is EntitySearchAction => a.type === 'entity-search',
    )

    Promise.all(
      searchActions.map((a) =>
        query.length >= 2 ? a.search(query) : Promise.resolve([]),
      ),
    ).then((results) => {
      if (!cancelled) {
        setEntityResults(results.flat())
      }
    })

    return () => {
      cancelled = true
    }
  }, [query, registry])

  // Reset state when palette closes — called in event handler, not effect body
  function handleOpenChange(newOpen: boolean) {
    if (!newOpen) {
      setQuery('')
      setEntityResults([])
    }
    onOpenChange(newOpen)
  }

  const navActions = registry.filter((a): a is NavigationAction => a.type === 'navigation')

  const filteredNav =
    query.length === 0
      ? navActions
      : navActions.filter((a) => a.label.toLowerCase().includes(query.toLowerCase()))

  function handleSelect(to: string) {
    navigate(to)
    handleOpenChange(false)
  }

  return (
    <CommandDialog open={open} onOpenChange={handleOpenChange} showCloseButton={false}>
      <CommandInput
        placeholder="Search or jump to…"
        value={query}
        onValueChange={setQuery}
      />
      <CommandList>
        <CommandEmpty>{query.length >= 2 && entityResults.length === 0 ? 'Searching…' : 'No results found.'}</CommandEmpty>

        {filteredNav.length > 0 && (
          <CommandGroup heading="Navigation">
            {filteredNav.map((action) => (
              <CommandItem
                key={action.id}
                value={action.label}
                onSelect={() => handleSelect(action.to)}
              >
                {action.icon && (
                  <action.icon size={16} className="shrink-0" aria-hidden="true" />
                )}
                <span>{action.label}</span>
              </CommandItem>
            ))}
          </CommandGroup>
        )}

        {entityResults.length > 0 && (
          <>
            {filteredNav.length > 0 && <CommandSeparator />}
            <CommandGroup heading="Results">
              {entityResults.map((result) => (
                <CommandItem
                  key={result.id}
                  value={`${result.id.split('-')[0]}:${result.label} ${result.sublabel ?? ''}`}
                  onSelect={() => handleSelect(result.to)}
                >
                  <span>{result.label}</span>
                  {result.sublabel && (
                    <span className="ml-2 text-xs text-muted-foreground">{result.sublabel}</span>
                  )}
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}
      </CommandList>
    </CommandDialog>
  )
}
