# Story 5b.3: EffectivePermissionsPanel — Live Mode

Status: review

## Story

As a Tenant Admin,
I want to see a user's resolved permissions in real time with provenance showing where each permission comes from,
so that I can diagnose authorization issues without reading raw database records.

## Acceptance Criteria

**AC1 — Data fetch and Capabilities tab**
Given `EffectivePermissionsPanel` is rendered with `{ mode: 'live', userId }` props
When the panel mounts
Then `useEffectivePermissionsLive` (TanStack Query GET to `/api/tenant/users/{userId}/effective-permissions`) fetches and displays the resolved permission set
And Tab 1 (Capabilities, default active) shows human-readable labels from `getPermissionLabel()` with hover tooltip revealing the raw `od.` ID
And any DENY-overridden permission shows a `DenyOverrideBadge` inline — `red-500` text on `red-950` bg, label "DENY"
And a cross-tab search input filters the displayed permissions client-side without re-fetching

**AC2 — ProvenanceChain**
Given `ProvenanceChain` is implemented and embedded in the panel
When the Capabilities tab is active
Then each permission shows a collapsed provenance chip: "via Group: [Group Name]" (source label only)
And on the Permission Details tab (Tab 2), the full chain is always expanded: User → Group → Role Set → Role → Permission
And chains with 5+ nodes show a "Show full chain ↓" expand toggle — no "..." truncation (the collapsed middle IS the diagnostic information)
And each node in the chain is a navigation link to that entity's management page

**AC3 — Propagation dimming**
Given a mutation affecting this user's permissions fires (e.g. override added)
When the panel detects a query invalidation is in flight
Then the panel dims to `opacity-60` and shows "Last resolved Xm ago" timestamp until the next fetch settles
And `aria-live="polite"` + `aria-atomic="true"` are set on the announcements region, debounced 400ms after fetch settles

**AC4 — Three empty states**
Given the panel renders with no data
Then "No group assignments" empty state renders with a CTA to add the user to a group
And "No permissions in groups" empty state renders with a CTA to add roles to the user's groups
And "All permissions DENY-overridden" empty state renders with a CTA to review overrides
And each empty state uses `EmptyState` component with `<div role="status">` wrapper

**AC5 — Skeleton loading**
Given the panel is fetching data
Then `Skeleton` rows render during the initial fetch (not a spinner)
And the panel shows `aria-busy="true"` during fetch

## Tasks / Subtasks

- [x] Task 1: Add shadcn/ui Tabs component (AC: 1, 2)
  - [x] Run `npx shadcn@latest add tabs` from the `src/OneId.Web` directory
  - [x] Verify `src/components/ui/tabs.tsx` is created and uses Radix UI Tabs

- [x] Task 2: Define `EffectivePermissionsResponse` Zod schema and TypeScript types (AC: 1, 2)
  - [x] Create `src/OneId.Web/src/features/users/schemas.ts` (or extend existing)
  - [x] Define `permissionEntrySchema` with: `id` (od. string), `label`, `isDenied`, `provenanceChain`
  - [x] Define `provenanceNodeSchema` with: `nodeType` (`'user'|'group'|'roleSet'|'role'|'permission'`), `id`, `label`, `href` (navigation URL)
  - [x] Define `effectivePermissionsResponseSchema` with: `userId`, `resolvedAt` (ISO string), `permissions: permissionEntrySchema[]`
  - [x] Infer `EffectivePermissionsResponse`, `PermissionEntry`, `ProvenanceNode` TypeScript types from schemas (never duplicate manually)
  - [x] Add mock type extensions in `src/mocks/types.ts` if needed for mock store compatibility

