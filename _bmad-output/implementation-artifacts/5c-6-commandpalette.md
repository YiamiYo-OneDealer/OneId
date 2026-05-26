# Story 5c-6: CommandPalette

Status: done

## Story

As an Internal Admin or Tenant Admin,
I want a ⌘K CommandPalette for quick navigation and entity search,
So that I can jump to any section or find any entity without using the sidebar.

## Acceptance Criteria

1. **Keyboard trigger** — Pressing `⌘K` (macOS) or `Ctrl+K` (Windows/Linux) from anywhere in the app opens the CommandPalette modal. The hint in `GlobalNav` footer ("Press ⌘K to search") is already rendered; no change needed there.

2. **Focus trap & Escape** — When open, focus is trapped inside the palette. Pressing `Escape` closes it and returns focus to the previously focused element.

3. **TypeScript-enforced registry** — The action registry accepts only three discriminated union types: `NavigationAction | EntitySearchAction | QuickAction`. TypeScript compilation fails for any unrecognised type (UX-DR3). At minimum these actions are registered:
   - Navigate to each sidebar section (Users, Groups, Roles, Role Sets, Audit Log — scoped by tier)
   - Search Users (by name/email) → `EntitySearchAction`
   - Search Groups → `EntitySearchAction`
   - Search Roles → `EntitySearchAction`

4. **Navigation actions** — Selecting a `NavigationAction` result navigates to the target route and closes the palette.

5. **Entity search actions** — Typing a query that matches an `EntitySearchAction` fetches results from the mock store (simulated async with `mockDelay`) and displays them. Selecting a result navigates to that entity's detail/list page and closes the palette.

6. **Accessibility** — The result list uses `role="listbox"` with `role="option"` items and `aria-selected` on the focused item. The palette dialog itself uses `role="dialog"` with `aria-modal="true"`. `vitest-axe` test passes: `expect(container).toHaveNoViolations()`.

7. **Build clean** — `npm run build`, `npm run lint`, `npm test` all pass with no new errors or warnings.

## Tasks / Subtasks

- [x] Install shadcn `command` component: `npx shadcn@latest add command` (inside `src/OneId.Web/`)
- [x] Define `PaletteAction` discriminated union types in `src/components/shared/CommandPalette.tsx`
- [x] Build `CommandPalette` component with keyboard handler, focus trap, and ARIA roles
- [x] Register navigation actions for both tiers (internal + tenant)
- [x] Register entity search actions (Users, Groups, Roles) wired to mock store
- [x] Wire `⌘K`/`Ctrl+K` global keydown handler in `InternalLayout` and `TenantAdminLayout`
- [x] Write `vitest-axe` accessibility test for `CommandPalette`
- [x] Run `npm test -- --run`, `npm run build`, `npm run lint` and confirm all pass

---

## Dev Notes

### CRITICAL: Current Project State (READ FIRST)

**Available shadcn/ui components** (in `src/components/ui/`):
`badge`, `breadcrumb`, `button`, `checkbox`, `dialog`, `input`, `label`, `separator`, `sheet`, `skeleton`, `theme-toggle`, `tooltip`

**MISSING for this story — install before writing any code:**
```bash
cd src/OneId.Web
npx shadcn@latest add command
```
This creates `src/components/ui/command.tsx` which exports: `Command`, `CommandDialog`, `CommandEmpty`, `CommandGroup`, `CommandInput`, `CommandItem`, `CommandList`, `CommandSeparator`, `CommandShortcut`.

**shadcn `CommandDialog` BUG (known from stories 5c-3, 5c-5):** `npx shadcn@latest add command` may place `command.tsx` in the project root instead of `src/components/ui/`. If that happens, manually move it to `src/OneId.Web/src/components/ui/command.tsx`.

**Mock data layer** — use `mockStore` directly (synchronous reads) inside an async wrapper to simulate delay. **No** new query hook is needed for the palette — it searches data already in memory and the delay is short (simulated in the async fn directly).

