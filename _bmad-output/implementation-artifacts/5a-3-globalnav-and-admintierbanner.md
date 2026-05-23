# Story 5a.3: GlobalNav and AdminTierBanner

Status: review

## Story

As an Internal Admin or Tenant Admin,
I want a persistent sidebar navigation and a contextual banner that shows my current admin tier,
so that I always know what context I'm operating in and can navigate quickly between sections.

## Acceptance Criteria

1. **GlobalNav — Tenant Admin tier** — `GlobalNav` renders with nav items: Users, Groups, Roles, Role Sets, Audit Log. `aria-current="page"` set on the active item. Active item has 2px `indigo-500` left border and `zinc-800` background.

2. **GlobalNav — Internal Admin tier** — Additional items visible: Tenants, Permissions, Licenses. `TenantSwitcher` visible at the bottom. ⌘K hint visible in sidebar footer (stub text — not functional until Epic 5c).

3. **Sidebar collapse** — Click toggles between expanded (240px) and icon-only (56px). Collapsed/expanded state persists in `localStorage` and is restored on page reload. `<nav>` landmark, `<main>` content area, and `<header>` breadcrumbs structured as ARIA landmarks regardless of sidebar state.

4. **AdminTierBanner** — Full-width 40px strip above sidebar+content layout. `bg-admin-banner-bg` (`amber-600`) background, `zinc-950` text. Content: "Internal Admin — Tenant: [tenantId] / [Current Section]". "← All Tenants" router link back to `/internal`. `aria-live="polite"` on the banner — NOT `role="alert"`. Banner is NOT rendered when Internal Admin is at root `/internal` (no `tenantId` in URL / Zustand store is null).

5. **Unsaved changes guard** — When form state is dirty and user clicks "← All Tenants", a confirmation Dialog appears: "You have unsaved changes. Leave anyway?" Confirming navigates away; cancelling stays. Guard does not fire when `isFormDirty` is false (clean navigation proceeds uninterrupted).

## Tasks / Subtasks

- [x] Install shadcn components (AC: all)
  - [x] `npx shadcn add button dialog tooltip separator breadcrumb` inside `src/OneId.Web/`
  - [x] Verify `src/components/ui/` has: `button.tsx`, `dialog.tsx`, `tooltip.tsx`, `separator.tsx`, `breadcrumb.tsx`