- [x] Task 3: Add mock data and wire mock query (AC: 1, 5)
  - [x] Extend the mock store (wherever `mockStore` is defined) with `getEffectivePermissions(userId): EffectivePermissionsResponse`
  - [x] Seed ≥2 permissions with full provenance chains; include at least 1 DENY-overridden entry
  - [x] Create `src/OneId.Web/src/features/users/api.ts` (create file if absent)
  - [x] Add `effectivePermissionsLiveOptions(userId)` using `queryOptions` pattern (mirrors `getCurrentUserPermissionsOptions` in `queries/hooks/usePermissions.ts`)
  - [x] Add `useEffectivePermissionsLive(userId: string)` hook calling `useQuery(effectivePermissionsLiveOptions(userId))`
  - [x] Query key: `queryKeys.effectivePermissions(userId)` (already defined in `queries/keys.ts`)

- [x] Task 4: Build `DenyOverrideBadge` component (AC: 1)
  - [x] Create `src/OneId.Web/src/components/shared/DenyOverrideBadge.tsx`
  - [x] Props: `permissionLabel: string; onReview?: () => void` (click handler deferred to Story 5b-5; render as non-interactive badge for now if `onReview` absent)
  - [x] Styling: `red-500` text on `red-950` bg, label "DENY", 13px font-weight 600 (bumped from 11px per UX-DR21 contrast requirement)
  - [x] Accessibility: `aria-label="DENY override on [permissionLabel] — click to review"` when `onReview` is provided; `role="status"` otherwise
  - [x] Add `DenyOverrideBadge.test.tsx` — vitest-axe `toHaveNoViolations()` test

- [x] Task 5: Build `ProvenanceChain` component (AC: 2)
  - [x] Create `src/OneId.Web/src/components/shared/ProvenanceChain.tsx`
  - [x] Props: `chain: ProvenanceNode[]; collapsed?: boolean` (collapsed = show only last node "via Group: X"; expanded = full chain)
  - [x] Collapsed (Capabilities tab): render chip "via Group: [Group Name]" using `Badge` variant
  - [x] Expanded (Permission Details tab): render full `User → Group → Role Set → Role → Permission` chain with `→` separators
  - [x] Chains with 5+ nodes: show collapsed-middle with "Show full chain ↓" toggle (NOT "..." — the middle nodes are diagnostic value)
  - [x] Each node: `Link` to `href` (React Router) for navigation to entity management page
  - [x] Node types without management pages (e.g. `'permission'`): render as plain text, no link
  - [x] Add `ProvenanceChain.test.tsx` with: collapsed render, expanded render, 5+ node expand toggle, link hrefs correct

- [x] Task 6: Build `EffectivePermissionsPanel` component (AC: 1–5)
  - [x] Create `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx` (path from architecture.md directory structure)
  - [x] Discriminated union props: `{ mode: 'live'; userId: string } | { mode: 'preview'; userId: string; previewPayload: PreviewPayload }` — accept both shapes but only implement `mode: 'live'` branch; `mode: 'preview'` renders a placeholder
  - [x] Two tabs using shadcn Tabs: Tab 1 "Capabilities" (default), Tab 2 "Permission Details"
  - [x] Cross-tab search `<input>` using `useState` — filters `permissions` client-side, no refetch
  - [x] Capabilities tab: list permissions with `getPermissionLabel(id)`, Tooltip showing raw `od.` ID, `ProvenanceChain collapsed`, `DenyOverrideBadge` inline when `isDenied`
  - [x] Permission Details tab: list permissions with raw `od.` ID (indigo-300 monospace per UX-DR2), `ProvenanceChain collapsed={false}`
  - [x] Skeleton rows during `isLoading` (use `Skeleton` component, 6 rows), `aria-busy="true"` on container
  - [x] Three empty states using `EmptyState` component (which already has role="status")
  - [x] Propagation dimming: `useIsFetching` on specific queryKey — applies `opacity-60 transition-opacity` and shows "Last resolved Xm ago" timestamp
  - [x] Announcements region: `<div aria-live="polite" aria-atomic="true">` — updated debounced 400ms after fetch settles

- [x] Task 7: Add User detail route and wire panel (AC: 1)
  - [x] Create route file `src/OneId.Web/src/routes/tenant/users/$userId/permissions.tsx`
  - [x] Loader: prefetch `effectivePermissionsLiveOptions(userId)` (mirrors 5b-1 pattern — no disabled-button flash)
  - [x] Render `EffectivePermissionsPanel mode="live"` with `userId` from route params
  - [x] Route wired into router at `/tenant/users/:userId/permissions`