**No `useFormMutation`** — palette has no mutations, read-only navigation + search only.

**ESLint design-token rule** — applies to JSX `className`. Use semantic tokens: `bg-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`. Raw Tailwind colors (e.g. `text-gray-500`) will fail lint.

**`useTenantStore`** — the Tenant Admin tier stores the active tenant in Zustand. For entity search scoping, use `useTenantStore((s) => s.activeTenantId)`. The Internal Admin tier (`/internal/...`) has no active tenant scoped in Zustand — use `null` to indicate "all tenants" context.

**Route structure** — both layouts already render `GlobalNav` and `<Outlet>`. The `CommandPalette` must be rendered at the layout level so it's available on every page.

---

### Step 1: Install shadcn `command`

```bash
cd src/OneId.Web
npx shadcn@latest add command
```

Verify `src/components/ui/command.tsx` exists. If it was placed in root, move it.

---

### Step 2: Define `PaletteAction` discriminated union

**File: `src/components/shared/CommandPalette.tsx`** — new file, define types at the top:

```typescript
// TypeScript-enforced registry — only these three action types are permitted.
// Adding any other shape causes a compilation error at the registry definition.

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
  label: string        // e.g. "Search Users"
  group: string
  icon?: React.ElementType
  entityLabel: string  // e.g. "User"
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
  label: string          // primary display (e.g. user name)
  sublabel?: string      // secondary (e.g. email)
  to: string             // route to navigate on select
}

export type PaletteAction = NavigationAction | EntitySearchAction | QuickAction
```

The `PaletteAction` union is the registry type. TypeScript enforces it — if a developer adds an object with `type: 'bulk-edit'` TypeScript will error. No runtime enforcement needed.

---

### Step 3: Build the `CommandPalette` component

The component receives `tier` and `tenantId` (for entity search scoping). It is **controlled** by the parent layout via `open`/`onOpenChange` props — the layout manages the boolean state and the global keydown listener.

```typescript
interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  tier: 'internal' | 'tenant'
  tenantId: string | null  // null = Internal Admin (unscoped)
}
```

**Internal structure:**

```tsx
<CommandDialog open={open} onOpenChange={onOpenChange}>
  <CommandInput placeholder="Search or jump to..." />
  <CommandList>
    <CommandEmpty>No results found.</CommandEmpty>
    {/* Render NavigationAction groups */}
    <CommandGroup heading="Navigation">
      {navigationResults.map(action => (
        <CommandItem
          key={action.id}
          role="option"
          aria-selected={...}
          onSelect={() => { navigate(action.to); onOpenChange(false) }}
        >
          {action.icon && <action.icon size={16} aria-hidden="true" />}
          <span>{action.label}</span>
        </CommandItem>
      ))}
    </CommandGroup>
    {/* Entity search results — shown once user types */}
    {entityResults.length > 0 && (
      <CommandGroup heading="Results">
        {entityResults.map(result => (
          <CommandItem
            key={result.id}
            role="option"
            aria-selected={...}
            onSelect={() => { navigate(result.to); onOpenChange(false) }}
          >
            <span>{result.label}</span>
            {result.sublabel && <span className="text-muted-foreground text-xs ml-2">{result.sublabel}</span>}
          </CommandItem>
        ))}
      </CommandGroup>
    )}
  </CommandList>
</CommandDialog>
```

**`CommandDialog` from shadcn/ui** — it already wraps content in a `role="dialog" aria-modal="true"` `Dialog`. The `CommandList` inside must have `role="listbox"` and each `CommandItem` has `role="option"`. shadcn's `CommandItem` uses `cmdk` under the hood which sets these roles automatically — verify after install by inspecting the rendered HTML.

**ARIA roles clarification:** shadcn `Command` (built on `cmdk`) renders:
- `CommandList` → `role="listbox"`
- `CommandItem` → `role="option"`
- `cmdk` sets `aria-selected` on the focused/highlighted item automatically