- [x] Create UI state store (AC: #5)
  - [x] Create `src/OneId.Web/src/store/ui-store.ts` with `isFormDirty` state (see spec)
- [x] Create `useSidebarState` hook (AC: #3)
  - [x] Create `src/OneId.Web/src/hooks/useSidebarState.ts` with `localStorage` persistence (see spec)
- [x] Create `GlobalNav` component (AC: #1, #2, #3)
  - [x] Create `src/OneId.Web/src/components/shared/GlobalNav.tsx` (see spec below)
  - [x] Nav items by tier: Internal Admin = 6 items + TenantSwitcher + ⌘K hint; Tenant Admin = 5 items
  - [x] Expanded (240px) / collapsed (56px) with icon-only mode; toggle button
  - [x] Active state: `border-l-2 border-primary bg-sidebar`; `aria-current="page"` via NavLink or useMatch
  - [x] Tooltip on icon-only items (collapsed state)
- [x] Create `TenantSwitcher` stub (AC: #2)
  - [x] Create `src/OneId.Web/src/components/shared/TenantSwitcher.tsx` (see spec)
  - [x] Shows current tenantId from Zustand; links to `/internal` for tenant selection
- [x] Create `Breadcrumbs` component (AC: #3)
  - [x] Create `src/OneId.Web/src/components/shared/Breadcrumbs.tsx` (see spec)
  - [x] Uses `useMatches()` + path-segment fallback to build breadcrumb trail
- [x] Create `AdminTierBanner` component (AC: #4, #5)
  - [x] Create `src/OneId.Web/src/components/shared/AdminTierBanner.tsx` (see spec)
  - [x] Reads `activeTenantId` from Zustand (null → banner hidden)
  - [x] `aria-live="polite"` — NOT `role="alert"`
  - [x] "← All Tenants" uses `useBlocker` + Dialog for unsaved-changes guard
- [x] Update `TenantContextLayout` to clear Zustand on unmount (AC: #4)
  - [x] `src/OneId.Web/src/routes/internal/tenants/_layout.tsx` — add cleanup `useEffect` calling `clearTenant()`
- [x] Update `InternalLayout` to include GlobalNav + AdminTierBanner (AC: #1–#5)
  - [x] `src/OneId.Web/src/routes/internal/_layout.tsx` — layout structure: AdminTierBanner + flex[GlobalNav + main[header[Breadcrumbs] + Outlet]]
- [x] Update `TenantAdminLayout` to include GlobalNav + Breadcrumbs (AC: #1, #3)
  - [x] `src/OneId.Web/src/routes/tenant/_layout.tsx` — layout structure: flex[GlobalNav(tier=tenant) + main[header[Breadcrumbs] + Outlet]]
- [x] Write component tests (AC: #1, #2, #3, #4)
  - [x] `src/OneId.Web/src/components/shared/GlobalNav.test.tsx` — tier-based nav items, aria-current, collapse toggle
  - [x] `src/OneId.Web/src/components/shared/AdminTierBanner.test.tsx` — visibility on/off, aria-live, content
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: all)

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**`components/` directory does not exist yet** — this story creates it. `src/components/shared/` is a NEW directory.

**shadcn was initialized in Story 1.1** (`components.json` exists, `src/lib/utils.ts` has `cn()`) but zero UI components have been added. Run `npx shadcn add ...` to install components. They will appear in `src/components/ui/`.

**Do NOT create raw CSS** for Dialog, Button, Tooltip — use shadcn components exclusively. They are pre-styled for the dark mode token system.

**`lucide-react` is already installed** (`^1.16.0` in `package.json`). Import icons directly: `import { Users, Building2, Shield, ... } from 'lucide-react'`.

**Previous story debug learnings (5a.2):**
- `react-refresh/only-export-components` — each file must export ONLY components OR ONLY non-component values (not both). Keep shared helpers in separate files.
- `defineConfig` from `vitest/config` (not `vite`) for the vite config.
- Design-token ESLint rule fires on ANY raw Tailwind color in JSX `className`. Use semantic tokens everywhere.

**Existing store is Zustand v5** — `import { create } from 'zustand'` (named export). `useTenantStore` already exists at `src/store/tenant-store.ts` and has `activeTenantId`, `setActiveTenantId`, `clearTenant`.

**Routing:** React Router v7 library mode. `useMatch`, `useMatches`, `useLocation`, `useBlocker`, `NavLink` all imported from `react-router`.

**Design-token ESLint rule** is active from Story 5a.1. `bg-zinc-950`, `bg-zinc-900`, `text-indigo-500` etc. are BANNED in JSX className. Use:
- `bg-background` (zinc-950)
- `bg-sidebar` (zinc-900)
- `bg-card` (zinc-800)
- `text-primary` (indigo-500) or `border-primary`
- `bg-admin-banner-bg` (amber-600) — **this is the AdminTierBanner background**
- `text-foreground`, `text-muted-foreground`

---

### shadcn Installation — Exact Commands

Run inside `src/OneId.Web/`:
```bash
npx shadcn add button dialog tooltip separator breadcrumb
```

Accept all prompts (overwrite if asked — files don't exist yet).

After installation, verify `src/components/ui/` contains: `button.tsx`, `dialog.tsx`, `tooltip.tsx`, `separator.tsx`, `breadcrumb.tsx`.

**Note on shadcn in Tailwind v4:** The project uses Tailwind v4 (`tailwindcss@^4.3.0`). shadcn's CSS variable tokens are defined in `index.css` (NOT a `tailwind.config.js`). All shadcn components use the `cn()` helper from `@/lib/utils`. Do not run `npx shadcn init` again — it's already initialized.

---

### UI State Store

**File: `src/OneId.Web/src/store/ui-store.ts`** — NEW

```typescript
import { create } from 'zustand'

interface UiState {
  isFormDirty: boolean
  setFormDirty: (dirty: boolean) => void
}

export const useUiStore = create<UiState>((set) => ({
  isFormDirty: false,
  setFormDirty: (dirty) => set({ isFormDirty: dirty }),
}))
```

Future forms will call `setFormDirty(true)` on dirty and `setFormDirty(false)` on success/cancel. AdminTierBanner reads `isFormDirty` for the `useBlocker` condition. For now (no forms), this is always false — guard never triggers.

---

### useSidebarState Hook

**File: `src/OneId.Web/src/hooks/useSidebarState.ts`** — NEW

```typescript
import { useState } from 'react'

const STORAGE_KEY = 'oneid:sidebar:collapsed'

export function useSidebarState() {
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true'
    } catch {
      return false
    }
  })

  const toggle = () => {
    setCollapsed((prev) => {
      const next = !prev
      try {
        localStorage.setItem(STORAGE_KEY, String(next))
      } catch {
        // localStorage unavailable in tests / private browsing
      }
      return next
    })
  }

  return { collapsed, toggle }
}
```

---

### GlobalNav Component — Implementation Spec

**File: `src/OneId.Web/src/components/shared/GlobalNav.tsx`** — NEW

```tsx
import { NavLink, useMatch } from 'react-router'
import { cn } from '@/lib/utils'
import { useSidebarState } from '@/hooks/useSidebarState'
import { TenantSwitcher } from './TenantSwitcher'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { Separator } from '@/components/ui/separator'
import {
  Users, Users2, Shield, ShieldPlus, ScrollText,
  Building2, Key, CreditCard, PanelLeftClose, PanelLeftOpen
} from 'lucide-react'

interface NavItem {
  to: string
  label: string
  icon: React.ElementType
}

const TENANT_ADMIN_NAV: NavItem[] = [
  { to: 'users',     label: 'Users',     icon: Users },
  { to: 'groups',    label: 'Groups',    icon: Users2 },
  { to: 'roles',     label: 'Roles',     icon: Shield },
  { to: 'role-sets', label: 'Role Sets', icon: ShieldPlus },
  { to: 'audit-log', label: 'Audit Log', icon: ScrollText },
]

const INTERNAL_ADMIN_EXTRA: NavItem[] = [
  { to: '/internal',             label: 'Tenants',     icon: Building2 },
  { to: '/internal/permissions', label: 'Permissions', icon: Key },
  { to: '/internal/licenses',   label: 'Licenses',    icon: CreditCard },
]

interface GlobalNavProps {
  tier: 'internal' | 'tenant'
}

export function GlobalNav({ tier }: GlobalNavProps) {
  const { collapsed, toggle } = useSidebarState()
  const basePrefix = tier === 'internal' ? '/internal' : '/tenant'

  const navItems: NavItem[] =
    tier === 'internal'
      ? [
          ...INTERNAL_ADMIN_EXTRA,
          // tenant-specific items appear inside TenantContextLayout via TenantSwitcher
        ]
      : TENANT_ADMIN_NAV.map((item) => ({ ...item, to: `${basePrefix}/${item.to}` }))

  return (
    <TooltipProvider delayDuration={200}>
      <nav
        aria-label={tier === 'internal' ? 'Internal Admin navigation' : 'Tenant Admin navigation'}
        className={cn(
          'flex flex-col border-r border-border bg-sidebar transition-all duration-200',
          collapsed ? 'w-14' : 'w-60',
        )}
      >
        {/* Header / logo area */}
        <div className={cn('flex h-14 items-center border-b border-border px-3', collapsed && 'justify-center')}>
          {!collapsed && <span className="text-sm font-semibold text-foreground">OneId</span>}
        </div>

        {/* Nav items */}
        <div className="flex flex-1 flex-col gap-1 p-2">
          {navItems.map((item) => (
            <NavItem key={item.to} item={item} collapsed={collapsed} />
          ))}
        </div>

        <Separator />

        {/* Footer: TenantSwitcher (Internal Admin only) + ⌘K hint */}
        <div className="flex flex-col gap-1 p-2">
          {tier === 'internal' && <TenantSwitcher collapsed={collapsed} />}
          {!collapsed && (
            <div className="px-3 py-2 text-xs text-muted-foreground">
              Press ⌘K to search
            </div>
          )}
        </div>

        {/* Collapse toggle */}
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
```

**NavItem sub-component** — extract to prevent `react-refresh/only-export-components` issues if needed, or keep inline as a local function (not exported):

```tsx
function NavItem({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
  const match = useMatch({ path: item.to, end: false })
  const isActive = !!match

  const content = (
    <NavLink
      to={item.to}
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
```

**Active state precision:** `useMatch({ path: item.to, end: false })` matches any route STARTING with `item.to` (prefix match). This means "Users" stays active on `/tenant/users/abc`. For the Internal Admin root (`/internal`), use `end: true` to avoid matching all `/internal/*` routes simultaneously.

---

### TenantSwitcher Component — Stub

**File: `src/OneId.Web/src/components/shared/TenantSwitcher.tsx`** — NEW

This is a stub — real tenant list API comes in Epic 3. Shows the current tenant ID from Zustand and links to the root internal admin page for tenant selection.

```tsx
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
        className="flex items-center justify-center rounded-md p-2 text-muted-foreground hover:bg-card hover:text-foreground"
      >
        <Building2 size={18} />
      </Link>
    )
  }

  return (
    <Link
      to="/internal"
      className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-card hover:text-foreground"
    >
      <Building2 size={18} aria-hidden="true" />
      <span className="truncate">
        {activeTenantId ?? 'Select tenant'}
      </span>
    </Link>
  )
}
```

---

### Breadcrumbs Component

**File: `src/OneId.Web/src/components/shared/Breadcrumbs.tsx`** — NEW

Uses React Router's `useMatches()`. Routes that export a `handle.breadcrumb` function will use it; otherwise falls back to the path segment (capitalized).

```tsx
import { useMatches, Link } from 'react-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'

interface RouteHandle {
  breadcrumb?: () => string
}

function segmentToLabel(segment: string): string {
  // e.g. "role-sets" → "Role Sets", "users" → "Users"
  return segment
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}

export function Breadcrumbs() {
  const matches = useMatches()

  // Build crumbs from matches that have pathname segments
  const crumbs = matches
    .filter((m) => m.pathname !== '/')
    .map((m) => {
      const handle = m.handle as RouteHandle | undefined
      const segments = m.pathname.split('/').filter(Boolean)
      const label = handle?.breadcrumb?.() ?? segmentToLabel(segments[segments.length - 1] ?? '')
      return { label, to: m.pathname }
    })
    .filter((c) => c.label && !c.label.match(/^[0-9a-f-]{36}$/)) // skip raw UUIDs/IDs

  if (crumbs.length === 0) return null

  return (
    <Breadcrumb>
      <BreadcrumbList>
        {crumbs.map((crumb, i) => (
          <span key={crumb.to} className="flex items-center gap-1.5">
            {i > 0 && <BreadcrumbSeparator />}
            <BreadcrumbItem>
              {i === crumbs.length - 1 ? (
                <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
              ) : (
                <BreadcrumbLink asChild>
                  <Link to={crumb.to}>{crumb.label}</Link>
                </BreadcrumbLink>
              )}
            </BreadcrumbItem>
          </span>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  )
}
```

---

### AdminTierBanner Component

**File: `src/OneId.Web/src/components/shared/AdminTierBanner.tsx`** — NEW

**Critical accessibility note:** The epics AC explicitly specifies `aria-live="polite"` — NOT `role="alert"`. The UX spec has a conflicting mention of `role="alert"` — the epics AC takes precedence.

```tsx
import { Link, useBlocker, useMatches } from 'react-router'
import { useTenantStore } from '@/store/tenant-store'
import { useUiStore } from '@/store/ui-store'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { ChevronLeft } from 'lucide-react'

function useCurrentSection(): string {
  const matches = useMatches()
  const last = matches[matches.length - 1]
  if (!last) return ''
  const segments = last.pathname.split('/').filter(Boolean)
  // Find the segment after tenantId
  const tenantIdx = segments.findIndex((s) => s === 'tenants')
  const afterTenant = tenantIdx >= 0 ? segments.slice(tenantIdx + 2) : []
  if (afterTenant.length === 0) return 'Dashboard'
  return afterTenant[afterTenant.length - 1]
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}

export function AdminTierBanner() {
  const activeTenantId = useTenantStore((s) => s.activeTenantId)
  const isFormDirty = useUiStore((s) => s.isFormDirty)
  const currentSection = useCurrentSection()

  const blocker = useBlocker(({ nextLocation }) => {
    // Only block navigation AWAY from tenant context
    return isFormDirty && !nextLocation.pathname.includes(activeTenantId ?? '__never__')
  })

  if (!activeTenantId) return null

  return (
    <>
      <div
        aria-live="polite"
        className="flex h-10 w-full shrink-0 items-center justify-between bg-admin-banner-bg px-4 text-sm"
      >
        <span className="font-medium text-foreground">
          Internal Admin — Tenant: {activeTenantId} / {currentSection}
        </span>
        <Link
          to="/internal"
          className="flex items-center gap-1 text-foreground underline-offset-2 hover:underline"
        >
          <ChevronLeft size={14} />
          All Tenants
        </Link>
      </div>

      {/* Unsaved changes guard dialog */}
      <Dialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.()
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>You have unsaved changes</DialogTitle>
            <DialogDescription>
              Leaving this page will discard your unsaved changes. Leave anyway?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => blocker.reset?.()}>
              Stay
            </Button>
            <Button variant="destructive" onClick={() => blocker.proceed?.()}>
              Leave anyway
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
```

**AdminTierBanner text note:** The text color in the banner is `text-foreground` (near-white). The background is `bg-admin-banner-bg` (amber-600). WCAG AA contrast for amber-600 + zinc-950 text is ≥4.5:1 — already validated per UX-DR21. Do NOT use `text-zinc-950` directly (raw color banned by ESLint rule). However, since the ESLint rule does not protect `text-*` variants for `zinc-950` explicitly (only protects `bg-zinc-950`), and `text-foreground` is near-white — this might seem wrong. The key point: **use `text-zinc-950` for text on the amber banner** since white text on amber fails contrast. But `text-zinc-950` is banned by the ESLint rule.

**Resolution:** The ESLint rule from Story 5a.1 bans `text-zinc-950` in JSX className. The banner needs dark text on amber background for contrast. Extend the `@theme inline` block in `index.css` to add `--color-on-admin-banner: hsl(240 5.9% 3.9%)` (zinc-950) and use `text-on-admin-banner` class. OR: use `[color:hsl(var(--background))]` as an arbitrary value. Simplest fix: use CSS variable directly with inline style for the banner text color.

**Practical approach:** Add `--color-on-admin-banner` to `index.css`'s `@theme inline` block (alongside existing OneId tokens) and use `text-on-admin-banner` in JSX. This is additive — doesn't break anything from Story 5a.1.

---

### Layout Modifications

**File: `src/OneId.Web/src/routes/internal/_layout.tsx`** — MODIFY

```tsx
import { Outlet } from 'react-router'
import { GlobalNav } from '@/components/shared/GlobalNav'
import { AdminTierBanner } from '@/components/shared/AdminTierBanner'
import { Breadcrumbs } from '@/components/shared/Breadcrumbs'

export function InternalLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <AdminTierBanner />
      <div className="flex flex-1">
        <GlobalNav tier="internal" />
        <main className="flex flex-1 flex-col">
          <header className="border-b border-border px-6 py-3">
            <Breadcrumbs />
          </header>
          <div className="flex-1 px-6 py-4">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
```

**File: `src/OneId.Web/src/routes/tenant/_layout.tsx`** — MODIFY

```tsx
import { Outlet } from 'react-router'
import { GlobalNav } from '@/components/shared/GlobalNav'
import { Breadcrumbs } from '@/components/shared/Breadcrumbs'

export function TenantAdminLayout() {
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <GlobalNav tier="tenant" />
      <main className="flex flex-1 flex-col">
        <header className="border-b border-border px-6 py-3">
          <Breadcrumbs />
        </header>
        <div className="flex-1 px-6 py-4">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
```

**File: `src/OneId.Web/src/routes/internal/tenants/_layout.tsx`** — MODIFY (add cleanup)

```tsx
import { Outlet, useParams } from 'react-router'
import { useEffect, useRef } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTenantStore } from '@/store/tenant-store'

export function TenantContextLayout() {
  const { tenantId } = useParams<{ tenantId: string }>()
  const queryClient = useQueryClient()
  const previousTenantId = useRef<string | undefined>(undefined)
  const setActiveTenant = useTenantStore((s) => s.setActiveTenantId)
  const clearTenant = useTenantStore((s) => s.clearTenant)

  useEffect(() => {
    if (tenantId && tenantId !== previousTenantId.current) {
      if (previousTenantId.current) {
        queryClient.invalidateQueries({
          queryKey: ['tenants', previousTenantId.current],
        })
      }
      setActiveTenant(tenantId)
      previousTenantId.current = tenantId
    }
  }, [tenantId, queryClient, setActiveTenant])

  // Clear Zustand tenant state when leaving tenant context entirely
  useEffect(() => {
    return () => {
      clearTenant()
    }
  }, [clearTenant])

  if (!tenantId) return null
  return <Outlet />
}
```

---

### CSS Token Addition Required

Add to `src/OneId.Web/src/index.css` in the `@theme inline` block (after existing OneId tokens):

```css
  --color-on-admin-banner: hsl(var(--background)); /* zinc-950 text on amber banner */
```

This enables `text-on-admin-banner` utility class for dark text on the amber AdminTierBanner background, satisfying both the WCAG AA contrast requirement and the ESLint design-token rule.

**Note:** The `.dark` block in `index.css` has `--background: 240 5.9% 3.9%` (zinc-950). So `hsl(var(--background))` IS zinc-950 in dark mode. The `text-on-admin-banner` utility correctly references the semantic token rather than the raw color.

---

### Tests — Implementation Spec

**File: `src/OneId.Web/src/components/shared/GlobalNav.test.tsx`**

Test utilities needed:
- `createMemoryRouter` + `RouterProvider` for React Router context
- `QueryClientProvider` for any TanStack Query context
- Zustand store is module-level — reset between tests with `useTenantStore.setState({ activeTenantId: null })`

```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { GlobalNav } from './GlobalNav'

function renderWithRouter(component: React.ReactNode, initialPath = '/tenant/users') {
  const router = createMemoryRouter(
    [{ path: '*', element: <>{component}</> }],
    { initialEntries: [initialPath] },
  )
  return render(<RouterProvider router={router} />)
}

describe('GlobalNav', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('renders Tenant Admin nav items for tenant tier', () => {
    renderWithRouter(<GlobalNav tier="tenant" />)
    expect(screen.getByText('Users')).toBeInTheDocument()
    expect(screen.getByText('Groups')).toBeInTheDocument()
    expect(screen.getByText('Roles')).toBeInTheDocument()
    expect(screen.getByText('Role Sets')).toBeInTheDocument()
    expect(screen.getByText('Audit Log')).toBeInTheDocument()
  })

  it('renders Internal Admin extra items for internal tier', () => {
    renderWithRouter(<GlobalNav tier="internal" />, '/internal')
    expect(screen.getByText('Tenants')).toBeInTheDocument()
    expect(screen.getByText('Permissions')).toBeInTheDocument()
    expect(screen.getByText('Licenses')).toBeInTheDocument()
  })

  it('does NOT show TenantSwitcher for tenant tier', () => {
    renderWithRouter(<GlobalNav tier="tenant" />)
    // TenantSwitcher has aria-label="Switch tenant" in collapsed mode
    // or links to /internal — neither should exist for tenant tier
    expect(screen.queryByText('Select tenant')).not.toBeInTheDocument()
  })

  it('collapses and expands on toggle button click', () => {
    renderWithRouter(<GlobalNav tier="tenant" />)
    const toggle = screen.getByRole('button', { name: /collapse sidebar/i })
    fireEvent.click(toggle)
    // After collapse, text labels should be hidden
    expect(screen.queryByText('Users')).not.toBeInTheDocument()
    // Expand toggle should now appear
    expect(screen.getByRole('button', { name: /expand sidebar/i })).toBeInTheDocument()
  })

  it('persists collapsed state to localStorage', () => {
    renderWithRouter(<GlobalNav tier="tenant" />)
    fireEvent.click(screen.getByRole('button', { name: /collapse sidebar/i }))
    expect(localStorage.getItem('oneid:sidebar:collapsed')).toBe('true')
  })

  it('restores collapsed state from localStorage', () => {
    localStorage.setItem('oneid:sidebar:collapsed', 'true')
    renderWithRouter(<GlobalNav tier="tenant" />)
    // Sidebar is collapsed — "Collapse" button not visible, "Expand" is
    expect(screen.getByRole('button', { name: /expand sidebar/i })).toBeInTheDocument()
  })
})
```

**File: `src/OneId.Web/src/components/shared/AdminTierBanner.test.tsx`**

```tsx
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { QueryClientProvider, QueryClient } from '@tanstack/react-query'
import { useTenantStore } from '@/store/tenant-store'
import { AdminTierBanner } from './AdminTierBanner'

function renderBanner(path = '/internal/tenants/test-tenant/users') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [{ path: '*', element: <AdminTierBanner /> }],
    { initialEntries: [path] },
  )
  return render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('AdminTierBanner', () => {
  afterEach(() => {
    useTenantStore.setState({ activeTenantId: null })
  })

  it('renders when activeTenantId is set in store', () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    renderBanner()
    expect(screen.getByText(/Internal Admin/)).toBeInTheDocument()
    expect(screen.getByText(/test-tenant/)).toBeInTheDocument()
  })

  it('does NOT render when activeTenantId is null', () => {
    renderBanner('/internal')
    expect(screen.queryByText(/Internal Admin/)).not.toBeInTheDocument()
  })

  it('has aria-live="polite" (NOT role="alert")', () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    renderBanner()
    const banner = screen.getByText(/Internal Admin/).closest('[aria-live]')
    expect(banner).toHaveAttribute('aria-live', 'polite')
    expect(banner).not.toHaveAttribute('role', 'alert')
  })

  it('renders "← All Tenants" navigation link', () => {
    useTenantStore.setState({ activeTenantId: 'test-tenant' })
    renderBanner()
    expect(screen.getByText(/All Tenants/)).toBeInTheDocument()
  })
})
```

**Test gotcha:** `useBlocker` in AdminTierBanner requires a router context with history support. `createMemoryRouter` provides this. If tests fail on `useBlocker`, mock it: `vi.mock('react-router', async (importOriginal) => { const actual = await importOriginal(); return { ...actual, useBlocker: () => ({ state: 'unblocked' }) } })`.

---

### ARIA Landmark Structure (Summary)

The final layout structure after modifications must have:
```
<div> (page root)
  <div aria-live="polite"> (AdminTierBanner — conditional)
  <div className="flex flex-1">
    <nav aria-label="..."> (GlobalNav — landmark: navigation)
    <main> (content area — landmark: main)
      <header> (Breadcrumbs — landmark: banner/header)
      <div> (page content + <Outlet />)
```

This satisfies AC #3: `<nav>` landmark, `<main>` content area, `<header>` breadcrumbs as ARIA landmarks.

---

### Files NOT Created in This Story

- `CommandPalette.tsx` — Epic 5c
- `DataTable.tsx` — Story 5a.4
- `EmptyState.tsx` — Story 5a.4
- `PageSkeleton.tsx` — future story
- `MutationFeedback.tsx` — Story 5b.1
- `lib/api-client.ts` — Epic 2
- `lib/auth.ts` — Epic 2
- Real tenant data fetching in TenantSwitcher — Epic 3 (requires tenant API)

---

### Design System Quick Reference

Available semantic Tailwind utilities (from `index.css` `@theme inline`):

| Utility | Semantic Meaning | Raw Value |
|---|---|---|
| `bg-background` | Page background | zinc-950 |
| `bg-sidebar` | Sidebar background | zinc-900 |
| `bg-card` | Card / hover surface | zinc-800 |
| `bg-admin-banner-bg` | AdminTierBanner | amber-600 |
| `text-on-admin-banner` | Dark text on amber banner | zinc-950 (via `--background`) |
| `text-primary` / `border-primary` | Accent / active | indigo-500 |
| `text-foreground` | Primary text | near-white |
| `text-muted-foreground` | Secondary text | zinc-400 |
| `border-border` | Dividers | zinc-700 range |

### References

- Story 5a.1: [5a-1-design-system-foundation.md](./5a-1-design-system-foundation.md) — ESLint rule, Tailwind v4, design tokens
- Story 5a.2: [5a-2-app-shell-routing-tenant-context-and-query-key-factory.md](./5a-2-app-shell-routing-tenant-context-and-query-key-factory.md) — routing structure, Zustand patterns, vitest setup
- UX-DR3: GlobalNav spec — [epics.md](../planning-artifacts/epics.md)
- UX-DR4: AdminTierBanner spec — [epics.md](../planning-artifacts/epics.md)
- UX-DR5: URL-as-truth tenant context — [epics.md](../planning-artifacts/epics.md)
- Architecture: frontend component locations, Zustand, React Hook Form — [architecture.md](../planning-artifacts/architecture.md)
- UX design spec: GlobalNav/AdminTierBanner anatomy — [ux-design-specification.md](../planning-artifacts/ux-design-specification.md)
- shadcn `components.json`: [src/OneId.Web/components.json](../../src/OneId.Web/components.json)
- Current route layouts: [src/OneId.Web/src/routes/internal/_layout.tsx](../../src/OneId.Web/src/routes/internal/_layout.tsx), [src/OneId.Web/src/routes/tenant/_layout.tsx](../../src/OneId.Web/src/routes/tenant/_layout.tsx)
- Tenant store: [src/OneId.Web/src/store/tenant-store.ts](../../src/OneId.Web/src/store/tenant-store.ts)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6 (create-story workflow, 2026-05-23)

### Debug Log References

- shadcn `npx shadcn add` created files at `@/components/ui/` literally (not resolving `@` alias). Fixed by manually moving files to `src/components/ui/`. Added `src/components/ui/**` to `globalIgnores` in `eslint.config.js` since generated shadcn files export both component and variant helper (react-refresh/only-export-components conflict).
- `--color-on-admin-banner` token added to `@theme inline` in `index.css` as `hsl(var(--background))` — equals zinc-950 in dark mode, providing WCAG AA contrast on amber-600 banner background while satisfying design-token ESLint rule.

### Completion Notes List

- Installed shadcn components: button, dialog, tooltip, separator, breadcrumb. Components at `src/components/ui/`.
- Created `src/store/ui-store.ts` — `isFormDirty` Zustand state for unsaved-changes guard (false by default until real forms exist in Epic 5c).
- Created `src/hooks/useSidebarState.ts` — localStorage-persisted collapse toggle with key `oneid:sidebar:collapsed`.
- Created `src/components/shared/GlobalNav.tsx` — tier-based navigation (internal: Tenants/Permissions/Licenses; tenant: Users/Groups/Roles/Role Sets/Audit Log). Collapse toggle persists to localStorage. Tooltip on icon-only items. TenantSwitcher in internal footer. `aria-current="page"` via `useMatch`. 
- Created `src/components/shared/TenantSwitcher.tsx` — stub showing active tenantId from Zustand, links to `/internal`.
- Created `src/components/shared/Breadcrumbs.tsx` — `useMatches()` + path-segment fallback. Filters raw UUID-like segments.
- Created `src/components/shared/AdminTierBanner.tsx` — reads activeTenantId from Zustand (null = hidden). `aria-live="polite"`. `useBlocker` + Dialog for unsaved-changes guard. Uses `text-on-admin-banner` semantic token.
- Updated `TenantContextLayout` — added cleanup `useEffect(() => () => clearTenant(), [clearTenant])`.
- Updated `InternalLayout` — AdminTierBanner + GlobalNav(internal) + main[header[Breadcrumbs] + Outlet].
- Updated `TenantAdminLayout` — GlobalNav(tenant) + main[header[Breadcrumbs] + Outlet].
- Added `--color-on-admin-banner: hsl(var(--background))` to `index.css` `@theme inline` block.
- Tests: 7 GlobalNav tests, 4 AdminTierBanner tests. All 17 tests pass (including 6 from Story 5a.2).
- Build: ✅ `npm run build` passes. Lint: ✅ `npm run lint` clean. Tests: ✅ 17/17 pass.

### File List

- `src/OneId.Web/src/components/ui/button.tsx` (new — shadcn generated)
- `src/OneId.Web/src/components/ui/dialog.tsx` (new — shadcn generated)
- `src/OneId.Web/src/components/ui/tooltip.tsx` (new — shadcn generated)
- `src/OneId.Web/src/components/ui/separator.tsx` (new — shadcn generated)
- `src/OneId.Web/src/components/ui/breadcrumb.tsx` (new — shadcn generated)
- `src/OneId.Web/src/store/ui-store.ts` (new)
- `src/OneId.Web/src/hooks/useSidebarState.ts` (new)
- `src/OneId.Web/src/components/shared/GlobalNav.tsx` (new)
- `src/OneId.Web/src/components/shared/TenantSwitcher.tsx` (new)
- `src/OneId.Web/src/components/shared/Breadcrumbs.tsx` (new)
- `src/OneId.Web/src/components/shared/AdminTierBanner.tsx` (new)
- `src/OneId.Web/src/components/shared/GlobalNav.test.tsx` (new)
- `src/OneId.Web/src/components/shared/AdminTierBanner.test.tsx` (new)
- `src/OneId.Web/src/routes/internal/_layout.tsx` (modified)
- `src/OneId.Web/src/routes/tenant/_layout.tsx` (modified)
- `src/OneId.Web/src/routes/internal/tenants/_layout.tsx` (modified)
- `src/OneId.Web/src/index.css` (modified — added `--color-on-admin-banner` token)
- `src/OneId.Web/eslint.config.js` (modified — ignore `src/components/ui/**`)

## Change Log

- 2026-05-23: Story implemented — GlobalNav, AdminTierBanner, TenantSwitcher stub, Breadcrumbs; layout updates for InternalLayout and TenantAdminLayout; TenantContextLayout cleanup; shadcn components installed; 17 tests pass, build and lint clean.