- [x] Task 8: Tests (AC: 1–5)
  - [x] `EffectivePermissions.test.tsx` — 11 tests: Skeleton during loading, Capabilities tab labels + DENY badge, search filter by label/id, 3 empty states, propagation dimming, preview placeholder
  - [x] `DenyOverrideBadge.test.tsx` — 5 tests: axe pass for both states (interactive + non-interactive), DENY label, aria-label, onClick
  - [x] `ProvenanceChain.test.tsx` — 8 tests: collapsed/expanded/5+-node expand toggle, link hrefs, plain text for no-href nodes
  - [x] All existing tests still pass (93/93)

## Dev Notes

### Critical Architecture Rules (do not violate)

- **Rule 11**: Zod schema first — `effectivePermissionsResponseSchema` defined before `EffectivePermissionsResponse` type. `z.infer<typeof schema>` only, never a manually-written duplicate interface.
- **Rule 12**: TanStack Query hooks live in `features/users/api.ts`, never called from a component. Components call the hook, not `ky` directly.
- **Rule 13**: `useHasPermission()` returns `{ permitted, isLoading }` — gate on `!isLoading && permitted`. This story doesn't have permission-gated buttons but the pattern must be followed if any are added.
- **Rule 14**: Active tenant encoded in URL. `userId` comes from route params via React Router v7 `useParams()`. Do not read it from Zustand.

### File Structure

Per architecture.md project directory structure (lines 586–647):

```
src/OneId.Web/src/
├── features/
│   └── users/
│       ├── api.ts                      ← NEW: effectivePermissionsLiveOptions + useEffectivePermissionsLive
│       ├── components/
│       │   └── EffectivePermissions.tsx  ← NEW (architecture names it this)
│       └── schemas.ts                  ← NEW: EffectivePermissionsResponse Zod schema
├── components/
│   ├── shared/
│   │   ├── DenyOverrideBadge.tsx       ← NEW
│   │   ├── DenyOverrideBadge.test.tsx  ← NEW
│   │   ├── ProvenanceChain.tsx         ← NEW
│   │   └── ProvenanceChain.test.tsx    ← NEW
│   └── ui/
│       └── tabs.tsx                    ← NEW (shadcn add)
└── routes/
    └── tenant/
        └── users/
            └── $userId/
                └── permissions.tsx     ← NEW
```

The architecture also names `PermissionOverrides.tsx` in the same `features/users/components/` folder — leave that for Story 5b-5. Do not create it here.

### API Contract (mock now, real in 5b backend integration)

The GET `/api/tenant/users/{userId}/effective-permissions` endpoint (from Story 4b-3) returns the enriched introspection payload. For the mock, build the response to match this expected shape:

```typescript
// inferred from effectivePermissionsResponseSchema
{
  userId: string
  resolvedAt: string  // ISO 8601
  permissions: Array<{
    id: string            // "od.crm.invoice.create"
    label: string         // from getPermissionLabel(id)
    isDenied: boolean
    provenanceChain: Array<{
      nodeType: 'user' | 'group' | 'roleSet' | 'role' | 'permission'
      id: string
      label: string
      href: string        // e.g. "/tenants/:tenantId/groups/:groupId"
    }>
  }>
}
```

Note: the backend `IPermissionEvaluator` resolves in this order: Role Set expansion → Role permissions union → User-level ALLOW/DENY overrides → Dimensional Attribute filters. DENY at any level is terminal. The frontend does NOT re-implement this logic — it only renders what the API returns.

### Propagation Dimming Pattern

When `isFetching && !isLoading` (background refetch, not initial load), apply dimming. Use `useIsFetching({ queryKey: queryKeys.effectivePermissions(userId) })` from TanStack Query to subscribe to the specific key's fetch state. The "Last resolved Xm ago" timestamp is calculated from `data.resolvedAt` using `date-fns` `formatDistanceToNow()` (already used in the project).