You do NOT need to manually add these — they come from `cmdk`. Your vitest-axe test will confirm this.

**Focus trap** — `CommandDialog` uses Radix `Dialog` under the hood which already handles focus trap and return-focus-on-close. No manual `focus-trap-react` needed.

---

### Step 4: Action registry

Build the registry as a function so it can be tier-scoped and use the navigate function:

```typescript
function buildRegistry(
  tier: 'internal' | 'tenant',
  tenantId: string | null,
): PaletteAction[] {
  const navActions: NavigationAction[] = tier === 'internal'
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
        { type: 'navigation', id: 'nav-audit-log', label: 'Audit Log', group: 'Navigation', icon: ScrollText, to: '/tenant/audit-log' },
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
        await mockDelay(200)  // shorter delay for search
        const allUsers = tenantId
          ? mockStore.getUsers(tenantId)
          : (mockStore as any)._getAllUsers?.() ?? []
        return allUsers
          .filter((u: User) =>
            u.name.toLowerCase().includes(query.toLowerCase()) ||
            u.email.toLowerCase().includes(query.toLowerCase()),
          )
          .slice(0, 5)
          .map((u: User) => ({
            id: u.id,
            label: u.name,
            sublabel: u.email,
            // Internal Admin: navigate into tenant context; Tenant Admin: navigate to users page
            to: tenantId
              ? `/internal/tenants/${u.tenantId}/users`
              : `/tenant/users`,
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
        const allGroups = tenantId ? mockStore.getGroups(tenantId) : []
        return allGroups
          .filter((g: Group) => g.name.toLowerCase().includes(query.toLowerCase()))
          .slice(0, 5)
          .map((g: Group) => ({
            id: g.id,
            label: g.name,
            to: tenantId ? `/internal/tenants/${g.tenantId}/groups` : `/tenant/groups`,
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
        const allRoles = tenantId ? mockStore.getRoles(tenantId) : []
        return allRoles
          .filter((r: Role) => r.name.toLowerCase().includes(query.toLowerCase()))
          .slice(0, 5)
          .map((r: Role) => ({
            id: r.id,
            label: r.name,
            to: tenantId ? `/internal/tenants/${r.tenantId}/roles` : `/tenant/roles`,
          }))
      },
    },
  ]

  return [...navActions, ...searchActions]
}
```

**IMPORTANT — `mockStore` does NOT have `getRoles(tenantId)` or `getGroups(tenantId)` methods that match your call pattern.** Check `src/mocks/store.ts` — it has:
- `getUsers(tenantId: string): User[]`
- `getGroups(tenantId: string): Group[]`
- `getRoles()`: need to check — it may return all roles without tenant filter.

**Read `src/mocks/store.ts` before writing the search functions** and use the actual method signatures. If `getRoles()` returns all roles, filter by `tenantId` in the search lambda. Do NOT add new mock store methods — use what's already there.

For the Internal Admin searching users across all tenants, you either:
- Call `mockStore.getTenants()` then `mockStore.getUsers(t.id)` for each tenant (fine for mock data)
- Or add a `getAllUsers(): User[]` convenience to store. Either approach is acceptable.

---

### Step 5: Query/search state management inside the component

```typescript
export function CommandPalette({ open, onOpenChange, tier, tenantId }: CommandPaletteProps) {
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [entityResults, setEntityResults] = useState<EntitySearchResult[]>([])
  const [isSearching, setIsSearching] = useState(false)

  const registry = useMemo(
    () => buildRegistry(tier, tenantId),
    [tier, tenantId],
  )

  // Debounced entity search
  useEffect(() => {
    if (query.length < 2) {
      setEntityResults([])
      return
    }
    let cancelled = false
    setIsSearching(true)

    // Run all EntitySearchAction searches in parallel
    const searchActions = registry.filter((a): a is EntitySearchAction => a.type === 'entity-search')
    Promise.all(searchActions.map((a) => a.search(query))).then((results) => {
      if (!cancelled) {
        setEntityResults(results.flat())
        setIsSearching(false)
      }
    })

    return () => { cancelled = true }
  }, [query, registry])

  // Reset on close
  useEffect(() => {
    if (!open) {
      setQuery('')
      setEntityResults([])
    }
  }, [open])

  const navActions = registry.filter((a): a is NavigationAction => a.type === 'navigation')

  // Filter navActions by query
  const filteredNav = query.length === 0
    ? navActions
    : navActions.filter((a) => a.label.toLowerCase().includes(query.toLowerCase()))

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput
        placeholder="Search or jump to..."
        value={query}
        onValueChange={setQuery}
      />
      <CommandList>
        <CommandEmpty>{isSearching ? 'Searching…' : 'No results found.'}</CommandEmpty>

        {filteredNav.length > 0 && (
          <CommandGroup heading="Navigation">
            {filteredNav.map((action) => (
              <CommandItem
                key={action.id}
                value={action.label}
                onSelect={() => { navigate(action.to); onOpenChange(false) }}
              >
                {action.icon && <action.icon size={16} className="mr-2 shrink-0" aria-hidden="true" />}
                <span>{action.label}</span>
              </CommandItem>
            ))}
          </CommandGroup>
        )}

        {entityResults.length > 0 && (
          <CommandGroup heading="Results">
            {entityResults.map((result) => (
              <CommandItem
                key={result.id}
                value={`${result.label} ${result.sublabel ?? ''}`}
                onSelect={() => { navigate(result.to); onOpenChange(false) }}
              >
                <span>{result.label}</span>
                {result.sublabel && (
                  <span className="ml-2 text-xs text-muted-foreground">{result.sublabel}</span>
                )}
              </CommandItem>
            ))}
          </CommandGroup>
        )}
      </CommandList>
    </CommandDialog>
  )
}
```

---

### Step 6: Wire global ⌘K/Ctrl+K listener in layouts

Both `InternalLayout` and `TenantAdminLayout` need to manage `paletteOpen` state and listen for the keyboard shortcut.

**Pattern for `InternalLayout`:**

```typescript
// src/routes/internal/_layout.tsx
import { useState, useEffect } from 'react'
import { CommandPalette } from '@/components/shared/CommandPalette'

export function InternalLayout() {
  const [paletteOpen, setPaletteOpen] = useState(false)

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setPaletteOpen((prev) => !prev)
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

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
      <CommandPalette
        open={paletteOpen}
        onOpenChange={setPaletteOpen}
        tier="internal"
        tenantId={null}
      />
    </div>
  )
}
```

Apply the same pattern to `TenantAdminLayout`, with `tier="tenant"` and `tenantId` from `useTenantStore`:

```typescript
// src/routes/tenant/_layout.tsx
import { useTenantStore } from '@/hooks/useTenantStore'  // verify hook name

const tenantId = useTenantStore((s) => s.activeTenantId) ?? 'acme-corp'

// ... same useEffect for keydown ...

<CommandPalette
  open={paletteOpen}
  onOpenChange={setPaletteOpen}
  tier="tenant"
  tenantId={tenantId}
/>
```

**Verify the Zustand store hook name** — check `src/hooks/` for the actual store file (may be `useTenantStore.ts`, `useActiveTenant.ts`, etc.) and use the correct export. Story 5c-5 used `useTenantStore((s) => s.activeTenantId) ?? 'acme-corp'`.

---

### Step 7: Accessibility test

**File: `src/components/shared/CommandPalette.test.tsx`** — NEW