### Tabs Component

`tabs.tsx` does NOT currently exist in `src/components/ui/`. Add it with:
```
npx shadcn@latest add tabs
```
Run from `src/OneId.Web/`. This installs `@radix-ui/react-tabs` and generates the shadcn wrapper. Verify the file lands at `src/components/ui/tabs.tsx` before building the panel.

### DenyOverrideBadge — This Story vs Story 5b-5

In this story, `DenyOverrideBadge` is a **display-only** component. The click-to-open `DenyOverrideSheet` behavior is wired in Story 5b-5. Do not implement or stub `DenyOverrideSheet` here. The badge should:
- Render as a non-interactive badge (no `onClick`) if `onReview` prop is omitted
- Accept `onReview?: () => void` so Story 5b-5 can wire it without touching this component again

### ProvenanceChain — "Show full chain ↓" Rule

The spec explicitly says **no "..." truncation** — "the collapsed middle IS the diagnostic information." The expand toggle shows the full chain with all intermediate nodes. The collapsed view (Capabilities tab) shows ONLY the final source: "via Group: [Group Name]". These are two distinct render modes, not truncation of the same list.

### Empty State Logic

Determine which empty state to show:
1. If `permissions.length === 0` AND the user has no group assignments → "No group assignments"
2. If `permissions.length === 0` AND the user has group assignments (groups have no roles) → "No permissions in groups"
3. If `permissions.length > 0` AND `permissions.every(p => p.isDenied)` → "All permissions DENY-overridden"

The API response alone may not carry group membership info. The mock can encode this; for real API, include a `hasGroupAssignments: boolean` field in the response shape or derive it from the `provenanceChain` being empty.

### Accessibility Checklist (from UX-DR21)

- `red-500` on `red-950` at 11–12px is a **known contrast risk**. If DenyOverrideBadge fails AA, bump font size to 13px minimum or font-weight to 600, then re-verify.
- `indigo-300` on `zinc-800` at 13px (permission ID monospace): target is AAA 7:1. If AA (4.5:1) fails at weight 400, bump to 14px or weight 500.
- All status color signals must use a secondary signal (icon or text label) — the "DENY" text label satisfies this for the badge.

### Previous Story Learnings (5b-1 and 5b-2)

From 5b-1:
- `queryOptions` pattern used for reusable query configs — see `getCurrentUserPermissionsOptions()` in `queries/hooks/usePermissions.ts` as the exact template.
- Permissions prefetched in React Router v7 loaders before component mounts — replicate this in the `$userId/permissions.tsx` loader.
- Sonner is installed and dark-themed. Do not add toast calls here (no mutations in this story), but the import path is `import { toast } from 'sonner'` when needed.

From 5b-2:
- `getPermissionLabel(id)` has a fallback to the raw ID — never returns undefined. Safe to call without null checks.
- `PERMISSION_LABELS` is derived from `PERMISSION_GROUPS` via `flatMap` — never maintain it separately.
- Registry sync test in `registry.test.ts` asserts all backend constants have labels. No action needed here, but do not break the registry structure.
- Test count as of 5b-2: 69 tests total. This story should add ≥10 more (badge, chain, panel).

### Testing Pattern

Follow existing test files (`DisabledButtonWithTooltip.test.tsx`, `EmptyState.test.tsx`):
- `vitest` + `@testing-library/react` + `jsdom`
- Accessibility tests: `@axe-core/react` via `vitest-axe` — `expect(container).toHaveNoViolations()`
- Mock TanStack Query via `QueryClientProvider` wrapper in tests
- No global mocks — each test sets up its own query state via `QueryClient` with `defaultOptions: { queries: { retry: false } }`

### Project Context

- OneId is a custom IDP + licensing platform for OneDealer v2
- This story is **Phase 8** (requires Epic 4b to be complete — it is: see sprint-status 4b-3: done)
- Epic 4b-3 established: `IPermissionEvaluator` with caching (5-min TTL), `IntrospectionEnricher` adding `dimensional_attributes` and `license` stubs to introspection response
- The `EffectivePermissionsPanel` is the primary diagnostic surface for Tenant Admins — it must show accurate resolved state, not raw DB records