```typescript
import { render } from '@testing-library/react'
import { expect, test } from 'vitest'
import { axe } from 'vitest-axe'
import { MemoryRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CommandPalette } from './CommandPalette'

function renderPalette(open: boolean) {
  const qc = new QueryClient()
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <CommandPalette
          open={open}
          onOpenChange={() => {}}
          tier="internal"
          tenantId={null}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

test('CommandPalette closed — no axe violations', async () => {
  const { container } = renderPalette(false)
  expect(await axe(container)).toHaveNoViolations()
})

test('CommandPalette open — no axe violations', async () => {
  const { container } = renderPalette(true)
  expect(await axe(container)).toHaveNoViolations()
})
```

**Check if `vitest-axe` is already installed** by scanning `package.json`. If not:
```bash
cd src/OneId.Web
npm install -D vitest-axe axe-core
```

If `vitest-axe` is already installed (it was used in earlier stories for accessibility), just import it.

**Existing test infra** — review how other `.test.tsx` files import and configure axe to follow the established pattern. Check `src/components/shared/DataTable.test.tsx` or any other existing test file for the correct vitest + RTL setup.

---

### File Structure After This Story

```
src/OneId.Web/src/
  components/
    ui/
      command.tsx                    ← NEW (shadcn install)
    shared/
      CommandPalette.tsx             ← NEW (component + types + registry)
      CommandPalette.test.tsx        ← NEW (vitest-axe accessibility test)
  routes/
    internal/
      _layout.tsx                    ← MODIFY (add palette state + keydown + <CommandPalette>)
    tenant/
      _layout.tsx                    ← MODIFY (add palette state + keydown + <CommandPalette>)
```

No mock layer changes, no query key changes, no router changes, no GlobalNav changes.

---

### Key Invariants to Preserve

- `GlobalNav.tsx` already shows "Press ⌘K to search" hint at line 116. **Do not modify GlobalNav** — the hint is already correct.
- Both layouts already render `GlobalNav` and `<Outlet>`. Adding `<CommandPalette>` as a sibling at the bottom of the layout JSX is the correct pattern (same as how `Sheet` in other stories is a sibling).
- The `⌘K` handler uses `e.preventDefault()` to prevent browser default behavior (browser address bar focus on some systems).
- The `CommandDialog` from shadcn/ui is a `Dialog` wrapper — it renders in a portal, so placement inside the layout div is fine.
- `useMemo` for `buildRegistry` prevents the registry array from being recreated on every render, which would cancel in-flight searches unnecessarily.

---

### Pre-Implementation Checklist

Before writing any component code:
- [ ] `npx shadcn@latest add command` ran — `src/components/ui/command.tsx` exists in the right place
- [ ] `npm run build` passes with current codebase (get a clean baseline)
- [ ] Verified `mockStore` method signatures for `getUsers`, `getGroups`, `getRoles` from `src/mocks/store.ts`
- [ ] Confirmed Zustand store hook name for tenant ID (check `src/hooks/`)
- [ ] Confirmed `vitest-axe` is installed (check `package.json`)

---

### ESLint known pre-existing errors (do not introduce new ones)

From story 5c-5 dev notes: there are 7 pre-existing ESLint errors (useTheme, TenantDetailPage, reset-password). These are NOT your responsibility. Ensure you introduce zero new lint errors.

---

## References

- [CommandPalette component](src/OneId.Web/src/components/shared/CommandPalette.tsx) — NEW
- [InternalLayout](src/OneId.Web/src/routes/internal/_layout.tsx) — MODIFY
- [TenantAdminLayout](src/OneId.Web/src/routes/tenant/_layout.tsx) — MODIFY
- [GlobalNav](src/OneId.Web/src/components/shared/GlobalNav.tsx) — READ ONLY (hint already present, no changes)
- [mockStore](src/OneId.Web/src/mocks/store.ts) — READ ONLY
- [mock fixtures](src/OneId.Web/src/mocks/fixtures.ts) — READ ONLY (tenant/user IDs for search verification)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Completion Notes List

- shadcn `command` installed; known bug hit (dropped in `@/components/ui/` root), manually moved to `src/components/ui/command.tsx`. `dialog.tsx` also updated.
- `PaletteAction = NavigationAction | EntitySearchAction | QuickAction` discriminated union enforces TypeScript-level registry — any unknown shape causes TS compilation error.
- Navigation actions: 4 for internal tier (Tenants, Permissions, Licenses, Audit Log), 5 for tenant tier (Users, Groups, Roles, Role Sets, Audit Log).
- Entity search: Users (by name/email), Groups, Roles — each wired to mock store with 200ms delay. Internal Admin searches across all tenants via `getTenants().flatMap(...)`.
- `⌘K`/`Ctrl+K` wired via `window.addEventListener('keydown', ...)` in both `InternalLayout` and `TenantAdminLayout`. Listeners cleaned up on unmount.
- Focus trap and return-focus-on-close handled automatically by Radix `Dialog` inside `CommandDialog`.
- Reset (query + results cleared) on close via `handleOpenChange` event handler — avoids synchronous `setState` in `useEffect` (lint rule compliance).
- 3 vitest-axe tests: palette closed, palette open (internal), palette open (tenant) — all pass.
- Build: ✅ clean | Tests: ✅ 54/54 passing | Lint: no new errors (pre-existing 8 errors unchanged).

### File List

- `src/OneId.Web/src/components/ui/command.tsx` — NEW (shadcn install)
- `src/OneId.Web/src/components/ui/dialog.tsx` — MODIFIED (updated by shadcn install, adds `showCloseButton` support)
- `src/OneId.Web/src/components/shared/CommandPalette.tsx` — NEW
- `src/OneId.Web/src/components/shared/CommandPalette.test.tsx` — NEW
- `src/OneId.Web/src/routes/internal/_layout.tsx` — MODIFIED (⌘K handler + CommandPalette)
- `src/OneId.Web/src/routes/tenant/_layout.tsx` — MODIFIED (⌘K handler + CommandPalette)

### Senior Developer Review (AI)

**Date:** 2026-05-26
**Outcome:** Changes Requested
**Layers:** Blind Hunter ✅ | Edge Case Hunter ✅ | Acceptance Auditor ✅

#### Action Items

- [x] [Review][Patch] `handleSelect` calls `onOpenChange(false)` directly — bypasses `handleOpenChange`, leaving `query` and `entityResults` stale on next palette open; fix: call `handleOpenChange(false)` instead [components/shared/CommandPalette.tsx line ~227]
- [x] [Review][Patch] Hardcoded `?? 'acme-corp'` fallback in TenantAdminLayout — silently queries wrong tenant when Zustand store is null [routes/tenant/_layout.tsx:1240]
- [x] [Review][Patch] Duplicate `value` props possible in CommandItem when same-named entities appear across search types — prefix result value with action type [components/shared/CommandPalette.tsx]
- [x] [Review][Defer] `JSON.stringify` on payload with circular refs/BigInt would throw in AuditEventSheet — mock data only, no real risk [components/shared/AuditEventSheet.tsx:63] — deferred, mock-only risk

### Review Follow-ups (AI)

- [x] [AI-Review] Fix handleSelect: replace `onOpenChange(false)` with `handleOpenChange(false)` to clear query + results on navigation
- [x] [AI-Review] Remove `?? 'acme-corp'` fallback from TenantAdminLayout — guard until tenantId is non-null
- [x] [AI-Review] Prefix CommandItem `value` prop with entity type to prevent cmdk deduplication: `value={\`user:${result.label}\`}`

## Change Log

- 2026-05-26: Story created — CommandPalette (⌘K) with TypeScript-enforced action registry, entity search, and accessibility test.
- 2026-05-26: Story implemented — all tasks complete, 54/54 tests pass, TypeScript build clean.
- 2026-05-26: Code review complete — 3 patch findings, 1 deferred. Status set to in-progress pending fixes.
- 2026-05-26: All 3 review patches applied (handleSelect→handleOpenChange, acme-corp fallback removal, CommandItem value prefix). 54/54 tests pass. Status: done.