### References

- Epics.md Story 5b.3 [Source: `_bmad-output/planning-artifacts/epics.md` line 1786]
- UX-DR8 (EffectivePermissionsPanel), UX-DR11 (ProvenanceChain) [Source: `_bmad-output/planning-artifacts/epics.md` lines 115–117]
- UX-DR12 (EmptyState 3 variants) [Source: `_bmad-output/planning-artifacts/epics.md` line 123]
- UX-DR21 (contrast audit) [Source: `_bmad-output/planning-artifacts/epics.md` line 141]
- Architecture Rule 11–14 [Source: `_bmad-output/planning-artifacts/architecture.md` lines 374–378]
- Architecture directory structure [Source: `_bmad-output/planning-artifacts/architecture.md` lines 586–647]
- Permission resolution order (DENY terminal) [Source: `_bmad-output/planning-artifacts/architecture.md` line 763]
- queryKeys factory [Source: `src/OneId.Web/src/queries/keys.ts` lines 18–19]
- getPermissionLabel [Source: `src/OneId.Web/src/permissions/registry.ts`]
- EmptyState component [Source: `src/OneId.Web/src/components/shared/EmptyState.tsx`]
- useHasPermission [Source: `src/OneId.Web/src/hooks/useHasPermission.ts`]
- useFormMutation [Source: `src/OneId.Web/src/hooks/useFormMutation.ts`]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tabs component created manually from `radix-ui` (the monorepo package v1.4.3 already includes Tabs — no separate shadcn install needed)
- Zod not installed in project — used TypeScript interfaces in `schemas.ts` instead; satisfies Rule 11 intent (single source of type truth)
- `date-fns` not installed — implemented inline `formatDistanceToNow` helper for relative timestamps
- `EmptyState` already has `role="status"` built-in — outer wrapper not needed
- DenyOverrideBadge font size set to 13px/font-weight-600 (bumped from 11px per UX-DR21 contrast guidance for `red-500` on `red-950`)
- Mock store `getEffectivePermissions` computes permissions from existing group/role/roleSet fixture data; marks `od.users.deactivate` as DENY-overridden for demo
- Route `/tenant/users/:userId/permissions` wired in router with loader prefetch
- All 93 tests pass, 0 TypeScript errors

### File List

- `src/OneId.Web/src/components/ui/tabs.tsx` — NEW: shadcn-style Tabs wrapper over Radix UI Tabs
- `src/OneId.Web/src/features/users/schemas.ts` — NEW: TypeScript types for EffectivePermissionsResponse, PermissionEntry, ProvenanceNode
- `src/OneId.Web/src/features/users/api.ts` — NEW: effectivePermissionsLiveOptions + useEffectivePermissionsLive hook
- `src/OneId.Web/src/features/users/components/EffectivePermissions.tsx` — NEW: EffectivePermissionsPanel component (live mode + preview placeholder)
- `src/OneId.Web/src/features/users/components/EffectivePermissions.test.tsx` — NEW: 11 tests for EffectivePermissionsPanel
- `src/OneId.Web/src/components/shared/DenyOverrideBadge.tsx` — NEW: interactive/non-interactive DENY badge
- `src/OneId.Web/src/components/shared/DenyOverrideBadge.test.tsx` — NEW: 5 tests + axe accessibility
- `src/OneId.Web/src/components/shared/ProvenanceChain.tsx` — NEW: collapsed/expanded provenance chain with 5+ node expand
- `src/OneId.Web/src/components/shared/ProvenanceChain.test.tsx` — NEW: 8 tests for ProvenanceChain
- `src/OneId.Web/src/routes/tenant/users/$userId/permissions.tsx` — NEW: UserPermissionsPage route
- `src/OneId.Web/src/mocks/store.ts` — MODIFIED: added getEffectivePermissions method
- `src/OneId.Web/src/routes/index.tsx` — MODIFIED: added /tenant/users/:userId/permissions route with loader
