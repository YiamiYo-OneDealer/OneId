---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-OneId-2026-05-21/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-design-specification.md
---

# OneId - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for OneId, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: OneId must support Authorization Code Flow with PKCE (for OneDealer v2 web sessions) and Client Credentials Flow (for service-to-service). Token refresh must be supported. Standard OIDC discovery endpoint (`/.well-known/openid-configuration`) must be present and accurate.

FR-2: A User can authenticate with username and password. Passwords are stored hashed (never plaintext). Failed authentication returns a generic error (no username enumeration). Account lockout triggers after 5 consecutive failures within a configurable window.

FR-3: An authenticated User must complete a TOTP second factor before a Token is issued. Token is not issued until both factors are verified. MFA enrollment is required on first login.

FR-4: A User can reset their password via a time-limited email link (1-hour expiry) without admin intervention. Previous password cannot be reused immediately.

FR-5: OneId issues a signed JWT (RS256) containing standard OIDC claims (`sub`, `iss`, `aud`, `exp`, `iat`, `jti`) and the User's Role names. OpenIddict retains a server-side `jti` record for revocation. The introspection endpoint (`/connect/introspect`) validates the token and enriches the response with Permissions, Dimensional Attributes, and License state. JWT TTL is 15 minutes; refresh token is long-lived and stored server-side.

FR-5a: When a User's Role assignments change, OneId immediately invalidates that User's active tokens in the server-side store by `jti`. The User's next introspection call (after the 5-minute consumer cache expires) returns `active: false`; OneDealer v2 triggers a silent token refresh.

FR-6: An Internal Admin can create, read, update, and deactivate string-identified Permissions globally. Permission identifiers use dot-notation (e.g., `crm.invoice.create`) and must be unique across the catalog. Deactivated Permissions are removed from future token issuances but the record is retained.

FR-7: A Tenant Admin can create, read, update, and delete Roles within their Tenant. A Role references one or more Permissions from the global catalog. Deleting a Role removes it from all Groups within the Tenant.

FR-8: A Tenant Admin can create, read, update, and delete Role Sets within their Tenant. A Role Set contains one or more Roles and can be assigned to Groups (bulk assignment mechanism; replaces RoleGroups from PRD).

FR-9: A Tenant Admin can create, read, update, and delete Groups within their Tenant. Groups are assigned Roles and Role Sets. A User's effective Permission set is the union of all Permissions from all Roles held by all Groups the User belongs to.

FR-10: A Tenant Admin can assign Dimensional Attribute values to Users within their Tenant across five axes: Company, Location, Branch, Make, MarketSegment. A User may hold multiple values per axis. Dimensional Attributes are assigned per User, not per Role or Group. Values are normalized against per-Tenant reference lists.

FR-11: When a Token is issued, OneId evaluates the User's full authorization context. JWT contains deduplicated Role names. Introspection returns: additive union of all Permissions, per-axis union of Dimensional Attribute values, and License state. DENY override at any level is terminal (short-circuits evaluation). Evaluation completes within the 500ms issuance budget.

FR-12: An Internal Admin can create, read, update, and suspend Tenants. Suspending a Tenant immediately revokes all active `jti` records for all Users in that Tenant. New token issuance is rejected until the Tenant is reinstated.

FR-13: All data reads and writes are scoped to the requesting User's Tenant. A Tenant Admin cannot read or modify data belonging to another Tenant. Tenant isolation is enforced at the data layer via EF Core global query filters (not only at the API layer). Cross-tenant data leakage is a critical defect.

FR-14: An Internal Admin can designate one or more Users within a Tenant as Tenant Admins. A Tenant Admin can manage Users, Groups, Roles, Role Sets, and Dimensional Attribute assignments within their Tenant. A Tenant Admin cannot elevate Permissions beyond what the Tenant itself holds or modify Licenses.

FR-15: An Internal Admin can create and update a seat-count License for a Tenant, specifying maximum active Users. A Tenant may have at most one active License. License record stores: Tenant ID, model (`seat_count`), max seats, effective date, and extensible `parameters` object.

FR-16: An Internal Admin can configure a Tenant to authenticate Users via Okta (OAuth2/OIDC). OneId acts as OIDC client; maps external identity to OneId User by email claim. Claims from Okta are not propagated. No matching OneId User = auth failure with clear error; auto-provisioning is out of scope.

FR-17: An Internal Admin can configure a Tenant to authenticate Users via Azure AD (Microsoft Entra ID). Same behavior as FR-16 for token issuance and identity mapping. Identity mapped by email or UPN claim from Azure AD.

FR-18: When issuing a Token, OneId checks whether the Tenant's active User count is within the licensed seat limit. At limit: token issuance is denied with a clear error referencing the Tenant Admin. Under limit: Token issued with `license.status = "active"`. Active User = any non-deactivated User account.

FR-19: The License data model supports extensibility to concurrent and usage-based models without schema migration. `model` field (initially `seat_count`) and generic `parameters` object for model-specific configuration. No concurrent or usage-based enforcement logic in POC.

FR-20: An Internal Admin can manage via the React console: Tenants, global Permission catalog, Tenant License assignments, IDP federation configuration per Tenant, Tenant Admin designation, Dimensional Attribute reference values, and global audit log. All Tenants are visible. Internal Admin can impersonate a Tenant Admin view for support.

FR-21: A Tenant Admin can manage within their Tenant via the React console: Users, Groups, Roles, Role Sets, Dimensional Attribute assignments, and tenant-scoped audit log. Navigation and data are scoped to the Tenant Admin's Tenant.

FR-22: All significant management actions (User create/update/delete, Role assignment change, License modification, IDP configuration change) are recorded with timestamp, actor, action, and entity ID. Audit log is readable by Internal Admins and Tenant Admins (scoped). Audit log is read-only in POC.

### NonFunctional Requirements

NFR-1 (Security): Passwords stored using Argon2id (ASP.NET Core Identity built-in). Tokens signed with RS256. HTTPS enforced on all endpoints with no HTTP fallback. Tenant data isolation enforced at the data layer. Credentials and secrets are never logged.

NFR-2 (Performance — Token Issuance): Token issuance (from credential verification to signed JWT) completes in under 500ms p95 under POC test load. This is the primary POC performance gate.

NFR-3 (Performance — Management UI): Management UI operations (CRUD) complete in under 1 second p95.

NFR-4 (Performance — Introspection): Introspection endpoint response time under 50ms p95 under POC test load (excludes network). Cache TTL is 5 minutes; OneDealer v2 calls introspection at most once per 5-minute window per active token. This is the accepted propagation delay for all permission/dimension changes — not a defect.

NFR-5 (OpenIddict Extensibility Validation): POC must confirm that OpenIddict's token pipeline can be extended to produce the hybrid JWT + enriched introspection response without breaking standard OIDC compliance. Custom claim destination wiring, enriched introspection handler, and server-side `jti` revocation must all be demonstrated end-to-end.

NFR-6 (Observability): Structured logs for: authentication success/failure (with Tenant ID, no credential data), token issuance, seat-count enforcement decisions, management actions. Pipeline: Serilog → OTEL Collector → Seq.

NFR-7 (Correctness — Signing Key): Dev signing key must be file-based and survive application restarts. `DevSigningKeyStabilityTest` enforces this. Production key storage and rotation procedure are post-POC.

### Additional Requirements

- AR-1 (Project Setup): Backend initialized with `dotnet new webapi -n OneId.Server --use-controllers` + OpenIddict 7.5.0 + Npgsql EF Core provider. Frontend initialized with `npm create vite@latest OneId.Web -- --template react-ts` + `npx shadcn@latest init`. This initialization is the first implementation story.
- AR-2 (Local Dev Infrastructure): Single `docker-compose.yml` covering: OneId.Server, PostgreSQL, OTEL Collector, Seq. Must be runnable with a single command for local development.
- AR-3 (CI/CD): GitHub Actions CI pipeline covering build, test, and migration bundle validation.
- AR-4 (Logging Pipeline — Day 1): Serilog + OTEL pipeline wired in the project setup story. Cannot be deferred. Sensitive field destructuring must prevent passwords and tokens from appearing in logs (`SerilogDestructuringTests.cs`).
- AR-5 (EF Core Setup Order): EF Core global query filters must be configured and active before any data access. Migrations applied after filter configuration. `ITenantContext` middleware must be registered before OpenIddict (provides the `tid` claim the pipeline reads).
- AR-6 (DevSeeder): Runs only after global query filters are active. Seeds: dev tenant, admin user, OpenIddict test client, pre-provisioned federated test user (for SM-3 federation test). Seeded data must respect tenant isolation.
- AR-7 (Integration Test Infrastructure): Testcontainers (PostgreSQL) + Respawn for integration tests. `TestTokenFactory` must produce tokens with full required claims: `tid`, `sub`, `scope`, `seat_count`, `roles[]`.
- AR-8 (ArchUnit Boundary Enforcement): `InternalAdminContext` injectable only in `Application/Internal/` namespace. Build failure on cross-namespace injection. `InternalBoundaryTests.cs` enforces this.
- AR-9 (Permission Catalog Seeding): `PermissionCatalog.cs` is the version-controlled source of truth. `Permissions` static class exposes all permission ID constants. No inline string literals for permission IDs anywhere in application code. `PermissionCatalogSyncTests.cs` asserts every constant has a DB seed row.
- AR-10 (Cache Abstraction): All cache access via `ICacheService` wrapping `IMemoryCache` for POC. Cache keys: `{entity}:{userId}:{tenantId}`. Redis swap at first staging deploy is an explicit gate, not a suggestion.
- AR-11 (User-level Permission Overrides): ALLOW/DENY overrides per User with reason field and optional expiry. DENY at any level is terminal. Expiry enforced at read time via DB filter (`ExpiresAt IS NULL OR ExpiresAt > NOW()`). No background sweeper. Expired records retained for audit trail.
- AR-12 (Normalized Dimension Values): Per-Tenant reference lists for each dimension axis (`DimensionValue` table). Tenant Admin assigns from these lists; cannot assign values outside the Tenant's reference list.
- AR-13 (Migration Bundles): Migration bundles used for CI/CD deployment. No EF tooling required at runtime.
- AR-14 (Concurrency): `UseXminAsConcurrencyToken()` on all mutable entities. `DbUpdateConcurrencyException` → `409 Conflict` Problem Details. No silent last-write-wins.
- AR-15 (Deferred-Skip Governance): The `[Fact(Skip = "Wired in Epic X")]` pattern is permitted to make forward dependencies visible in CI, but is strictly capped. At most 3 deferred skips may be open at any point in time. A new deferred skip must not be introduced until an existing one is closed by its owning story. The three currently defined skips (`DevSigningKeyStabilityTest` → Story 2.1, `TestTokenFactoryContractTests` → Story 3.5, `PermissionCatalogSyncTests` → Story 4a.1) are at the cap. Exceeding this cap is a sprint planning blocker.

### UX Design Requirements

UX-DR1: Implement semantic CSS variable token system in `globals.css` with 8 defined tokens: `--background` (zinc-950), `--sidebar` (zinc-900), `--card`/`--popover` (zinc-800), `--primary` (indigo-500), `--destructive` (red-500 fg / red-950 bg), AdminTierBanner amber-600 bg + zinc-950 text, permission ID indigo-300 monospace. ESLint rule enforcing no raw Tailwind color utilities on semantic elements — only CSS variable token aliases.

UX-DR2: Configure dark mode via Tailwind `dark:` variant with Inter typeface. Type scale: 24px page title, 18px section heading, 14px body, 12px label/caption, 13px monospace for permission IDs. Tabular numerals enabled for all numeric data. Base unit 4px, primary rhythm 8px. DataTable rows: 32px compact default / 40px comfortable (user preference in local state).

UX-DR3: Implement persistent left sidebar `GlobalNav` (240px expanded, icon-only 56px collapse, state persisted in `localStorage`). Navigation items scoped by admin tier: Tenant Admin sees Users, Groups, Roles, Role Sets, Audit Log; Internal Admin adds Tenants, Permissions, Licenses. `aria-current="page"` on active item. `indigo-500` 2px left border + `zinc-800` bg for active state. `TenantSwitcher` at bottom (Internal Admin only). ⌘K hint visible at bottom footer. `<nav>` landmark, `<main>` content area, `<header>` breadcrumbs.

UX-DR4: Implement `AdminTierBanner` — full-width 40px strip above sidebar+content layout. `amber-600` bg, `zinc-950` text. Content: "Internal Admin — Tenant: [Name] / [Current Section]". Action: "← All Tenants" router navigation (not "Exit"). Unsaved-changes guard triggers confirmation Dialog before navigation when form is dirty. `aria-live="polite"` (NOT `role="alert"` — context switch is intentional user action). WCAG AA contrast validation required (test at actual rendered size). Visible only when Internal Admin has active tenant context in URL.

UX-DR5: Encode active tenant in URL params (`/tenants/:tenantId/...`) as source of truth. React Router v7 nested layouts. Zustand caches resolved tenant object for convenience (never authoritative). All navigation updates URL. TanStack Query invalidates all tenant-scoped query keys on tenant change. Deep links and browser back/forward work.

UX-DR6: Define `queryKeys` factory in `queries/keys.ts` before writing the first query. Keys: `users(tenantId)`, `user(tenantId, userId)`, `groups(tenantId)`, `effectivePermissions(userId)`, `effectivePermissionsPreview()`, `tenants()`, `tenant(tenantId)`, `seatUsage(tenantId)`. All tenant-scoped keys include `tenantId` as const-typed tuple — missing tenantId = stale data bug after tenant switch.

UX-DR7: Implement `DataTable` component with TanStack Table v8. Props: `columns: ColumnDef<TData, TValue>[]`, `data`, `isLoading` (renders Skeleton rows), optional `pagination` with `onPaginationChange`. Filtering injected from page level — not inside component. Client-side sorting Phase 1 (`getSortedRowModel`), server-side opt-in via `onSortingChange` + `manualSorting`. `aria-busy="true"` on table container during initial fetch.

UX-DR8: Implement `EffectivePermissionsPanel` with TypeScript discriminated union props: `{ mode: 'live'; userId: string } | { mode: 'preview'; userId: string; previewPayload: PreviewPayload }`. Shell + two independent data hooks: `useEffectivePermissionsLive` (TanStack Query GET) + `useEffectivePermissionsPreview` (debounced POST 300–500ms, cancel-on-new-request). Tab 1 (Capabilities, default): human-readable labels from frontend label map, hover tooltip reveals `od.` ID, DENY badge inline, cross-tab search input. Tab 2 (Permission Details): raw `od.` IDs + full `ProvenanceChain`. Three distinct empty states with different copy and CTAs. Propagation dimming (`opacity-60`) after mutation with "Last resolved Xm ago" timestamp. `aria-live="polite"` + `aria-atomic="true"` on announcements, debounced 400ms after fetch settles.

UX-DR9: Implement `DenyOverrideBadge` + `DenyOverrideSheet`. Badge: `red-500` text on `red-950` bg, label "DENY", `aria-label="DENY override on [permission label] — click to review"`. Sheet: override type, reason, applied-by, date, optional expiry. "Remove Override" destructive Button. "Force Re-authenticate" secondary Button — permission-gated on `od.admin.users.revoke`: hidden if lacking permission, shown-disabled-with-tooltip if partial access (never shown-then-failed). Calls `POST /api/users/{userId}/tokens/revoke` (tenant-scoped jti invalidation). Toast: "Changes effective within 5 minutes" (no revocation) vs. "User must re-authenticate — changes are immediate" (revocation triggered).

UX-DR10: Implement `SeatUsageIndicator` inline in Users section header. Label: "42 of 50 seats used" (screen reader-safe — no "/" split numbers). Color + icon signal: zinc-400 (normal) → amber-400 + warning icon (≥80%) → red-400 + alert icon (100%). At 100%: "New User" primary CTA disabled with tooltip "Seat limit reached. Contact your administrator to expand your license." Seat error surfaces proactively — not for the first time at form submission.

UX-DR11: Implement `ProvenanceChain` component. Capabilities tab: collapsed to source label chip only — "via Group: Fleet Managers". Permission Details tab: full chain always visible (User → Group → Role Set → Role → Permission), horizontal scroll on overflow, "Show full chain ↓" expand for 5+ nodes (no "..." truncation — the collapsed middle is the diagnostic information). Chip style: zinc-700 bg, zinc-300 text, `›` separators, hover indigo-500 border. Each node is a navigation link to the entity's management page.

UX-DR12: Implement `EmptyState` component. Anatomy: centered, lucide icon (zinc-600), bold title, description (must name a next action), optional primary CTA. Variants: no data (with CTA), no search results (no CTA — modify search), 3 EffectivePermissionsPanel states (no assignments, no permissions in groups, all DENY), error. `<div role="status">` wrapper when replacing DataTable so screen readers announce the state change.

UX-DR13: Implement `useFormMutation` hook wrapping TanStack Query `useMutation`. `MutationMessages` interface: `{ success: string; error: string | ((err) => string); propagationNote?: string }`. Durable (non-auto-dismiss) Sonner toast on success. `propagationNote` appends "Changes effective within 5 minutes." Force revocation toast injects "User must re-authenticate — changes are immediate." Inline form errors for validation; auto-dismiss (8s) toast for system errors. `onSuccess`/`onError` TanStack callbacks passable through hook — not swallowed.

UX-DR14: Implement frontend `PERMISSION_GROUPS` / `PERMISSION_LABELS` label map in `permissions/registry.ts`. Structure: `PermissionGroup[]` grouped by domain with `{ id, label, description? }` per permission. Derived flat lookup `PERMISSION_LABELS` (never maintained separately). `getPermissionLabel(id)` returns label or raw `od.` ID as fallback (never blank). Frontend test asserts every backend `Permissions` class constant has a `PERMISSION_GROUPS` entry (mirrors backend `PermissionCatalogSyncTests.cs`).

UX-DR15: Implement `useHasPermission(permissionId)` hook returning `{ permitted: boolean, isLoading: boolean }`. Permissions prefetched in React Router v7 route `loader` before component mounts — no flash-disable on cold load. During `isLoading`: interactive elements render disabled (not hidden). Permission-gated UI: Hidden (tier never has access), Disabled-permission-block (with tooltip "You don't have permission"), Disabled-precondition-block (with specific blocker + resolution link), Visible-gated-on-action FORBIDDEN. Tenant Admin-only routes return 404-equivalent on direct URL access.

UX-DR16: Implement `DisabledButtonWithTooltip` pattern: `<span>` wrapper for pointer events, `aria-disabled="true"` alongside native `disabled`, `useId()` for SSR-safe IDs, `aria-describedby` pointing to `TooltipContent`. Two contracts: permission block ("You don't have permission to [action]. Contact your administrator.") and precondition block ("[Specific blocker]. [Concrete next step with link].").

UX-DR17: F-3 Tenant provisioning form — vertical stepper with numbered sections, validates on "Next" before proceeding. Review step (all sections read-only) before final commit. `unstable_useBlocker` for unsaved-changes guard (F-3 only — scope to user-entered content, not step navigation state). Note: API unstable in RR7 minor versions, does not intercept tab close. "Test Federation" action validates OIDC discovery URL reachability before tenant creation commit.

UX-DR18: Form patterns across all flows: validation fires on blur per field + on submit for all fields (not on every keystroke). Required fields: red asterisk in label. Submit button never pre-disabled based on field completion. Server validation errors inline under the field. Multi-select combobox (checkbox + search) for Group assignment and Dimension values — selections from tenant reference lists only, removable badges below input.

UX-DR19: Confirmation pattern tiers: Low (no confirm — removing user from group), Medium (`Dialog` with destructive confirm — deleting Role, removing override, **suspending Tenant**), High (type-to-confirm Dialog — **Tenant deletion only**). Dialog Medium: one-sentence consequence body. Dialog High: entity name in body + case-sensitive typed match required before confirm button enables. No undo toast in POC.

UX-DR20: F-1 New User flow — real-time `EffectivePermissionsPanel` preview before save (not after). Diff-based `POST /effective-permissions/preview` debounced 300ms. Default Capabilities tab for Tenant Admins. "This user will have no permissions" amber `Alert` warning when groups have no role assignments. Seat usage check at Users list level (indicator + disabled CTA) — not first at form submission.

UX-DR21: WCAG AA contrast validation (automated + manual) for 4 high-risk element combinations: (1) `amber-600` + `zinc-950` AdminTierBanner ≥4.5:1, (2) `indigo-300` on `zinc-800` 13px/400 — target AAA 7:1 (or bump to 14px/500 and recheck AA), (3) `red-500` on `red-950` at 11–12px — likely AA failure, escalate to 13px min or font-weight 600, (4) `amber-400` on `zinc-950` ≥3:1. All status-color signals also use a secondary signal (icon or text).

UX-DR22: Automated accessibility testing pipeline: `vitest-axe` on component tests (wrap async in `act()` before axe runs); `@axe-core/playwright` on flow tests (`await page.waitForSelector('[role="dialog"]')` before `.analyze()` for Sheet/Dialog portals); Playwright config `--force-color-profile=srgb` for CI headless contrast detection. Manual pre-POC checklist: keyboard-only through all 5 flows, DataTable with 100+ rows, task-completion test ("revoke DENY override keyboard-only"), Dialog/Sheet focus traps, CommandPalette focus return on close.

UX-DR23: Implement `DimensionalScopeSummary` component — plain-language sentence summarising a user's dimensional scope alongside any matrix or table view. Required wherever a role assignment includes dimensional restrictions. Template: `"[Role Name] — restricted to [Axis: value1, value2] and [Axis: value1]"`. Edge cases: all values on an axis = `"all [axis-plural]"` (not a full list); value count >3 = first 3 + `"+N more"` Tooltip (keyboard-accessible, `aria-label="Show all [axis] values"`); single value = singular axis label; no restrictions = explicit `"no dimensional restrictions (full scope)"` label. Render in: role assignment edit form (live-updating as values change), assignment confirmation step, User detail view (read-only). A non-technical admin must be able to read the summary aloud and correctly describe what the user can and cannot access without referring to the matrix. Covered by Story 5b.6.

### FR Coverage Map

| Requirement | Epic | Summary |
|---|---|---|
| FR-1 | Epic 2 | Authorization Code + PKCE, Client Credentials, token refresh, OIDC discovery |
| FR-2 | Epic 2 | Password auth, Argon2id, lockout after 5 failures |
| FR-3 | Epic 2 | TOTP MFA, enrollment on first login, TOTP challenge on subsequent logins |
| FR-4 | Epic 2 | Password reset via 1-hour email link — backend + minimal complete UI |
| FR-5 | Epic 2 | RS256 JWT issuance, jti revocation store (interface contract), enriched introspection handler |
| FR-5a | Epic 2 | Role-change jti invalidation (mechanism); full integration test in Epic 3 |
| FR-6 | Epic 4a | Global Permission catalog (Internal Admin), `od.` namespace, `PermissionCatalog.cs` source of truth |
| FR-7 | Epic 4a | Role management (Tenant Admin), permission references |
| FR-8 | Epic 4a | Role Set management (Tenant Admin), role references |
| FR-9 | Epic 4a | Group management (Tenant Admin), role + role-set assignments |
| FR-10 | Epic 4a | 5-axis Dimensional Attribute reference lists + per-user normalized assignments |
| FR-11 | Epic 4b | Token evaluation — permission union, DENY terminal, user-level overrides, dimensional attributes in introspection, 500ms budget |
| FR-12 | Epic 3 | Tenant CRUD + suspension with jti revocation for all tenant users |
| FR-13 | Epic 3 | Tenant data isolation via EF Core global query filters (data-layer enforcement) |
| FR-14 | Epic 3 | Tenant Admin designation by Internal Admin |
| FR-15 | Epic 3 | Seat-count License creation and update |
| FR-16 | Epic 6★ | Okta OIDC federation — identity mapped by email claim |
| FR-17 | Epic 6★ | Azure AD federation — identity mapped by email or UPN claim |
| FR-18 | Epic 3 | Seat limit enforcement at token issuance (active user count vs. max seats) |
| FR-19 | Epic 3 | Extensible license data model (`model` + `parameters` JSON) |
| FR-20 | Epics 5a–5c | Internal Admin React console (all management surfaces) |
| FR-21 | Epics 5a–5c | Tenant Admin React console (all management surfaces, tenant-scoped) |
| FR-22 | Epic 5c | Audit log UI (readable by Internal Admin + Tenant Admin) |
| NFR-1 | Epics 1–2 | Security — Argon2id, RS256, HTTPS, no credential logging |
| NFR-2 | Epic 2 | Token issuance ≤500ms p95 |
| NFR-3 | Epic 5c | UI management operations ≤1s p95 |
| NFR-4 | Epic 2 | Introspection ≤50ms p95, 5-min cache TTL |
| NFR-5 | Epic 2 | OpenIddict extensibility POC validation end-to-end |
| NFR-6 | Epic 1 | Serilog → OTEL Collector → Seq pipeline (wired Day 1) |
| NFR-7 | Epic 1 | Dev signing key file-based, survives restarts (`DevSigningKeyStabilityTest`) |
| AR-1 | Epic 1 | Project initialization — backend + frontend + shadcn |
| AR-2 | Epic 1 | Docker Compose local dev stack |
| AR-3 | Epic 1 | GitHub Actions CI pipeline |
| AR-4 | Epic 1 | Serilog destructuring tests — no credentials in logs |
| AR-5 | Epic 1+3 | EF Core global query filters configured first; `ITenantContext` registered before OpenIddict |
| AR-6 | Epic 1 | DevSeeder (runs after global query filters active) |
| AR-7 | Epic 1 | Testcontainers + Respawn + `TestTokenFactory` (full claim shape, including `seat_count` placeholder) |
| AR-8 | Epic 1 | ArchUnit boundary enforcement (`InternalBoundaryTests.cs`) |
| AR-9 | Epic 4a | `PermissionCatalog.cs` source of truth; `PermissionCatalogSyncTests.cs` |
| AR-10 | Epic 1 | `ICacheService` wrapping `IMemoryCache` |
| AR-11 | Epic 4b | User-level ALLOW/DENY overrides with reason + optional expiry |
| AR-12 | Epic 4a | Normalized dimension values (`DimensionValue` table, per-tenant reference lists) |
| AR-13 | Epic 1 | Migration bundles for CI/CD (validated against Testcontainers, not just compilation) |
| AR-14 | Epic 1 | `UseXminAsConcurrencyToken()` on all mutable entities → 409 Conflict |
| UX-DR1–6 | Epic 5a | CSS tokens, dark mode, typography, GlobalNav, URL-as-truth, queryKeys factory |
| UX-DR7–8 | Epic 5b | DataTable, EffectivePermissionsPanel (live + preview) |
| UX-DR9–10 | Epic 5b | DenyOverrideSheet, SeatUsageIndicator |
| UX-DR11–13 | Epic 5b | ProvenanceChain, EmptyState, useFormMutation |
| UX-DR14–16 | Epic 5b | Permission label map, useHasPermission, DisabledButtonWithTooltip |
| UX-DR17–20 | Epic 5c | Form patterns, confirmation tiers, F-1/F-3 stepper, real-time preview |
| UX-DR21–22 | Epic 5c | WCAG AA contrast validation, automated accessibility testing pipeline |
| UX-DR23 | Epic 5b | DimensionalScopeSummary — plain-language dimensional scope sentence |

---

## Epic List

### Epic 1: Foundation & Dev Infrastructure
A developer can clone the repo and run the entire system locally with a single `docker compose up`. CI pipeline is green. Observability pipeline is wired. Test infrastructure is ready. This is the greenfield bootstrap — no end-user feature value, but the prerequisite for everything else.

**Exit gate (Definition of Done):** CI pipeline passes (build + test + migration bundle validation against Testcontainers). `docker compose up` starts Server, PostgreSQL, OTEL Collector, Seq. DevSeeder runs, seeds dev tenant + admin user + OpenIddict test client. `SerilogDestructuringTests.cs` green. `InternalBoundaryTests.cs` green. `DevSigningKeyStabilityTest` not yet active (signing key infrastructure wired but auth pipeline is Epic 2). `TestTokenFactory` produces tokens with full claim shape including `seat_count` placeholder.

**FRs covered:** (none directly)
**ARs covered:** AR-1, AR-2, AR-3, AR-4, AR-5 (partial — EF Core global query filters + registration order), AR-6, AR-7, AR-8, AR-10, AR-13, AR-14
**NFRs covered:** NFR-6, NFR-7
**Implementation notes:**
- `Program.cs` registration order is a delivery correctness gate — story must include ordered checklist: (1) register `ITenantContext` middleware, (2) configure EF Core with global query filters referencing tenant context, (3) register OpenIddict (after EF Core). Backed by an integration test that boots the app with a real tenant context and asserts queries are filtered.
- `TestTokenFactory` claim shape is pinned from day one: `tid`, `sub`, `scope`, `seat_count` (hardcoded placeholder), `roles[]`. No drift allowed as later epics add claims.
- `DevSeederIntegrationTests.cs` asserts seeded data is visible to the expected tenant and invisible to a different tenant context (validates filter activation, not just filter existence).
- `UseXminAsConcurrencyToken()` applied to all mutable entities — story must enumerate them explicitly.
- Migration bundle validation in CI applies bundle against a fresh Testcontainers PostgreSQL instance, not just compilation.

---

### Epic 2: Working Authentication
An end user can authenticate with email/password + TOTP, receive a signed JWT with standard OIDC claims, introspect it, and reset their password. OpenIddict extensibility is validated end-to-end (custom claim wiring, enriched introspection, jti revocation). OIDC discovery endpoint is live and accurate.

**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5, FR-5a
**NFRs covered:** NFR-1, NFR-2, NFR-4, NFR-5
**Implementation notes:**
- jti revocation store — interface and schema fully delivered as a completed API contract by end of this epic. Epic 3's Tenant suspension path depends on it.
- `ITokenClaimsEnricher` interface (or `ClaimsTransformationPipeline` pattern) defined in this epic with a single-stage implementation (role names only). Epic 4b adds stages additively — zero token issuance code gets deleted.
- FR-4 password reset — backend (token generation, email, 1-hour expiry, no-reuse check) AND a minimal-but-complete UI (request reset page, email confirmation, new password form). Design system polish deferred to Epic 5; functional loop closes here.
- FR-5a (role-change jti invalidation) — mechanism implemented here; full integration test with tenant context written in Epic 3 when `ITenantContext` middleware is active.
- `DevSigningKeyStabilityTest` is the first integration test in this epic — ephemeral signing key means flapping JWKS in every subsequent auth test.
- Token issuance performance budget test: `TokenIssuancePerformanceTests.cs` with 400ms `Stopwatch` ceiling (leaving 100ms headroom against the 500ms NFR-2 gate).

---

### Epic 3: Tenant & License Governance
An Internal Admin can provision Tenants, assign seat-count Licenses, designate Tenant Admins, and suspend Tenants. Seat limits are enforced at token issuance. Tenant data isolation is enforced at the EF Core data layer. A Tenant Admin can log in and view their tenant's license and seat usage summary (first Tenant Admin touchpoint).

**FRs covered:** FR-12, FR-13, FR-14, FR-15, FR-18, FR-19
**ARs covered:** AR-5 (full — `ITenantContext` middleware registered before OpenIddict, enforced)
**Implementation notes:**
- `ITenantContext` middleware is story 1. Everything else in this epic depends on tenant context being resolvable at the data layer. No other stories start until this story is complete and its integration test is green.
- `TenantIsolationRegressionTests.cs` introduced in this epic. Asserts a user from Tenant A cannot observe Tenant B's data through any query path. Extended in Epic 4a when role/permission entities are added.
- Tenant suspension integrates with the jti revocation store delivered in Epic 2 — write the full cross-tenant suspension integration test here.
- ArchUnit `InternalBoundaryTests.cs` update (validates Internal Admin API endpoint isolation) — Internal Admin CRUD endpoints are the last stories in this epic, not intermediate ones, to avoid blocking CI during development.
- FR-18 seat enforcement — stories must cover boundary cases: at limit (token denied, clear error surfaced to user), under limit (token issued), and what happens to active sessions when the limit is lowered retroactively.
- Default role set initialization — Epic 3 seeds a minimal "empty" role set structure for new tenants. Bridges the Epic 3→4 handoff: Tenant Admin lands in Epic 4 with a canvas to build on, not a blank void.
- Tenant Admin touchpoint: Tenant Admin can log in and see a read-only view of their tenant's license (seats used / max seats, effective date). No management capability yet — that's Epic 4. This ensures Tenant Admin stakeholders have something demonstrable at Epic 3 completion.

---

### Epic 4a: Authorization Data Model
A Tenant Admin can manage the full authorization structure: global Permission catalog, Roles, Role Sets, Groups, Dimensional Attribute reference lists, and per-user Dimension assignments. All data model entities are in place with correct tenant isolation and concurrency tokens.

**FRs covered:** FR-6, FR-7, FR-8, FR-9, FR-10
**ARs covered:** AR-9, AR-12
**Implementation notes:**
- `PermissionCatalog.cs` fully populated + `PermissionCatalogSyncTests.cs` enforced is story 1. Every other story in this epic references `od.*` permission constants — the catalog must be stable before any permission reference work begins.
- `DimensionValue` table (per-tenant reference lists for all 5 axes) is a seeding story that must land before any `UserDimensionAssignment` stories. The foreign key dependency is a trap if stories run in the wrong order.
- `TenantIsolationRegressionTests.cs` (from Epic 3) extended to cover Role, RoleSet, Group, and Permission entities.
- `UseXminAsConcurrencyToken()` confirmed on all new entities (Role, RoleSet, Group, Permission, DimensionValue, UserDimensionAssignment).
- All API endpoints are Internal Admin or Tenant Admin management APIs — no token evaluation logic in this epic. Token evaluation is Epic 4b.

---

### Epic 4b: Token Evaluation & Overrides
Token issuance returns the correct effective permission set: union of all permissions across a user's group chain (User → Groups → Role Sets → Roles → Permissions), with DENY terminal, user-level overrides respected, and dimensional attributes in the enriched introspection response. The 500ms issuance budget is validated by a performance test.

**FRs covered:** FR-11
**ARs covered:** AR-11
**Implementation notes:**
- All stories in this epic depend on Epic 4a's schema being stable (permission catalog, roles, groups, dimension values all seeded).
- `ITokenClaimsEnricher` (defined in Epic 2) receives a full implementation here. Epic 4b extends the pipeline additively — no token issuance code from Epic 2 is deleted.
- User-level overrides (`UserPermissionOverride`): ALLOW/DENY with reason, optional expiry. Expiry enforced at read time via DB filter (`ExpiresAt IS NULL OR ExpiresAt > NOW()`). No background sweeper. Expired records retained for audit trail.
- Token evaluation (permission union + DENY terminal + override check + dimensional attributes) is the last story. All input sources must be complete before the evaluation pipeline is built and performance-tested.
- `TokenEvaluationPerformanceTests.cs` with 400ms `Stopwatch` ceiling — gate against the 500ms NFR-2 budget with headroom. Without a test, the budget is aspirational prose.
- `TestTokenFactoryContractTests.cs` — asserts that a token produced by `TestTokenFactory` passes validation through the same `ITokenClaimsEnricher` pipeline used in production. Catches drift as each epic adds claims.
- Introspection response enriched: `Permissions[]`, `DimensionalAttributes{axis: values[]}`, `License{status, seats_used, max_seats}`.

---

### Epic 5a: Frontend Shell & Core Components
The React application boots in dark mode with the full design system in place. A developer can navigate between pages using `GlobalNav`. `DataTable` renders with Skeleton loading states. `AdminTierBanner` appears when Internal Admin is in a tenant context. The query key factory and CSS token ESLint rule are enforced from the first commit.

**FRs covered:** FR-20, FR-21 (partial — navigation shell)
**UX-DRs covered:** UX-DR1, UX-DR2, UX-DR3, UX-DR4, UX-DR5, UX-DR6, UX-DR7, UX-DR12 (EmptyState)
**Implementation notes:**
- CSS token system in `globals.css` + ESLint rule for CSS token enforcement — wired in the first story. Without the ESLint rule, the token system is decorative.
- `queryKeys` factory in `queries/keys.ts` defined before the first query is written.
- `GlobalNav` uses URL params as source of truth for tenant context. Zustand caches resolved tenant object — never authoritative.
- `AdminTierBanner` uses `aria-live="polite"` (NOT `role="alert"`).
- Playwright step added to GitHub Actions CI as a stub in this epic — so it exists when Epic 5c adds real flow tests. Not an afterthought.
- `DataTable` `isLoading` + Skeleton rows wired from day one — not deferred.

---

### Epic 5b: Permission & Override UX
The `EffectivePermissionsPanel` shows a user's resolved authorization state in live mode and real-time preview mode. Tenant Admins can view DENY overrides, remove them, and trigger Force Re-authenticate. The `useFormMutation` hook drives all write operations with consistent propagation-honest feedback. The `useHasPermission` hook gates all permission-sensitive UI.

**FRs covered:** FR-20, FR-21 (permission management surfaces)
**UX-DRs covered:** UX-DR8, UX-DR9, UX-DR10, UX-DR11, UX-DR13, UX-DR14, UX-DR15, UX-DR16
**Dependencies:** Epic 4b's introspection payload shape must be stable before this epic starts. The `EffectivePermissionsPanel` preview POST endpoint must return deterministic results for the DENY override path.
**Implementation notes:**
- `EffectivePermissionsPanel` discriminated union props: `{ mode: 'live'; userId: string } | { mode: 'preview'; userId: string; previewPayload: PreviewPayload }`.
- `useEffectivePermissionsPreview` debounced POST (300–500ms, cancel-on-new-request).
- `DenyOverrideSheet` "Force Re-authenticate" is permission-gated on `od.admin.users.revoke` — hidden if lacking permission, shown-disabled-with-tooltip if partial access. Never shown-then-failed.
- `PERMISSION_GROUPS` + `PERMISSION_LABELS` frontend test asserts every backend `Permissions` constant has a label entry.
- Permissions prefetched in React Router v7 route loader before component mounts — no disabled-button flash.

---

### Epic 5c: Admin Pages & Accessibility
All Internal Admin and Tenant Admin management pages are complete and WCAG AA contrast validated. Audit log is readable. `CommandPalette` (⌘K) is available. Automated accessibility tests pass in CI. Manual pre-POC accessibility checklist is completed.

**FRs covered:** FR-20, FR-21 (all remaining management surfaces), FR-22
**UX-DRs covered:** UX-DR17, UX-DR18, UX-DR19, UX-DR20, UX-DR21, UX-DR22
**NFRs covered:** NFR-3 (UI operations ≤1s p95)
**Implementation notes:**
- Internal Admin pages: Tenants, Permissions catalog, Licenses, IDP federation config, Tenant Admin designation.
- Tenant Admin pages: Users (with SeatUsageIndicator), Groups, Roles, Role Sets, Dimension assignments, Audit Log.
- F-1 New User flow: stepper with real-time `EffectivePermissionsPanel` preview before save (diff-based POST debounced 300ms).
- F-3 Tenant provisioning: vertical stepper, validates on "Next", Review step, `unstable_useBlocker` unsaved-changes guard (F-3 only).
- `CommandPalette` TypeScript-enforced registry (`NavigationAction | EntitySearchAction | QuickAction` only).
- WCAG AA manual verification pass for 4 high-risk combinations: `amber-600` + `zinc-950` (AdminTierBanner), `indigo-300` on `zinc-800` 13px (target AAA 7:1), `red-500` on `red-950` at 11–12px (likely failure — escalate to 13px/weight-600), `amber-400` on `zinc-950` (SeatUsageIndicator).
- `@axe-core/playwright` Playwright config must include `--force-color-profile=srgb` for CI headless contrast detection.

---

### Epic 6: Federated Authentication ★ Stretch Goal ★
Tenants can configure Okta or Azure AD as an upstream IDP. Federated users authenticate through their IDP and receive OneId-managed JWTs with OneId-assigned roles (not upstream claims). IDP configs are validated before commit.

**This epic is a stretch goal — not committed POC scope.** Federation is genuine user value (dealership IT admin federates their Okta org, users stop managing another password). It is included in the POC demo script only if Epics 1–5c complete on schedule. If time is tight, federation is deferred to v1.

**FRs covered:** FR-16, FR-17
**Implementation notes:**
- `TestFederationHandler` (validates OIDC discovery URL reachable + OpenID config parseable) is story 1. Without it, Okta and Azure AD client stories require live external endpoints in CI.
- No auto-provisioning: no matching OneId user = auth failure with clear message ("No OneId account found for this identity. Contact your administrator.").
- Identity mapping: Okta → email claim; Azure AD → email or UPN claim.
- Fallback behavior flag: configurable local credential fallback if upstream IDP is unreachable.
- DevSeeder updated to include a pre-provisioned federated test user (required for Epic 6 integration test — already noted in AR-6).
- Federated user receives a OneId JWT structurally identical to a standard auth JWT. Upstream claims are not propagated.

---

**Dependency chain (updated 2026-05-23 — UI-first resequencing):**
```
[Phase 1]  Epic 5a  ← starts first, pure frontend, no backend dependency
[Phase 2]  Epic 2   ← auth backend, after shell is complete
[Phase 3]  Epic 3 (non-licensing): 3.1 → 3.2 → 3.4 → 3.6 → 3.8
[Phase 4]  Epic 4a + Epic 5b.1/5b.2/5b.6 (parallel)
[Phase 5]  ★ UI DEMO MILESTONE ★  Epic 5c: 5c.1 → 5c.3 → 5c.4 → 5c.5 → 5c.6
[Phase 6]  Epic 3 licensing: 3.3 → 3.5 → 3.7
[Phase 7]  Epic 4b: 4b.1 → 4b.2 → 4b.3
[Phase 8]  Epic 5b.3/5b.4/5b.5 + Epic 5c.2/5c.7
[Phase 9]  Epic 6 ★ stretch goal
```

Hard constraints preserved: 5b.3/5b.4/5b.5 and 5c.2 require Epic 4b (enriched introspection); 5c.1 requires Epic 4a APIs; 3.1 (ITenantContext) must precede all tenant-scoped work.

---

## Epic 1 Stories: Foundation & Dev Infrastructure

**Goal:** A developer can clone the repo and run the full system locally with a single `docker compose up`. CI pipeline is green. Observability pipeline is wired. Test infrastructure is ready.

---

### Story 1.1: Initialize Backend and Frontend Projects

As a developer,
I want both projects initialized with correct templates, dependencies, and compiler strictness configured,
So that the team has a compilable, runnable starting point with zero-warning enforcement from the first commit.

**Acceptance Criteria:**

**Given** a fresh clone of the repository
**When** `dotnet build` is run in `OneId.Server/`
**Then** the project compiles with zero errors and zero warnings
**And** `Directory.Build.props` contains `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<Nullable>enable</Nullable>` applied to all .NET projects
**And** OpenIddict 7.5.0 and the Npgsql EF Core provider are installed as NuGet references
**And** the project structure follows `dotnet new webapi -n OneId.Server --use-controllers`

**Given** a fresh clone of the repository
**When** `npm install && npm run build` is run in `OneId.Web/`
**Then** the frontend builds successfully using the Vite + React + TypeScript strict template (`npm create vite@latest OneId.Web -- --template react-ts`)
**And** `npx shadcn@latest init` has been run with the dark theme preset, outputting components to `src/components/ui/`
**And** `"strict": true` is confirmed in `tsconfig.json`

**Given** `OneId.Server` starts pointing at a running PostgreSQL instance
**When** EF Core migrations run on startup
**Then** the application starts without runtime errors and `GET /health` returns HTTP 200

**Given** the project repository (NFR-7)
**When** `DevSigningKeyStabilityTest` is examined
**Then** the test file exists at `OneId.Server.Tests/DevSigningKeyStabilityTest.cs` and is decorated with `[Fact(Skip = "Wired in Epic 2 — remove Skip when OpenIddict signing key is configured")]`
**And** the test body asserts: signing key is file-based at `keys/dev-signing.key`; after an application host restart, a token signed before restart validates successfully after restart
**And** Epic 2's story acceptance criteria must include: "Remove the `Skip` attribute from `DevSigningKeyStabilityTest` and make it pass"

---

### Story 1.2: Local Development Stack (Docker Compose)

As a developer,
I want a single Docker Compose file that starts the full local stack with health-checked services and a validated observability pipeline,
So that I can run the complete system with one command and immediately verify it is working end-to-end.

**Acceptance Criteria:**

**Given** Docker Desktop is running
**When** `docker compose up` is executed from the project root
**Then** four services start successfully: `OneId.Server`, `postgres`, `otel-collector`, `seq`
**And** `OneId.Server` passes its Docker health check (polls `GET /health`, expects HTTP 200 within 30 seconds)
**And** Seq is accessible at `http://localhost:5341`

**Given** the stack is running and `OneId.Server` handles any HTTP request
**When** a structured log event is emitted
**Then** the event appears in Seq with full structured fields (not raw text)
**And** at least one OTEL span with service name `OneId.Server` is visible in Seq — confirming data flows through the OTEL Collector to Seq, not bypassing it

**Given** the OTEL Collector configuration in `docker-compose.yml`
**When** the stack starts
**Then** `OneId.Server`'s OTEL exporter endpoint is set via an environment variable (e.g. `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317`) — not hardcoded in `appsettings.json`
**And** the collector's pipeline forwards spans to Seq

**Given** `docker compose down && docker compose up` is executed
**When** the stack restarts
**Then** PostgreSQL data persists across restarts (named volume — not anonymous)

---

### Story 1.3a: Tenant Context Middleware and Registration Order Enforcement

As a developer,
I want `ITenantContext` middleware registered and validated before any data access occurs,
So that no code path can bypass tenant isolation, even if future developers reorder middleware registration.

**Acceptance Criteria:**

**Given** `OneId.Server` starts up
**When** the DI container is built and the middleware pipeline is constructed
**Then** `ITenantContextMiddleware` is registered in `Program.cs` before OpenIddict and before EF Core `DbContext` resolution
**And** the registration is annotated with `// AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md`

**Given** any code attempts to access `ITenantContext.TenantId`
**When** `TenantContextMiddleware` has not yet executed for the current request
**Then** `ITenantContext` throws `InvalidOperationException("Tenant context not initialized — check middleware registration order in Program.cs")`
**And** a unit test in `TenantContextTests.cs` exercises this guard by calling `TenantId` on a fresh (uninitialized) `ITenantContext` instance and asserts the exception fires

**Given** `RegistrationOrderIntegrationTests.cs` runs using `WebApplicationFactory`
**When** an HTTP request is processed through the full pipeline
**Then** the test asserts `ITenantContext.TenantId` is resolvable (non-null) within the request scope — not just that the service is registered in DI
**And** a second test case in the same file moves `TenantContextMiddleware` after OpenIddict registration and asserts that an attempt to access `ITenantContext.TenantId` in a DB query throws the guard exception — proving the order enforcement has teeth

---

### Story 1.3b: EF Core Global Query Filters, Entity Stubs, and Concurrency Tokens

As a developer,
I want EF Core configured with tenant-scoped global query filters and optimistic concurrency tokens, with integration tests proving isolation,
So that cross-tenant data leakage is structurally impossible and concurrent write conflicts produce a correct HTTP 409 response.

**Acceptance Criteria:**

**Given** EF Core `AppDbContext` is configured
**When** global query filters are registered
**Then** every tenant-scoped entity has a filter on `TenantId` referencing `ITenantContext.TenantId`
**And** the filter is applied automatically — no query-site `Where(x => x.TenantId == ...)` is needed

**Given** entity stub types are defined for this epic's migration scope
**When** `UseXminAsConcurrencyToken()` is applied
**Then** it is configured on: `Tenant`, `User` — these are the only mutable entities in scope for Epic 1
**And** a comment in `AppDbContext.OnModelCreating` states: `// AR-14: UseXminAsConcurrencyToken applied to all mutable entities. Each epic that introduces a new mutable entity is responsible for adding it here.`
**And** Note: `Role`, `RoleSet`, `Group`, `Permission`, `DimensionValue` are NOT yet in scope — they are added in Epics 3 and 4a respectively

**Given** `DevSeederIntegrationTests.cs` runs
**When** a `User` record is seeded under Tenant 1
**Then** a query executed with Tenant 2's `ITenantContext` active does NOT return that user (global filter applied)
**And** the same user IS returned when Tenant 1's context is active

**Given** two concurrent requests attempt to update the same entity
**When** the second request's `xmin` value does not match the current row `xmin`
**Then** EF Core throws `DbUpdateConcurrencyException`
**And** the global exception handler (Problem Details middleware) maps this to HTTP `409 Conflict` with body: `{ "type": "https://httpstatuses.io/409", "title": "Conflict", "detail": "The resource was modified by another request. Reload and retry." }`
**And** an integration test simulates a stale-write and asserts the 409 response

---

### Story 1.4: Observability Pipeline (Serilog + OTEL + Seq)

As a developer,
I want structured logs and OTEL traces flowing through the collector to Seq with sensitive data provably absent,
So that the observability pipeline is wired on Day 1 and never retrofitted, and credentials never appear in logs.

**Acceptance Criteria:**

**Given** `OneId.Server` processes any HTTP request
**When** a structured log event is emitted
**Then** the event is enriched with: `EventType`, `TenantId` (nullable), `UserId` (nullable), `Outcome`, `TraceId`
**And** these fields are added via Serilog enrichers — no per-call-site field injection required

**Given** `SerilogDestructuringTests.cs` runs
**When** a log statement is invoked with a raw password, a `Authorization: Bearer ...` header value, or an OpenIddict client secret
**Then** the emitted log event does NOT contain the raw sensitive value
**And** the field is present but replaced with `[Redacted]`
**And** the test covers all three sensitive types explicitly

**Given** a request completes on `OneId.Server`
**When** OTEL tracing is enabled
**Then** a span with service name `OneId.Server` is exported to the OTEL Collector
**And** the Collector forwards it to Seq (pipeline validated — span visible in Seq UI)
**And** the exporter is configured to point at the Collector (not Seq directly), enforced by the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable

**Given** a developer adds a new log statement anywhere in the codebase
**When** the statement is executed
**Then** standard enriched fields are automatically present — no boilerplate required

---

### Story 1.5: Integration Test Infrastructure (Testcontainers + Respawn + TestTokenFactory)

As a developer,
I want a reusable test base with a real PostgreSQL instance, clean per-test state, and a pinned test token factory,
So that every integration test runs against real infrastructure without external dependencies or test pollution between runs.

**Acceptance Criteria:**

**Given** an integration test class extends `IntegrationTestBase`
**When** the test collection initializes (`IAsyncLifetime.InitializeAsync`)
**Then** Testcontainers starts a PostgreSQL container and applies all EF Core migrations
**And** a Respawn `Checkpoint` is created once after migrations complete (not in `DisposeAsync`)

**Given** each `[Fact]` in an integration test class is about to run
**When** per-test setup executes
**Then** `await checkpoint.ResetAsync(connectionString)` is called — restoring the database to a clean post-migration state before each test
**And** this pattern ensures no test-order-dependent failures regardless of test execution order

**Given** an integration test needs a JWT
**When** `TestTokenFactory.CreateToken(tenantId, userId, roles, seatCount)` is called
**Then** a signed JWT is returned with exactly these claims: `tid` (string), `sub` (string), `scope` ("openid"), `seat_count` (integer, default 50), `roles` (string array)
**And** claim names are snake_case: `seat_count` not `seatCount` — this is the pinned contract that Epic 3's license middleware depends on

**Given** `TestTokenFactoryContractTests.cs` exists
**When** it runs at this stage (before Epic 3 wires the production token pipeline)
**Then** it contains: `[Fact(Skip = "Wired in Epic 3 — remove Skip and make this pass in the licensing middleware story")]` with body `Assert.Fail("TestTokenFactory claim shape not yet validated against production ITokenClaimsEnricher — wire in Epic 3")`
**And** this skip is visible as a known gap in CI test reports (not silent green)
**And** Epic 3's licensing middleware story AC must include: "Remove the Skip from TestTokenFactoryContractTests.cs and make it pass against the real pipeline"

**Given** CI runs the integration test suite
**When** `dotnet test` executes
**Then** all integration tests pass using Testcontainers — no external PostgreSQL required in the CI environment

---

### Story 1.6: CI/CD Pipeline (GitHub Actions)

As a developer,
I want a GitHub Actions pipeline that validates every pull request including migration bundle correctness against a real database,
So that broken builds, failed tests, and silent data-loss migrations never reach the main branch.

**Acceptance Criteria:**

**Given** a pull request is opened or updated
**When** the CI pipeline runs
**Then** it executes these jobs in order, each gating the next: (1) `build` — `dotnet build` passes with zero warnings; (2) `test` — `dotnet test` all tests pass; (3) `migration-bundle` — `dotnet ef migrations bundle` generates without error; (4) `migration-validate` — bundle applied against a fresh Testcontainers PostgreSQL container started by this job

**Given** the `migration-validate` job runs
**When** the migration bundle is applied
**Then** a dedicated Testcontainers PostgreSQL container is started by this CI job (separate from the `test` job)
**And** the bundle is applied against it as an executable artifact — a rename-as-drop-add migration that compiles but destroys data will fail this step
**And** the job reports success only if the bundle applies and the schema is queryable after apply

**Given** the `playwright-tests` job in the CI workflow
**When** the workflow runs today (Epic 1 — no Playwright tests exist)
**Then** the job is defined in the workflow file with `if: false` — it is skipped without error
**And** `playwright.config.ts` is committed with `use: { launchOptions: { args: ["--force-color-profile=srgb"] } }` — pre-configured for CI contrast detection when Epic 5a enables the job

---

### Story 1.7a: ArchUnit Boundary Enforcement and Cache Abstraction

As a developer,
I want namespace boundary rules and a cache abstraction enforced from day one,
So that architectural constraints are machine-checked and no service bypasses the caching contract.

**Acceptance Criteria:**

**Given** `InternalBoundaryTests.cs` runs (ArchUnit)
**When** any type in the `Application/Internal/` namespace is referenced
**Then** the ArchUnit rule asserts it is NOT injected or instantiated by any type outside `Application/Internal/`
**And** the rule is expressed as an ArchUnit fluent API assertion (not a convention test or manual scan)
**And** a deliberate violation test case — injecting an `Application/Internal/` type into a controller in `Application/Tenant/` — causes the assertion to fail, proving the rule has teeth

**Given** any application service needs to cache a value
**When** it accesses the cache
**Then** it uses `ICacheService` only — no direct `IMemoryCache` injection is permitted outside `Infrastructure/Caching/`
**And** this is enforced by an ArchUnit rule: "types outside `Infrastructure/Caching/` MUST NOT depend on `IMemoryCache` directly"
**And** `ICacheService` is implemented by `MemoryCacheService` using cache key format: `{entity}:{userId}:{tenantId}` (e.g., `user:abc123:tenant456`)

---

### Story 1.7b: DevSeeder and Permission Catalog Stub

As a developer,
I want seed data and the Permission catalog skeleton active from day one,
So that the development environment is immediately usable and the Epic 4a wiring point is visible in CI.

**Acceptance Criteria:**

**Given** `OneId.Server` starts in the `Development` environment
**When** the DevSeeder executes (after EF Core global query filters are confirmed active per Story 1.3b)
**Then** the following seed data is present: (1) dev tenant (`id: dev-tenant`, `name: "Dev Tenant"`), (2) admin user (`email: admin@oneid.dev`, hashed password for `Admin123!`), (3) OpenIddict test client (`client_id: oneid-dev-client`, `redirect_uri: http://localhost:3000/callback`)
**And** DevSeeder is idempotent — running twice does not create duplicate records (uses find-or-create, not blind insert)
**And** Note: the pre-provisioned federated test user is NOT seeded here — it is deferred to Epic 6 Story 6.1, which is a stretch goal

**Given** `PermissionCatalog.cs` and `Permissions` static class exist
**When** the solution compiles
**Then** `Permissions` class compiles with zero constants (populated in Epic 4a)
**And** `PermissionCatalogSyncTests.cs` contains: `[Fact(Skip = "Wired in Epic 4a — remove Skip in Story 4a.1")]` with body `Assert.Fail("PermissionCatalog sync not yet enforced — wire in Epic 4a")`
**And** this skip is visible as a known gap in CI test reports (counts toward AR-15 deferred-skip cap)

---

## Epic 2 Stories: Working Authentication

**Goal:** An end user can authenticate with email/password + TOTP, receive a signed JWT with standard OIDC claims, introspect it, and reset their password. OpenIddict extensibility is validated end-to-end (custom claim wiring, enriched introspection, jti revocation). OIDC discovery endpoint is live and accurate.

---

### Story 2.1: OpenIddict Configuration and OIDC Discovery

As a developer,
I want OpenIddict configured with a stable RS256 signing key, active OIDC discovery, and the authorization and token endpoints registered,
So that all subsequent authentication stories have a verified cryptographic foundation and the OIDC spec compliance check is green from day one.

**Acceptance Criteria:**

**Given** `DevSigningKeyStabilityTest.cs` contains a `[Fact(Skip = "...")]` from Story 1.1
**When** this story is implemented
**Then** the `Skip` attribute is removed from `DevSigningKeyStabilityTest` and the test passes: a token signed before a `WebApplicationFactory` restart validates successfully after restart
**And** the signing key is file-based at `keys/dev-signing.key` and the file is excluded from `.gitignore`

**Given** `OneId.Server` starts
**When** a `GET /.well-known/openid-configuration` request is sent
**Then** the response is HTTP 200 with `Content-Type: application/json`
**And** the payload contains: `issuer`, `authorization_endpoint`, `token_endpoint`, `jwks_uri`, `introspection_endpoint`, `scopes_supported` (includes `openid`), `response_types_supported` (includes `code`), `token_endpoint_auth_methods_supported`
**And** `GET {jwks_uri}` returns an RS256 public key as a JWKS JSON document

**Given** OpenIddict is configured
**When** the DI container is built
**Then** Authorization Code Flow with PKCE and Client Credentials Flow are both enabled
**And** token refresh is enabled (refresh token lifetime is long-lived — minimum 7 days in dev config)
**And** an integration test in `OpenIddictConfigurationTests.cs` asserts that OpenIddict is registered after `ITenantContextMiddleware` in `Program.cs` (satisfies AR-5 ordering)

---

### Story 2.2: Password Authentication and Account Lockout

As an end user,
I want to authenticate with my email and password through the token endpoint,
So that I receive an authorization code (or appropriate error) that enables the OIDC flow to continue.

**Acceptance Criteria:**

**Given** a `POST /connect/token` request with valid `grant_type=password`, registered email, and correct password
**When** the request is processed
**Then** the response contains an `access_token` (JWT), `token_type: "Bearer"`, `expires_in`, and `refresh_token`
**And** the JWT `sub` claim matches the user's ID

**Given** a `POST /connect/token` request with a valid email but incorrect password
**When** the request is processed
**Then** the response is HTTP 400 with `error: "invalid_grant"`
**And** the error body does NOT reveal whether the email exists — response is identical for unknown email and wrong password (no username enumeration)

**Given** a user makes 5 consecutive failed authentication attempts within the lockout window
**When** the 5th failure is recorded
**Then** the account is locked out (ASP.NET Core Identity lockout applied)
**And** subsequent attempts return HTTP 400 with `error: "invalid_grant"` — no lockout ETA disclosed in the response body
**And** a `LockoutTriggeredIntegrationTest` in `PasswordAuthTests.cs` simulates 5 failures and asserts the lockout state via the Identity user store — not just the HTTP response

**Given** ASP.NET Core Identity is configured
**When** the `AppDbContext` is seeded with the dev admin user from Story 1.7
**Then** the password is stored as an Argon2id hash (Identity's default hasher for .NET 8 + Identity 2.0)
**And** `SerilogDestructuringTests.cs` already covers password field redaction — no raw password appears in any log event for this authentication path

---

### Story 2.3: TOTP MFA Enrollment and Challenge

As an end user,
I want to enroll in TOTP on my first login and be challenged on every subsequent login,
So that my account requires two factors before any token is issued.

**Acceptance Criteria:**

**Given** a user has correct email/password but has not yet enrolled in TOTP
**When** the password factor is verified
**Then** the response is HTTP 200 with a `mfa_required: true` flag and a `totp_enrollment_uri` (otpauth:// URI compatible with Google Authenticator / Authy)
**And** no `access_token` is issued — the authentication flow is suspended at the MFA gate

**Given** a user submits a valid TOTP code during enrollment
**When** the enrollment is confirmed
**Then** the TOTP secret is stored (encrypted at rest) and `totp_enrolled: true` is set on the user record
**And** an `access_token` is issued for this initial login after successful enrollment

**Given** a user has TOTP enrolled and provides correct email/password
**When** the password factor is verified
**Then** the response indicates a TOTP challenge is required (no token issued yet)
**And** submitting a valid current TOTP code completes authentication and issues a token
**And** submitting an incorrect TOTP code returns HTTP 400 with `error: "invalid_grant"` — no TOTP enrollment URI, no brute-force timing hints

**Given** a TOTP code has already been used
**When** the same code is submitted again within its 30-second validity window
**Then** the authentication fails (replay prevention — one-time-use enforcement)
**And** `TotpMfaIntegrationTests.cs` covers: enrollment, valid challenge, invalid challenge, and replay prevention

---

### Story 2.4: JWT Issuance with ITokenClaimsEnricher Pipeline

As a developer,
I want a formal `ITokenClaimsEnricher` pipeline wired into OpenIddict token issuance,
So that Epic 4b can add authorization claims additively without touching token issuance code, and the issuance performance budget is verified.

**Acceptance Criteria:**

**Given** `ITokenClaimsEnricher` is defined
**When** the solution compiles
**Then** the interface is in `Application/TokenPipeline/ITokenClaimsEnricher.cs` with signature: `Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)`
**And** `TokenEnrichmentContext` carries at minimum: `UserId`, `TenantId`, `GrantType`
**And** an `EnricherPipelineOrderTest` registers two `ITokenClaimsEnricher` stubs in a known sequence: `StubEnricherA` (adds claim `test-marker-a` to the identity) followed by `StubEnricherB` (asserts `test-marker-a` is already present before adding `test-marker-b`); the test issues a token and asserts both claims are present, confirming A ran before B

**Given** a token is issued via `POST /connect/token`
**When** the `ITokenClaimsEnricher` pipeline runs
**Then** the resulting JWT contains: `sub` (user ID), `iss`, `aud`, `exp`, `iat`, `jti`, and `roles` (array of role name strings assigned to the user)
**And** `roles` is populated by the first `ITokenClaimsEnricher` stage (`RoleClaimsEnricher`) — this is the only stage in Epic 2
**And** a `JwtClaimsIntegrationTest` decodes the issued JWT and asserts all required claims are present with correct types

**Given** `TokenIssuancePerformanceTests.cs` runs
**When** token issuance is measured under test load (single-threaded sequential calls)
**Then** 95th percentile issuance time is under 400ms (100ms headroom against the NFR-2 500ms gate)
**And** the test uses a `Stopwatch` per issuance call and fails if the p95 exceeds the ceiling

---

### Story 2.5: Token Introspection and jti Revocation Store

As a resource server (OneDealer v2),
I want a `/connect/introspect` endpoint that validates active tokens and a server-side jti store that supports revocation,
So that token validity can be checked at the data layer and compromised tokens can be immediately invalidated.

**Acceptance Criteria:**

**Given** a valid, non-revoked JWT is submitted to `POST /connect/introspect` with valid client credentials
**When** introspection is processed
**Then** the response contains `active: true`, `sub`, `exp`, `jti`, `iss`, `aud`, and `scope`
**And** the jti is confirmed present in the server-side jti store (OpenIddict's authorization record)

**Given** a JWT whose jti has been revoked (removed from the jti store)
**When** it is submitted to `/connect/introspect`
**Then** the response contains `active: false` — no other claims present
**And** a `JtiRevocationIntegrationTest` issues a token, revokes its jti directly in the store, and asserts the introspection returns `active: false`

**Given** an expired JWT is submitted to `/connect/introspect`
**When** the introspection handler runs
**Then** the response contains `active: false`

**Given** `IntrospectionPerformanceTests.cs` runs
**When** introspection is measured
**Then** 95th percentile response time (excluding network) is under 50ms — satisfying NFR-4
**And** the test uses a `Stopwatch` per introspection call and fails if p95 exceeds the ceiling

**Story Notes:**
- `TestTokenFactoryContractTests.cs` contains a `[Fact(Skip = "Wired in Epic 3 — remove Skip in Story 3.5")]` that is NOT removed in this story. It remains deferred to Story 3.5 (Seat Limit Enforcement at Token Issuance), which is when the full licensing middleware is wired. This is intentional and tracked under AR-15 (deferred-skip governance).

---

### Story 2.6: Role-Change jti Invalidation (FR-5a)

As an Internal Admin or Tenant Admin,
I want active tokens invalidated immediately when a user's role assignments change,
So that permission changes take effect within the 5-minute consumer cache window — not only on the next login.

**Acceptance Criteria:**

**Given** a `UserRoleInvalidationService` (or equivalent) is implemented
**When** a user's role assignment changes (role added or removed)
**Then** all active jti records for that user are revoked in the OpenIddict authorization store
**And** a subsequent introspection call for any of those tokens returns `active: false`

**Given** `RoleChangeInvalidationTests.cs` runs
**When** a test token is issued for a user, their role is changed, and the token is introspected
**Then** the introspection returns `active: false`
**And** this test uses a test tenant context and a `TestTokenFactory`-issued token — the full cross-tenant integration test with `ITenantContext` middleware active is written in Epic 3

**Given** no role assignment change occurs
**When** an active token is introspected within its TTL
**Then** introspection returns `active: true` — the invalidation mechanism does not affect unrelated tokens

---

### Story 2.7: Password Reset via Email Link

As an end user,
I want to reset my password via a time-limited email link without contacting an admin,
So that I can regain account access independently while keeping the process secure.

**Acceptance Criteria:**

**Given** a `POST /account/forgot-password` request with a registered email
**When** the request is processed
**Then** a password reset token (1-hour expiry) is generated and stored server-side
**And** an email is sent to that address containing a reset link with the token as a query parameter
**And** the response is HTTP 202 — identical response for unknown email (no user enumeration)

**Given** a valid, non-expired reset token is submitted to `POST /account/reset-password` with a new password
**When** the request is processed
**Then** the password is updated and stored as an Argon2id hash
**And** the reset token is consumed (single-use) — a second use of the same token returns HTTP 400
**And** the previous password cannot be set as the new password (one-step no-reuse check)
**And** all active jti records for the user are revoked (password reset is a security event)

**Given** a reset token older than 1 hour is submitted
**When** the reset is attempted
**Then** the response is HTTP 400 with `error: "invalid_or_expired_token"` — same message as a completely invalid token

**Given** the password reset UI
**When** a user navigates to `/forgot-password`
**Then** a form renders with a single email input and a submit button
**And** on submit, a confirmation message appears ("If this email is registered, you will receive a reset link")
**And** the reset link (`/reset-password?token=...`) renders a new-password form with password + confirm-password fields
**And** on successful reset the user is redirected to `/login` with a success toast
**And** design system polish is explicitly out of scope — functional loop closes here; visual polish is deferred to Epic 5

---

### Story 2.8: Refresh Token Rotation and SPA Token Lifecycle

As a user of the React management console,
I want my session to stay active while I am working without re-authenticating every 15 minutes,
So that short access token lifetimes do not disrupt my workflow.

**Architecture decision (documented here):** Access tokens and refresh tokens are stored in JavaScript memory only (no `localStorage`, no `sessionStorage`, no cookies). A page reload clears all tokens — the user must re-authenticate. This is the accepted trade-off for security; a BFF with httpOnly cookie refresh token storage is a post-POC upgrade path.

**Acceptance Criteria:**

**Given** OpenIddict is configured for refresh token issuance
**When** a developer inspects the OpenIddict server options
**Then** `AllowRefreshTokenFlow()` is enabled and refresh token rotation is active — each use of a refresh token issues a new access token AND a new refresh token; the used refresh token is immediately invalidated
**And** access token lifetime is 15 minutes; refresh token sliding expiry is 8 hours with an absolute ceiling of 24 hours
**And** these values are configurable via `appsettings.json` — not hardcoded

**Given** a user authenticates successfully via PKCE
**When** the SPA receives the token response
**Then** the access token and refresh token are stored in JavaScript module-scope memory (not persisted to any browser storage)
**And** the SPA axios instance has a request interceptor that attaches the access token as `Authorization: Bearer {token}`

**Given** the SPA makes an API request and the access token has expired
**When** the API returns HTTP 401
**Then** the SPA axios response interceptor automatically calls `POST /connect/token` with `grant_type=refresh_token` and the stored refresh token
**And** on a successful token response, the interceptor stores the new access and refresh tokens in memory and retries the original request transparently — the calling component receives the successful response with no awareness of the token refresh
**And** the transparent retry happens at most once per original request — a second 401 after retry does NOT trigger another refresh attempt (prevents infinite loops)

**Given** a `RefreshTokenRotationIntegrationTest` runs
**When** it calls `POST /connect/token` with `grant_type=refresh_token` using a valid refresh token
**Then** the response contains a new `access_token` and a new `refresh_token`
**And** replaying the original (consumed) refresh token returns HTTP 400 with `error: "invalid_grant"`
**And** the new access token passes signature validation (RS256, correct issuer and audience)

---

### Story 2.9: Session Expiry UX

As a user of the React management console,
I want to see a clear, friendly message when my session has expired,
So that I do not think the application has crashed when I am returned to the login screen.

**Acceptance Criteria:**

**Given** a user reloads the browser tab or navigates directly to a protected route
**When** no access token exists in memory (tokens cleared by reload or session expiry)
**Then** the user is redirected to `/login` without error
**And** the original URL is preserved as a `?returnTo=` query parameter on the `/login` route

**Given** a user is redirected to `/login` with a `returnTo` parameter that was set because of session expiry (as opposed to a first visit)
**When** the login page renders
**Then** a non-blocking informational banner displays: "Your session has expired. Please sign in again."
**And** the banner is distinct from an error state — it uses an informational style (not red/destructive)
**And** the banner is announced by screen readers (`role="status"`)

**Given** a user successfully re-authenticates after being returned to `/login` with a `returnTo` parameter
**When** authentication completes
**Then** the user is redirected to the original `returnTo` URL
**And** if `returnTo` is absent or points outside the application origin, the user is redirected to the default post-login route (no open redirect)

**Given** the SPA axios interceptor fails to refresh the token (refresh token also expired or revoked — `POST /connect/token` returns HTTP 400)
**When** this happens mid-session (not on page load)
**Then** all in-memory tokens are cleared
**And** the user is redirected to `/login?returnTo={currentPath}` with the session expiry banner
**And** any in-progress form state is not forcibly discarded — the returnTo redirect gives the user a chance to re-authenticate and return

---

## Epic 3 Stories: Tenant & License Governance

**Goal:** An Internal Admin can provision Tenants, assign seat-count Licenses, designate Tenant Admins, and suspend Tenants. Seat limits are enforced at token issuance. Tenant data isolation is enforced at the EF Core data layer. A Tenant Admin can log in and view their tenant's license and seat usage summary.

---

### Story 3.1: ITenantContext Middleware and Tenant Isolation Regression Tests

As a developer,
I want `ITenantContext` middleware fully active and a regression test suite proving data-layer isolation,
So that cross-tenant data leakage is structurally impossible and the isolation guarantee is machine-verified from this epic forward.

**Acceptance Criteria:**

**Given** `ITenantContextMiddleware` was registered in Story 1.3a
**When** this story completes
**Then** middleware is confirmed resolving `TenantId` from the authenticated JWT's `tid` claim — not from a header or query parameter
**And** a `TenantResolutionIntegrationTest` asserts: a request with a valid JWT containing `tid: tenant-A` results in `ITenantContext.TenantId == "tenant-A"` within the request scope
**And** an unauthenticated request to a tenant-scoped endpoint returns HTTP 401 — `ITenantContext.TenantId` is never resolved from an unauthenticated source

**Given** `TenantIsolationRegressionTests.cs` is introduced in this story
**When** it runs
**Then** it contains at minimum these three test cases:
(1) A `User` created under Tenant A is NOT returned by a query executing with Tenant B's `ITenantContext` active
(2) The same `User` IS returned when Tenant A's context is active
(3) A direct `DbContext` query without any `ITenantContext` active throws `InvalidOperationException` — the guard from Story 1.3a fires
**And** this test file is extended in Epic 4a when Role/Group/Permission entities are added — the test class is designed for extension (base class or shared fixture)

**Given** `TestTokenFactoryContractTests.cs` contains a `[Fact(Skip = "Wired in Epic 3 ...")]`
**When** the licensing middleware story in this epic is complete (Story 3.5)
**Then** the Skip is removed and the test passes against the real `ITokenClaimsEnricher` pipeline — this is the explicit gate noted in Story 1.5 and Story 2.5

---

### Story 3.2: Tenant CRUD (Internal Admin)

As an Internal Admin,
I want to create, read, update, and deactivate Tenants via the management API,
So that I can provision and manage the organizations that use OneId.

**Acceptance Criteria:**

**Given** an authenticated Internal Admin calls `POST /api/internal/tenants`
**When** the request body contains a valid tenant name and slug
**Then** a new `Tenant` record is created with `Status: Active`, a generated `id`, and a `createdAt` timestamp
**And** the response is HTTP 201 with the created tenant's full representation
**And** `UseXminAsConcurrencyToken()` is confirmed applied to the `Tenant` entity (inherits from Story 1.3b's pattern)

**Given** an Internal Admin calls `GET /api/internal/tenants`
**When** the request is processed
**Then** all tenants are returned (Internal Admin is not scoped by the global query filter — the filter is bypassed for Internal Admin context)
**And** a `TenantListIntegrationTest` asserts Tenant A and Tenant B are both visible to an Internal Admin but only Tenant A is visible to a Tenant A Admin

**Given** an Internal Admin calls `PUT /api/internal/tenants/{id}`
**When** the request body contains updated fields
**Then** the tenant record is updated and the response is HTTP 200 with the updated representation
**And** a concurrent update attempt with a stale `xmin` returns HTTP 409 Conflict (AR-14)

**Given** an Internal Admin calls `DELETE /api/internal/tenants/{id}` (soft delete / deactivate)
**When** the request is processed
**Then** the tenant `Status` is set to `Inactive` — the record is NOT physically deleted
**And** subsequent token issuance for users in that tenant is rejected (active status check at issuance)
**And** `InternalBoundaryTests.cs` ArchUnit rule update: `/api/internal/` endpoints are confirmed only accessible via `Application/Internal/` namespace types

---

### Story 3.3: Seat-Count License Management (Internal Admin)

As an Internal Admin,
I want to create and update a seat-count License for a Tenant,
So that I can control how many active users each Tenant is allowed.

**Acceptance Criteria:**

**Given** an Internal Admin calls `POST /api/internal/tenants/{id}/license`
**When** the request body contains `model: "seat_count"` and `maxSeats: N` and `effectiveDate`
**Then** a `License` record is created linked to the tenant
**And** the response is HTTP 201 with the full license representation: `tenantId`, `model`, `maxSeats`, `effectiveDate`, `parameters` (empty object for seat_count model)
**And** a second `POST` for the same tenant returns HTTP 409 — a Tenant may have at most one active License

**Given** a License exists for a Tenant
**When** an Internal Admin calls `PUT /api/internal/tenants/{id}/license` to update `maxSeats`
**Then** the License record is updated and the response is HTTP 200
**And** the `parameters` JSON field is preserved unmodified if not included in the update body (extensibility — future concurrent/usage-based models store their config here without schema migration)

**Given** `LicenseExtensibilityTest` runs
**When** a license is created with `model: "seat_count"` and `parameters: { "overage_allowed": false }`
**Then** the record round-trips correctly — `parameters` is stored as JSON and returned verbatim
**And** no application code inspects or validates the contents of `parameters` for non-`seat_count` models (future-proofing, FR-19)

---

### Story 3.4: Tenant Admin Designation (Internal Admin)

As an Internal Admin,
I want to designate one or more users within a Tenant as Tenant Admins,
So that those users can manage their Tenant's configuration without Internal Admin involvement.

**Acceptance Criteria:**

**Given** an Internal Admin calls `POST /api/internal/tenants/{tenantId}/admins/{userId}`
**When** the request is processed
**Then** the target user is granted the Tenant Admin role within that Tenant
**And** the response is HTTP 200 with the updated user representation showing `isTenantAdmin: true`
**And** the user must already exist within the specified Tenant — attempting to designate a user from a different Tenant returns HTTP 404 (tenant isolation enforced, not 403 — no cross-tenant existence disclosure)

**Given** an Internal Admin calls `DELETE /api/internal/tenants/{tenantId}/admins/{userId}`
**When** the request is processed
**Then** the Tenant Admin role is removed from the user
**And** removing the last Tenant Admin from a Tenant returns HTTP 409 with `error: "last_tenant_admin"` — a Tenant must retain at least one Admin

**Given** a user is designated as Tenant Admin
**When** they authenticate and receive a JWT
**Then** their `roles` claim includes `"TenantAdmin"`
**And** a `TenantAdminDesignationIntegrationTest` verifies the role appears in the JWT after designation and is absent after removal

---

### Story 3.5: Seat Limit Enforcement at Token Issuance (FR-18)

As an Internal Admin who has set a seat limit,
I want token issuance to enforce the licensed seat count,
So that Tenants cannot exceed their contracted user limit.

**Acceptance Criteria:**

**Given** a Tenant has a License with `maxSeats: 5` and 4 active users
**When** a 5th user authenticates
**Then** token issuance succeeds (under limit) and the JWT is issued normally

**Given** a Tenant has a License with `maxSeats: 5` and 5 active users already holding active sessions
**When** a new (6th) user attempts to authenticate
**Then** token issuance is denied with HTTP 400 and `error: "seat_limit_reached"`
**And** the error message references the Tenant Admin: `"detail": "This organization has reached its user limit. Contact your administrator to expand the license."`
**And** `SeatLimitEnforcementTests.cs` covers: under-limit success, at-limit denial, and the boundary case of exactly-at-limit

**Given** a Tenant's `maxSeats` is lowered retroactively below the current active user count
**When** an existing user (who was previously within the limit) authenticates after the limit change
**Then** token issuance is denied with the same `seat_limit_reached` error — retroactive enforcement applies at next token issuance, not immediately
**And** this boundary case is covered by `SeatLimitRetrospectiveEnforcementTest`

**Given** a Tenant has no License record
**When** a user from that Tenant authenticates
**Then** token issuance succeeds without seat checking — no License means no limit (Internal Admin must explicitly set a limit)

**Given** `TestTokenFactoryContractTests.cs` Skip is removed in this story
**When** the test runs
**Then** a token produced by `TestTokenFactory.CreateToken(...)` passes validation through the real `ITokenClaimsEnricher` pipeline (satisfies the gate from Story 1.5)
**And** the `seat_count` claim in `TestTokenFactory` tokens matches the format the licensing middleware reads from the production pipeline

---

### Story 3.6: Tenant Suspension with jti Revocation (FR-12)

As an Internal Admin,
I want to suspend a Tenant and immediately invalidate all active sessions for that Tenant's users,
So that a suspended Tenant's users lose access within the introspection cache window.

**Acceptance Criteria:**

**Given** an Internal Admin calls `POST /api/internal/tenants/{id}/suspend`
**When** the request is processed
**Then** the Tenant `Status` is set to `Suspended`
**And** all active jti records for all Users in that Tenant are revoked in the OpenIddict authorization store (integrates with jti revocation store from Story 2.5)
**And** the response is HTTP 200 with the updated tenant representation

**Given** a Tenant is suspended and a user from that Tenant attempts to use their existing token
**When** the token is introspected by OneDealer v2
**Then** introspection returns `active: false` (jti revoked)
**And** new token issuance is rejected with HTTP 400 `error: "tenant_suspended"` until the Tenant is reinstated

**Given** a `TenantSuspensionIntegrationTest` runs
**When** a Tenant is suspended after issuing tokens to 3 users
**Then** all 3 tokens introspect as `active: false`
**And** this test is the full cross-tenant integration test for FR-5a that was deferred from Story 2.6 — it uses the real `ITenantContext` middleware and the jti revocation store together

**Given** an Internal Admin calls `POST /api/internal/tenants/{id}/reinstate`
**When** the request is processed
**Then** the Tenant `Status` returns to `Active`
**And** new token issuance is permitted again (previously revoked jtis remain revoked — users must re-authenticate)

---

### Story 3.7: Tenant Admin License View

As a Tenant Admin,
I want to view my Tenant's current license and seat usage after logging in,
So that I can understand my organization's capacity at a glance — even before management features are available.

**Acceptance Criteria:**

**Given** a Tenant Admin authenticates successfully
**When** they call `GET /api/tenant/license`
**Then** the response contains: `model` (`"seat_count"`), `maxSeats`, `seatsUsed` (count of non-deactivated users), `effectiveDate`
**And** this endpoint is scoped to the Tenant Admin's Tenant via `ITenantContext` — it cannot return another Tenant's license data

**Given** a Tenant has no License record
**When** a Tenant Admin calls `GET /api/tenant/license`
**Then** the response is HTTP 200 with `{ "model": null, "maxSeats": null, "seatsUsed": N, "effectiveDate": null }` — not 404
**And** `seatsUsed` is always populated regardless of license presence

**Given** `TenantAdminLicenseViewIntegrationTest` runs
**When** a Tenant Admin token (from `TestTokenFactory` with `roles: ["TenantAdmin"]`) calls the endpoint
**Then** it returns only the license for the Tenant in the token's `tid` claim — a different `tid` in the token returns that Tenant's license (isolation verified)
**And** an Internal Admin token calling this endpoint returns HTTP 403 (this is a Tenant Admin endpoint, not an Internal Admin one)

---

### Story 3.8: Audit Log Infrastructure

As a Tenant Admin,
I want mutations to users, roles, and groups within my Tenant to be recorded automatically,
So that I can review a chronological history of administrative changes for compliance and troubleshooting.

**Acceptance Criteria:**

**Given** the `AuditLog` entity is defined
**When** a developer inspects the entity shape
**Then** it has fields: `Id` (UUID), `TenantId` (UUID, non-nullable), `ActorUserId` (UUID, nullable — null for system events), `Action` (string, e.g. `"user.created"`), `EntityType` (string), `EntityId` (UUID), `Payload` (JSONB), `Timestamp` (UTC, non-nullable)
**And** the entity is append-only — no `UpdatedAt`, no `IsDeleted`, no `UseXminAsConcurrencyToken()` on this entity
**And** EF Core global query filters apply `TenantId` isolation to `AuditLog` reads

**Given** `IAuditLogRepository` is defined in `Application/`
**When** a developer inspects the interface
**Then** it exposes exactly two methods: `AppendAsync(AuditLogEntry entry, CancellationToken ct)` and `QueryAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)`
**And** `AppendAsync` does not throw on duplicate `Id` — it is idempotent (safe to retry on transient failure)

**Given** `AuditLogService` wraps `IAuditLogRepository`
**When** `AppendAsync` is called
**Then** `AuditLogService` validates that the `entry.TenantId` matches the current `ITenantContext.TenantId` before delegating to the repository
**And** a cross-tenant `TenantId` on the entry throws `InvalidOperationException` — audit entries cannot be written outside the caller's tenant scope

**Given** a Tenant Admin calls `GET /api/tenant/audit`
**When** the request includes optional query params `?page=1&pageSize=25`
**Then** the response is HTTP 200 with `{ "items": [...], "page": 1, "pageSize": 25, "totalCount": N }`
**And** each item contains all `AuditLog` fields except raw `Payload` is returned as an opaque JSON object
**And** results are ordered by `Timestamp` descending
**And** the endpoint is secured — a missing or non-`TenantAdmin` JWT returns HTTP 401/403

**Given** `TenantService` (from Story 3.2) creates or updates a Tenant
**When** the mutation completes successfully
**Then** `AuditLogService.AppendAsync` is called with `Action: "tenant.created"` or `"tenant.updated"`, `EntityType: "Tenant"`, `EntityId: tenant.Id`, and a `Payload` containing the changed fields
**And** a unit test on `TenantService` verifies `AppendAsync` is called with the correct `Action` on create and update

**Given** `AuditLogInfrastructureIntegrationTest` runs
**When** a Tenant Admin token (from `TestTokenFactory`) creates a Tenant via `TenantService` and then calls `GET /api/tenant/audit`
**Then** the response contains exactly the audit entry generated by the creation
**And** a second Tenant's audit entries are NOT returned — TenantId isolation is enforced at the query level
**And** pagination returns the correct subset when `totalCount > pageSize`

---

## Epic 4a Stories: Authorization Data Model

**Goal:** A Tenant Admin can manage the full authorization structure: global Permission catalog, Roles, Role Sets, Groups, Dimensional Attribute reference lists, and per-user Dimension assignments. All data model entities are in place with correct tenant isolation and concurrency tokens.

---

### Story 4a.1: Permission Catalog (Internal Admin)

As an Internal Admin,
I want to manage a global Permission catalog with dot-notation string identifiers,
So that all Roles across all Tenants reference a single authoritative set of Permissions.

**Acceptance Criteria:**

**Given** `PermissionCatalog.cs` and the `Permissions` static class exist (stubbed in Story 1.7)
**When** this story is implemented
**Then** `PermissionCatalog.cs` is populated with the initial `od.*` permission set covering all management surfaces defined in FR-6 through FR-22
**And** every permission ID follows dot-notation (e.g., `od.crm.invoice.create`) and is unique across the catalog
**And** the `Permissions` static class exposes a `const string` for every permission ID — no inline string literals for permission IDs anywhere in application code

**Given** `PermissionCatalogSyncTests.cs` contains a `[Fact(Skip = "Wired in Epic 4a — remove Skip in Story 4a.1")]`
**When** this story is implemented
**Then** the Skip is removed and the test passes: every `const string` in the `Permissions` class has a corresponding seed row in the `Permission` DB table
**And** the test fails if a constant is added to `Permissions` without a matching seed row (sync is machine-enforced)

**Given** an Internal Admin calls `POST /api/internal/permissions`
**When** the request body contains a valid `id` (dot-notation) and `description`
**Then** a new Permission is created with `Status: Active`
**And** a duplicate `id` returns HTTP 409

**Given** an Internal Admin calls `DELETE /api/internal/permissions/{id}` (deactivate)
**When** the request is processed
**Then** the Permission `Status` is set to `Inactive` — the record is NOT physically deleted
**And** the deactivated Permission is no longer included in future token issuances
**And** `UseXminAsConcurrencyToken()` is applied to the `Permission` entity
**And** `TenantIsolationRegressionTests.cs` is extended: Permission records (global, not tenant-scoped) are readable by all authenticated Internal Admins regardless of tenant context

---

### Story 4a.2: Role Management (Tenant Admin)

As a Tenant Admin,
I want to create, read, update, and delete Roles within my Tenant,
So that I can define named sets of Permissions that reflect my organization's job functions.

**Acceptance Criteria:**

**Given** a Tenant Admin calls `POST /api/tenant/roles`
**When** the request body contains a `name` and an array of `permissionIds` referencing active `od.*` constants
**Then** a `Role` record is created scoped to the Tenant Admin's Tenant
**And** the response is HTTP 201 with the created role including resolved permission details
**And** referencing a non-existent or `Inactive` permission ID returns HTTP 422 with a field-level error identifying the invalid ID

**Given** a Tenant Admin calls `GET /api/tenant/roles`
**When** the request is processed
**Then** only Roles belonging to the Tenant Admin's Tenant are returned (global query filter enforced)
**And** `TenantIsolationRegressionTests.cs` is extended: Role from Tenant A is NOT visible under Tenant B's context

**Given** a Tenant Admin calls `DELETE /api/tenant/roles/{id}`
**When** the Role is currently assigned to one or more Groups
**Then** the deletion is rejected with HTTP 409 and `error: "role_in_use"` listing the Group names
**And** deleting an unassigned Role succeeds with HTTP 204 and the record is physically deleted

**Given** a Role is updated via `PUT /api/tenant/roles/{id}`
**When** the update modifies the permission set
**Then** the Role's permission references are updated atomically
**And** a stale-`xmin` concurrent update returns HTTP 409 (AR-14)
**And** `UseXminAsConcurrencyToken()` is confirmed applied to the `Role` entity

---

### Story 4a.3: Role Set Management (Tenant Admin)

As a Tenant Admin,
I want to create, read, update, and delete Role Sets within my Tenant,
So that I can bundle multiple Roles for bulk assignment to Groups.

**Acceptance Criteria:**

**Given** a Tenant Admin calls `POST /api/tenant/role-sets`
**When** the request body contains a `name` and an array of `roleIds`
**Then** a `RoleSet` record is created scoped to the Tenant Admin's Tenant
**And** all referenced `roleIds` must belong to the same Tenant — a cross-tenant role ID returns HTTP 422
**And** the response is HTTP 201 with the created role set including inline Role summaries

**Given** a Tenant Admin calls `DELETE /api/tenant/role-sets/{id}`
**When** the Role Set is currently assigned to one or more Groups
**Then** the deletion is rejected with HTTP 409 and `error: "role_set_in_use"` listing the Group names
**And** deleting an unassigned Role Set succeeds with HTTP 204

**Given** a Role Set is updated via `PUT /api/tenant/role-sets/{id}`
**When** the update modifies the roles list
**Then** the Role Set's role references are updated atomically
**And** a stale-`xmin` concurrent update returns HTTP 409 (AR-14)
**And** `UseXminAsConcurrencyToken()` is confirmed applied to the `RoleSet` entity
**And** `TenantIsolationRegressionTests.cs` is extended: a RoleSet from Tenant A is NOT visible under Tenant B's context

---

### Story 4a.4: Group Management (Tenant Admin)

As a Tenant Admin,
I want to create, read, update, and delete Groups and assign Roles and Role Sets to them,
So that I can organize users by job function and manage their permissions in bulk.

**Acceptance Criteria:**

**Given** a Tenant Admin calls `POST /api/tenant/groups`
**When** the request body contains a `name`, optional `roleIds`, and optional `roleSetIds`
**Then** a `Group` record is created scoped to the Tenant Admin's Tenant
**And** all referenced `roleIds` and `roleSetIds` must belong to the same Tenant — cross-tenant references return HTTP 422
**And** the response is HTTP 201 with the created Group including inline Role and RoleSet summaries

**Given** a Tenant Admin calls `PUT /api/tenant/groups/{id}/members`
**When** the request body contains a `userId`
**Then** the User is added to the Group (only if the User belongs to the same Tenant — cross-tenant User ID returns HTTP 404)
**And** adding a User who is already a member is idempotent — HTTP 200, no duplicate record

**Given** a Tenant Admin calls `DELETE /api/tenant/groups/{id}/members/{userId}`
**When** the request is processed
**Then** the User is removed from the Group
**And** removing a User from their last Group does not delete the User — Group membership and User existence are independent

**Given** a Tenant Admin calls `DELETE /api/tenant/groups/{id}`
**When** the request is processed
**Then** the Group is deleted and all Group membership records for that Group are removed
**And** Users who were members of the deleted Group are unaffected — their User records remain
**And** `UseXminAsConcurrencyToken()` is confirmed applied to the `Group` entity
**And** `TenantIsolationRegressionTests.cs` is extended to cover Group isolation

---

### Story 4a.5: Dimensional Attribute Reference Lists

As an Internal Admin (for initial axis definition) and a Tenant Admin (for per-Tenant values),
I want each Tenant to maintain reference lists for the 5 dimension axes,
So that Dimension assignments are validated against a controlled vocabulary rather than free-text.

**Acceptance Criteria:**

**Given** the `DimensionValue` table is introduced via EF Core migration
**When** the migration runs
**Then** the table has columns: `Id`, `TenantId`, `Axis` (enum: `Company`, `Location`, `Branch`, `Make`, `MarketSegment`), `Value` (string), `IsActive`
**And** a unique constraint exists on `(TenantId, Axis, Value)` — no duplicate values per axis per Tenant
**And** `UseXminAsConcurrencyToken()` is applied to the `DimensionValue` entity

**Given** a Tenant Admin calls `POST /api/tenant/dimensions/{axis}/values`
**When** the request body contains a `value` string
**Then** a `DimensionValue` record is created for that axis within the Tenant Admin's Tenant
**And** the response is HTTP 201 with the created value
**And** a duplicate `(axis, value)` for the same Tenant returns HTTP 409

**Given** a Tenant Admin calls `GET /api/tenant/dimensions/{axis}/values`
**When** the request is processed
**Then** only `IsActive` values for the calling Tenant's axis are returned (global query filter + IsActive filter)

**Given** a Tenant Admin calls `DELETE /api/tenant/dimensions/{axis}/values/{id}` (soft delete)
**When** the value is currently assigned to one or more users
**Then** `IsActive` is set to `false` — the record is NOT physically deleted and existing assignments are not removed
**And** the deactivated value cannot be used in new assignments (Story 4a.6 validates against active values only)
**And** `TenantIsolationRegressionTests.cs` is extended to cover DimensionValue isolation

---

### Story 4a.6: Per-User Dimension Assignments (Tenant Admin)

As a Tenant Admin,
I want to assign Dimensional Attribute values to Users within my Tenant across all 5 axes,
So that token evaluation can include the User's organizational context in the introspection response.

**Acceptance Criteria:**

**Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/dimensions`
**When** the request body contains `axis` and `valueId`
**Then** a `UserDimensionAssignment` record is created linking the User to the active `DimensionValue`
**And** the `valueId` must belong to the same Tenant and be `IsActive: true` — an inactive or cross-tenant value returns HTTP 422
**And** a User may hold multiple values per axis — assigning a second value to the same axis does not replace the first
**And** the response is HTTP 201 with the created assignment

**Given** a Tenant Admin calls `DELETE /api/tenant/users/{userId}/dimensions/{assignmentId}`
**When** the request is processed
**Then** the assignment record is physically deleted (dimension assignments are not audit-sensitive — removal is clean)
**And** the User's other dimension assignments on the same or other axes are unaffected

**Given** `GET /api/tenant/users/{userId}/dimensions` is called
**When** the request is processed
**Then** all active dimension assignments for the User are returned, grouped by axis: `{ "Company": ["value1"], "Location": ["value2", "value3"], ... }`
**And** axes with no assignments return an empty array — not omitted from the response

**Given** `TenantIsolationRegressionTests.cs` is extended
**When** it runs
**Then** `UserDimensionAssignment` records for User A in Tenant A are NOT visible under Tenant B's context
**And** `UseXminAsConcurrencyToken()` is confirmed applied to the `UserDimensionAssignment` entity

---

### Story 4a.7: User Lifecycle Management (Tenant Admin)

As a Tenant Admin,
I want to create, update, deactivate, and list users within my Tenant,
So that I can manage my organization's user base without Internal Admin involvement.

**Prerequisite:** Story 3.8 (Audit Log Infrastructure) — all mutations in this story write to the audit log.

**Acceptance Criteria:**

**Given** the `UserDto` contract is defined
**When** a developer inspects the response shape
**Then** it contains: `id` (UUID), `email` (string), `displayName` (string), `tenantId` (UUID), `isActive` (boolean), `isTenantAdmin` (boolean), `createdAt` (UTC), `updatedAt` (UTC)

**Given** a Tenant Admin calls `POST /api/tenant/users`
**When** the request body contains `email`, `displayName`, and optional `password`
**Then** a new User is created scoped to the caller's `TenantId` (from `ITenantContext`) — the caller cannot specify a `tenantId` in the request body
**And** the response is HTTP 201 with the full `UserDto`
**And** if `email` already exists within the same Tenant the response is HTTP 409 with `error: "email_conflict"`
**And** if `email` already exists in a *different* Tenant the response is HTTP 201 — cross-tenant email uniqueness is not enforced
**And** `AuditLogService.AppendAsync` is called with `Action: "user.created"`, `EntityType: "User"`, `EntityId: user.Id`

**Given** a Tenant Admin calls `PATCH /api/tenant/users/{id}`
**When** the request body contains one or more of: `displayName`, `email`
**Then** only the supplied fields are updated (RFC 7396 merge patch semantics)
**And** the response is HTTP 200 with the updated `UserDto` including a refreshed `updatedAt`
**And** if `{id}` belongs to a different Tenant the response is HTTP 404 (no cross-tenant existence disclosure)
**And** `AuditLogService.AppendAsync` is called with `Action: "user.updated"` and a `Payload` containing only the changed fields

**Given** a Tenant Admin calls `DELETE /api/tenant/users/{id}`
**When** the request is processed
**Then** the User is soft-deleted: `isActive` is set to `false`, the record is NOT physically removed
**And** the response is HTTP 204
**And** the deactivated user's seat is freed immediately — `seatsUsed` returned by `GET /api/tenant/license` decrements by 1
**And** if `{id}` belongs to a different Tenant the response is HTTP 404
**And** deleting an already-inactive user returns HTTP 204 (idempotent)
**And** `AuditLogService.AppendAsync` is called with `Action: "user.deactivated"`

**Given** a Tenant Admin calls `GET /api/tenant/users`
**When** the request includes optional query params `?page=1&pageSize=25&includeInactive=false`
**Then** the response is HTTP 200 with `{ "items": [...], "page": 1, "pageSize": 25, "totalCount": N }`
**And** by default `includeInactive=false` — deactivated users are excluded unless explicitly requested
**And** only users belonging to the caller's Tenant are returned — cross-tenant users are never present

**Given** a Tenant Admin calls `GET /api/tenant/users/{id}`
**When** the `{id}` belongs to the caller's Tenant
**Then** the response is HTTP 200 with the `UserDto`
**And** if `{id}` belongs to a different Tenant the response is HTTP 404

**Given** all five endpoints above are called with a valid JWT that lacks the `TenantAdmin` role
**When** the requests are processed
**Then** all five return HTTP 403

**Given** `UserLifecycleIntegrationTest` runs
**When** it executes the full sequence: `POST` (create) → `PATCH` (update displayName) → `GET /{id}` (verify update) → `DELETE` (deactivate) → `GET /{id}` (verify isActive=false) → `GET /api/tenant/license` (verify seatsUsed decremented)
**Then** each step passes and the seat count reflects deactivation
**And** `GET /api/tenant/audit` (from Story 3.8) returns three audit entries for the created user: `user.created`, `user.updated`, `user.deactivated` in timestamp order

**Given** `TenantIsolationRegressionTests.cs` is extended
**When** it runs
**Then** User records created under Tenant A are not visible via `GET /api/tenant/users` under Tenant B's context
**And** `POST /api/tenant/users` with a Tenant B `TenantAdminToken` cannot create a user that appears under Tenant A

---

## Epic 4b Stories: Token Evaluation & Overrides

**Goal:** Token issuance returns the correct effective permission set: union of all permissions across a user's group chain (User → Groups → Role Sets → Roles → Permissions), with DENY terminal, user-level overrides respected, and dimensional attributes in the enriched introspection response. The 500ms issuance budget is validated by a performance test.

---

### Story 4b.1: User-Level Permission Override Data Model

As a Tenant Admin,
I want to set ALLOW or DENY overrides on individual Permissions for specific Users,
So that I can handle exceptions to the standard Group-based authorization without restructuring Roles.

**Acceptance Criteria:**

**Given** the `UserPermissionOverride` table is introduced via EF Core migration
**When** the migration runs
**Then** the table has columns: `Id`, `TenantId`, `UserId`, `PermissionId`, `OverrideType` (enum: `Allow`, `Deny`), `Reason` (required string), `ExpiresAt` (nullable datetime), `CreatedAt`, `CreatedByUserId`
**And** a unique constraint exists on `(TenantId, UserId, PermissionId)` — one override per permission per user per tenant
**And** `UseXminAsConcurrencyToken()` is applied to the `UserPermissionOverride` entity

**Given** a Tenant Admin calls `POST /api/tenant/users/{userId}/overrides`
**When** the request body contains `permissionId`, `overrideType`, `reason`, and optional `expiresAt`
**Then** a `UserPermissionOverride` record is created
**And** `permissionId` must reference an active permission in the global catalog — inactive or non-existent permission IDs return HTTP 422
**And** `reason` is required — an empty or missing reason returns HTTP 422

**Given** a Tenant Admin calls `GET /api/tenant/users/{userId}/overrides`
**When** the request is processed
**Then** all override records for the user are returned — including expired ones (expired records are retained for audit trail, AR-11)
**And** each record includes an `isExpired` boolean computed from `ExpiresAt` vs current UTC time

**Given** a Tenant Admin calls `DELETE /api/tenant/users/{userId}/overrides/{overrideId}`
**When** the request is processed
**Then** the override record is physically deleted
**And** `TenantIsolationRegressionTests.cs` is extended: `UserPermissionOverride` records for User A in Tenant A are NOT visible under Tenant B's context

**Given** the override read path queries `UserPermissionOverride`
**When** filtering active overrides for evaluation
**Then** the EF Core query applies `WHERE ExpiresAt IS NULL OR ExpiresAt > NOW()` — expired records are automatically excluded from evaluation at read time with no background sweeper (AR-11)

---

### Story 4b.2: Permission Evaluation Pipeline

As a developer,
I want the `ITokenClaimsEnricher` pipeline extended with a full permission evaluation stage,
So that token issuance produces the correct effective permission set for each user based on their complete Group chain.

**Acceptance Criteria:**

**Given** `ITokenClaimsEnricher` was defined in Story 2.4 with a `RoleClaimsEnricher` stage
**When** this story adds a `PermissionEvaluationEnricher` stage
**Then** no code from Story 2.4's `RoleClaimsEnricher` is deleted or modified — the new stage is registered additively after the existing stage

**Given** a User belongs to Groups G1 and G2
**When** G1 has Role R1 (permissions: `od.crm.read`, `od.crm.write`) and G2 has RoleSet RS1 containing Role R2 (permissions: `od.crm.write`, `od.finance.read`)
**Then** the effective permission set is the deduplicated union: `["od.crm.read", "od.crm.write", "od.finance.read"]`
**And** a `PermissionUnionIntegrationTest` constructs this exact scenario and asserts the union

**Given** a User has a DENY override on `od.crm.write` with no expiry
**When** the permission evaluation pipeline runs
**Then** `od.crm.write` is excluded from the effective permission set regardless of Group assignments
**And** DENY is terminal — no ALLOW override or Group assignment can reinstate a DENY-overridden permission
**And** a `DenyTerminalIntegrationTest` asserts: User with DENY on `od.crm.write` via override does NOT receive `od.crm.write` even when a Group grants it

**Given** a User has an ALLOW override on `od.finance.delete` not present in any Group assignment
**When** the permission evaluation pipeline runs
**Then** `od.finance.delete` IS included in the effective permission set
**And** a `AllowOverrideIntegrationTest` asserts this additive ALLOW behaviour

**Given** a User has a DENY override on `od.crm.write` with `ExpiresAt` in the past
**When** the permission evaluation pipeline runs
**Then** the expired DENY override is NOT applied — `od.crm.write` is included if a Group grants it
**And** `ExpiredDenyOverrideIntegrationTest` confirms this by inserting an override with `ExpiresAt = NOW() - 1 minute` and asserting the permission is present

---

### Story 4b.3: Enriched Introspection Response and Performance Gate

As a resource server (OneDealer v2),
I want the introspection endpoint to return the user's full resolved authorization state — permissions, dimensional attributes, and license status — within the 50ms performance budget,
So that OneDealer v2 can make authorization decisions from a single introspection call without additional round trips.

**Acceptance Criteria:**

**Given** a valid token is submitted to `POST /connect/introspect`
**When** introspection runs with the full enrichment pipeline active
**Then** the response includes (in addition to the existing `active`, `sub`, `jti`, etc.):
`permissions` (array of effective permission ID strings, DENY-evaluated, expiry-filtered),
`dimensional_attributes` (object keyed by axis name, value is array of strings — e.g. `{ "Company": ["OneDealer GmbH"], "Location": [] }`),
`license` (object: `{ "status": "active" | "seat_limit_reached", "seats_used": N, "max_seats": N }`)
**And** all five dimension axes are always present in `dimensional_attributes` — axes with no assignments return an empty array, never omitted

**Given** a User has no Group assignments
**When** their token is introspected
**Then** `permissions` is an empty array and `dimensional_attributes` has all five axes as empty arrays
**And** the response is still `active: true` (no permissions ≠ invalid token)

**Given** `TestTokenFactoryContractTests.cs` Skip was removed in Story 3.5
**When** the enrichment pipeline from this story is active
**Then** `TestTokenFactoryContractTests.cs` is updated to additionally assert: a `TestTokenFactory` token passed through the full `ITokenClaimsEnricher` pipeline produces an introspection response with the correct `permissions`, `dimensional_attributes`, and `license` shape
**And** this test catches drift between `TestTokenFactory` claim shape and the production pipeline as future epics add claims

**Given** `TokenEvaluationPerformanceTests.cs` runs with the full enrichment pipeline active
**When** introspection is measured end-to-end (permission union + override filter + dimension lookup + license check)
**Then** 95th percentile introspection time (excluding network) is under 40ms (10ms headroom against the NFR-4 50ms gate)
**And** the test uses a `Stopwatch` per call, minimum 50 samples, and fails if p95 exceeds the ceiling
**And** `ICacheService` (AR-10) is confirmed used for: effective permission set keyed `permissions:{userId}:{tenantId}`, dimensional attributes keyed `dimensions:{userId}:{tenantId}` — cache miss path and cache hit path are both covered by integration tests

**Given** `IntrospectionEnrichmentRegressionTests.cs` runs
**When** a User's Group assignment changes between two introspection calls
**Then** the second introspection (after cache TTL of 5 minutes in production — bypassed in test via cache invalidation) reflects the updated permission set
**And** the test explicitly invalidates the cache between calls to confirm the refresh path is exercised

---

## Epic 5a Stories: Frontend Shell & Core Components

**Goal:** The React application boots in dark mode with the full design system in place. A developer can navigate between pages using `GlobalNav`. `DataTable` renders with Skeleton loading states. `AdminTierBanner` appears when Internal Admin is in a tenant context. The query key factory and CSS token ESLint rule are enforced from the first commit.

---

### Story 5a.1: Design System Foundation

As a developer,
I want a CSS token system, dark mode configuration, and typography scale enforced by an ESLint rule,
So that every component built from this point forward uses the design system automatically — no manual audits required.

**Acceptance Criteria:**

**Given** `globals.css` is created
**When** the application loads
**Then** the following 8 CSS variable tokens are defined: `--background` (zinc-950), `--sidebar` (zinc-900), `--card` (zinc-800), `--popover` (zinc-800), `--primary` (indigo-500), `--destructive-fg` (red-500), `--destructive-bg` (red-950), `--admin-banner-bg` (amber-600)
**And** Tailwind is configured to use these tokens as semantic aliases — e.g. `bg-background` maps to `var(--background)`
**And** the Inter typeface is loaded and set as the default font family

**Given** the Tailwind configuration
**When** dark mode is configured
**Then** dark mode uses the `class` strategy (not `media`) so the theme is explicitly applied to the `<html>` element
**And** the app defaults to dark mode on load — `<html class="dark">` is set before first paint (no flash of light theme)

**Given** the typography scale is configured
**When** any text element is rendered
**Then** the scale is: 24px page title, 18px section heading, 14px body, 12px label/caption, 13px monospace for permission IDs
**And** tabular numerals are enabled globally (`font-variant-numeric: tabular-nums`) for all numeric data
**And** base unit is 4px, primary rhythm 8px — spacing scale enforces this via Tailwind config

**Given** the ESLint rule for CSS token enforcement is configured
**When** a developer writes a raw Tailwind color utility on a semantic element (e.g. `bg-zinc-950` directly on a page background)
**Then** the ESLint rule flags it as an error: "Use CSS variable token alias instead of raw Tailwind color utility on semantic elements"
**And** `npm run lint` fails if the rule is violated
**And** the rule does NOT flag raw colors in non-semantic contexts (e.g. SVG fills, test files)

---

### Story 5a.2: App Shell — Routing, Tenant Context, and Query Key Factory

As a developer,
I want React Router v7 nested layouts with URL-as-truth tenant context, a Zustand tenant cache, and the `queryKeys` factory defined before any query is written,
So that all navigation updates the URL, tenant switches invalidate the right queries, and stale-data bugs from missing `tenantId` in cache keys are prevented from the start.

**Acceptance Criteria:**

**Given** React Router v7 is configured with nested layouts
**When** a user navigates to `/tenants/:tenantId/users`
**Then** the `tenantId` URL param is the authoritative source of tenant context — it is read directly from the route params, not from Zustand or component state
**And** Zustand stores the resolved `Tenant` object for convenience (display name, etc.) but is never used as the authoritative `tenantId` source
**And** browser back/forward navigation correctly updates the active tenant context without stale data

**Given** `queries/keys.ts` is created before any TanStack Query hook is written
**When** the file is committed
**Then** it exports a `queryKeys` factory with at minimum these keys: `users(tenantId)`, `user(tenantId, userId)`, `groups(tenantId)`, `effectivePermissions(userId)`, `effectivePermissionsPreview()`, `tenants()`, `tenant(tenantId)`, `seatUsage(tenantId)`
**And** all tenant-scoped keys include `tenantId` as a const-typed tuple member — TypeScript compilation fails if `tenantId` is omitted from a tenant-scoped key call

**Given** a user switches tenant (URL changes from `/tenants/A/...` to `/tenants/B/...`)
**When** the route change fires
**Then** TanStack Query invalidates all query keys that include the previous `tenantId`
**And** a `TenantSwitchQueryInvalidationTest` (vitest) asserts the correct keys are invalidated and no Tenant A data is visible in the Tenant B query cache

**Given** the app shell layout renders
**When** no tenant is selected (Internal Admin at root `/`)
**Then** a placeholder page renders with navigation to the Tenants list — no layout errors, no empty `tenantId` passed to query hooks

---

### Story 5a.3: GlobalNav and AdminTierBanner

As an Internal Admin or Tenant Admin,
I want a persistent sidebar navigation and a contextual banner that shows my current admin tier,
So that I always know what context I'm operating in and can navigate quickly between sections.

**Acceptance Criteria:**

**Given** `GlobalNav` renders for a Tenant Admin
**When** the sidebar is in its default expanded state (240px)
**Then** navigation items visible are: Users, Groups, Roles, Role Sets, Audit Log
**And** `aria-current="page"` is set on the active nav item
**And** the active item has a 2px `indigo-500` left border and `zinc-800` background

**Given** `GlobalNav` renders for an Internal Admin (no active tenant context)
**When** the sidebar is expanded
**Then** additional items are visible: Tenants, Permissions, Licenses
**And** `TenantSwitcher` is visible at the bottom of the sidebar (Internal Admin only)
**And** a ⌘K hint is visible in the sidebar footer (CommandPalette stub — not yet functional, wired in Epic 5c)

**Given** the user clicks the collapse toggle
**When** the sidebar collapses
**Then** it transitions to icon-only mode (56px width)
**And** the collapsed/expanded state is persisted in `localStorage` and restored on page reload
**And** the `<nav>` landmark, `<main>` content area, and `<header>` breadcrumbs are correctly structured as ARIA landmarks regardless of sidebar state

**Given** an Internal Admin navigates to `/tenants/:tenantId/...` (active tenant context in URL)
**When** the `AdminTierBanner` renders
**Then** a full-width 40px strip appears above the sidebar+content layout with `amber-600` background and `zinc-950` text
**And** the content reads: "Internal Admin — Tenant: [Tenant Name] / [Current Section]"
**And** an "← All Tenants" router link navigates back to the Tenants list
**And** `aria-live="polite"` is set on the banner — NOT `role="alert"`
**And** the banner is NOT rendered when the Internal Admin is at the root (no tenant context in URL)

**Given** an Internal Admin with a dirty form clicks "← All Tenants"
**When** the unsaved-changes guard triggers
**Then** a confirmation Dialog appears: "You have unsaved changes. Leave anyway?"
**And** confirming navigates away; cancelling returns focus to the form
**And** the guard fires only when form state is dirty — clean navigation proceeds without interruption

---

### Story 5a.4: DataTable and EmptyState Components

As a developer,
I want reusable `DataTable` and `EmptyState` components wired with loading states and ARIA from day one,
So that every list view in the application has consistent behaviour and accessibility without per-page implementation.

**Acceptance Criteria:**

**Given** `DataTable` is implemented with TanStack Table v8
**When** rendered with `isLoading: true`
**Then** Skeleton rows render in place of data rows — the column count matches the real column definitions
**And** `aria-busy="true"` is set on the table container during initial fetch
**And** once data loads, `aria-busy` is removed and real rows replace the Skeleton rows without layout shift

**Given** `DataTable` is rendered with data
**When** a user clicks a column header
**Then** client-side sorting activates (`getSortedRowModel`) — rows re-order without a network request
**And** server-side sorting is opt-in via `onSortingChange` + `manualSorting` props — the component supports both modes without modification

**Given** `DataTable` is rendered with `pagination` prop
**When** the user changes page
**Then** `onPaginationChange` fires with the new pagination state
**And** filtering is injected from the page level via props — no filter input is rendered inside `DataTable` itself

**Given** `EmptyState` is rendered
**When** it replaces a `DataTable` (e.g. no data found)
**Then** the component renders: a centered lucide icon (zinc-600), bold title, description naming a next action, and an optional primary CTA
**And** `<div role="status">` wraps the component so screen readers announce the state change when it replaces the table
**And** the four required variants are implemented: no data (with CTA), no search results (no CTA — modify search), error state, and loading-complete-but-empty

**Given** a `DataTable` vitest component test runs
**When** `isLoading` transitions from `true` to `false`
**Then** the test asserts Skeleton rows are present during loading and absent after data loads
**And** `aria-busy` is asserted present during loading and absent after

---

### Story 5a.5: CI Playwright Stub and Playwright Configuration

As a developer,
I want the Playwright job pre-wired in CI and the config committed with sRGB colour profile enforcement,
So that Epic 5c can enable real flow tests by simply removing the `if: false` skip — no CI rewiring needed.

**Acceptance Criteria:**

**Given** the GitHub Actions CI workflow from Story 1.6
**When** this story updates it
**Then** a `playwright-tests` job is defined with `if: false` — it is skipped on every run today without error
**And** the job definition includes: checkout, Node install, `npx playwright install --with-deps`, `npx playwright test` — the full run command is present even though the job is skipped

**Given** `playwright.config.ts` is committed
**When** the config is examined
**Then** `use.launchOptions.args` includes `"--force-color-profile=srgb"` — required for CI headless contrast detection (UX-DR22)
**And** the base URL is configured via `process.env.BASE_URL` with a localhost default
**And** the config targets Chromium only for Phase 1 (cross-browser testing is post-POC)

**Given** a developer runs `npx playwright test` locally with no test files present
**When** the command executes
**Then** Playwright exits with code 0 and outputs "No tests found" — it does NOT error on an empty test suite
**And** a placeholder `tests/.gitkeep` file ensures the tests directory exists in the repository

**Given** `vitest-axe` is installed
**When** a component test imports it
**Then** `expect(container).toHaveNoViolations()` is available and functional
**And** a smoke test in `src/components/ui/EmptyState.test.tsx` runs `vitest-axe` on the `EmptyState` component and passes — proving the axe integration works before Epic 5b relies on it

---

## Epic 5b Stories: Permission & Override UX

**Goal:** The `EffectivePermissionsPanel` shows a user's resolved authorization state in live mode and real-time preview mode. Tenant Admins can view DENY overrides, remove them, and trigger Force Re-authenticate. The `useFormMutation` hook drives all write operations with consistent propagation-honest feedback. The `useHasPermission` hook gates all permission-sensitive UI.

---

### Story 5b.1: useFormMutation and useHasPermission Hooks

As a developer,
I want `useFormMutation` and `useHasPermission` hooks available before any management form or permission-gated UI is built,
So that all write operations produce propagation-honest feedback and all permission gates behave consistently across the application.

**Acceptance Criteria:**

**Given** `useFormMutation` is implemented wrapping TanStack Query `useMutation`
**When** a mutation succeeds
**Then** a durable (non-auto-dismiss) Sonner toast fires with the `success` message from `MutationMessages`
**And** if `propagationNote` is provided, "Changes effective within 5 minutes." is appended to the toast
**And** if force-revocation was triggered, the toast instead reads "User must re-authenticate — changes are immediate" (the `propagationNote` is overridden, not appended)

**Given** a mutation fails with a system error (network, 5xx)
**When** `useFormMutation` handles the error
**Then** an auto-dismissing toast (8 seconds) fires with the `error` message (string or function result from `MutationMessages`)
**And** inline form field errors are shown for validation failures (4xx with field-level error body) — these do NOT produce a toast
**And** `onSuccess` and `onError` TanStack callbacks passed through the hook are called — they are not swallowed by the hook's internal handlers

**Given** `useHasPermission(permissionId)` is implemented
**When** the hook is called in a component
**Then** it returns `{ permitted: boolean, isLoading: boolean }` derived from the prefetched permissions in the route loader
**And** during `isLoading: true`, interactive elements that depend on the hook render as `disabled` — not hidden, not errored
**And** a vitest test asserts: component using `useHasPermission` renders a disabled button during loading and an enabled button once `permitted: true` resolves

**Given** `useHasPermission` permissions are prefetched in the React Router v7 route `loader`
**When** the component mounts
**Then** `isLoading` is `false` on first render — no flash of disabled state on cold load
**And** Tenant Admin-only routes accessed directly by URL without the Tenant Admin role return a 404-equivalent (`<NotFound />`) — not a permission error that discloses the route exists

---

### Story 5b.2: Permission Label Map and DisabledButtonWithTooltip

As a developer,
I want a frontend permission label registry and a `DisabledButtonWithTooltip` pattern available before any permission-gated UI is built,
So that permission IDs always display human-readable labels and disabled states communicate their reason accessibly.

**Acceptance Criteria:**

**Given** `permissions/registry.ts` is created
**When** it is committed
**Then** it exports `PERMISSION_GROUPS: PermissionGroup[]` structured as groups by domain, each containing `{ id: string, label: string, description?: string }` entries covering every `od.*` constant defined in the backend `Permissions` class
**And** `PERMISSION_LABELS` is a flat lookup derived from `PERMISSION_GROUPS` — it is never maintained separately
**And** `getPermissionLabel(id: string)` returns the label for a known ID or the raw `od.` ID as fallback — never returns blank or undefined

**Given** a frontend vitest test for the permission registry runs
**When** it executes
**Then** it asserts every `const string` exported from the backend `Permissions` class (imported as a JSON fixture or mirrored constant file) has a matching entry in `PERMISSION_GROUPS`
**And** the test fails if a backend permission constant is added without a corresponding frontend label entry — mirroring `PermissionCatalogSyncTests.cs`

**Given** `DisabledButtonWithTooltip` is implemented
**When** a button is permission-blocked
**Then** the button is wrapped in a `<span>` for pointer-event capture, has `aria-disabled="true"` alongside native `disabled`, uses `useId()` for SSR-safe IDs, and has `aria-describedby` pointing to the `TooltipContent`
**And** the permission-block tooltip reads: "You don't have permission to [action]. Contact your administrator."
**And** the precondition-block tooltip reads the specific blocker with a concrete next step (e.g. "No roles assigned to this group. Add a role first.")

**Given** a `DisabledButtonWithTooltip` vitest-axe test runs
**When** the component is rendered in both permission-block and precondition-block states
**Then** `expect(container).toHaveNoViolations()` passes for both states
**And** a keyboard-only user can focus the disabled button wrapper and read the tooltip via `aria-describedby`

---

### Story 5b.3: EffectivePermissionsPanel — Live Mode

As a Tenant Admin,
I want to see a user's resolved permissions in real time with provenance showing where each permission comes from,
So that I can diagnose authorization issues without reading raw database records.

**Acceptance Criteria:**

**Given** `EffectivePermissionsPanel` is rendered with `{ mode: 'live', userId }` props
**When** the panel mounts
**Then** `useEffectivePermissionsLive` (TanStack Query GET to `/api/tenant/users/{userId}/effective-permissions`) fetches and displays the resolved permission set
**And** Tab 1 (Capabilities, default active) shows human-readable labels from `getPermissionLabel()` with hover tooltip revealing the raw `od.` ID
**And** any DENY-overridden permission shows a `DenyOverrideBadge` inline — `red-500` text on `red-950` bg, label "DENY"
**And** a cross-tab search input filters the displayed permissions client-side without re-fetching

**Given** `ProvenanceChain` is implemented and embedded in the panel
**When** the Capabilities tab is active
**Then** each permission shows a collapsed provenance chip: "via Group: [Group Name]" (source label only)
**And** on the Permission Details tab (Tab 2), the full chain is always expanded: User → Group → Role Set → Role → Permission
**And** chains with 5+ nodes show a "Show full chain ↓" expand — no "..." truncation (the collapsed middle IS the diagnostic information)
**And** each node in the chain is a navigation link to that entity's management page

**Given** propagation dimming is implemented
**When** a mutation affecting this user's permissions fires (e.g. override added)
**Then** the panel dims to `opacity-60` and shows "Last resolved Xm ago" timestamp until the next fetch settles
**And** `aria-live="polite"` + `aria-atomic="true"` are set on the announcements region, debounced 400ms after fetch settles

**Given** three distinct empty states exist
**When** the panel renders with no data
**Then** "No group assignments" empty state renders with a CTA to add the user to a group
**And** "No permissions in groups" empty state renders with a CTA to add roles to the user's groups
**And** "All permissions DENY-overridden" empty state renders with a CTA to review overrides
**And** each empty state uses `EmptyState` component with `<div role="status">` wrapper (Story 5a.4)

---

### Story 5b.4: EffectivePermissionsPanel — Preview Mode

As a Tenant Admin,
I want to see a real-time preview of how a user's permissions will change before I save a modification,
So that I can verify the impact of role or override changes before committing them.

**Acceptance Criteria:**

**Given** `EffectivePermissionsPanel` is rendered with `{ mode: 'preview', userId, previewPayload }` props
**When** the panel mounts or `previewPayload` changes
**Then** `useEffectivePermissionsPreview` fires a debounced POST (300–500ms) to `/api/tenant/effective-permissions/preview` with the `previewPayload`
**And** in-flight requests are cancelled when a newer `previewPayload` arrives before the debounce fires (cancel-on-new-request)
**And** the panel displays the preview result using the same Capabilities / Permission Details tab structure as live mode

**Given** the preview payload changes rapidly (user typing in a search or toggling checkboxes)
**When** multiple payload changes occur within the debounce window
**Then** only the final payload triggers a network request — intermediate payloads are debounced away
**And** a vitest test confirms: 5 rapid payload changes within 200ms result in exactly 1 fetch call

**Given** the preview result differs from the current live permissions
**When** the preview panel renders
**Then** newly added permissions are visually highlighted (green indicator)
**And** permissions that would be removed are visually struck through (red indicator)
**And** unchanged permissions render without highlight

**Given** `EffectivePermissionsPanel` in preview mode is embedded in the New User flow (Story 5c — F-1 stepper)
**When** no group has been assigned yet in the form
**Then** an amber `Alert` warning renders: "This user will have no permissions" — matching UX-DR20
**And** the alert is visible before the user saves, not after

---

### Story 5b.5: DenyOverrideSheet, DenyOverrideBadge, and SeatUsageIndicator

As a Tenant Admin,
I want to review and remove DENY overrides inline and see seat usage at a glance in the Users section header,
So that I can resolve authorization exceptions and monitor capacity without leaving the current context.

**Acceptance Criteria:**

**Given** `DenyOverrideBadge` renders inline next to a permission in `EffectivePermissionsPanel`
**When** a user clicks the badge
**Then** `DenyOverrideSheet` opens as a side sheet showing: override type, reason, applied-by user, date applied, and optional expiry date
**And** the badge has `aria-label="DENY override on [permission label] — click to review"`
**And** `DenyOverrideBadge` vitest-axe test passes: `expect(container).toHaveNoViolations()`

**Given** `DenyOverrideSheet` is open
**When** the Tenant Admin has the `od.admin.users.revoke` permission
**Then** "Force Re-authenticate" button is visible and enabled
**And** clicking it calls `POST /api/tenant/users/{userId}/tokens/revoke` and fires the revocation toast: "User must re-authenticate — changes are immediate"

**Given** the Tenant Admin does NOT have `od.admin.users.revoke`
**When** `DenyOverrideSheet` renders
**Then** "Force Re-authenticate" button is hidden entirely (tier never has access = hidden, per UX-DR15)
**And** this is distinct from partial access: if the permission check returns `isLoading`, the button is disabled — never shown-then-failed on click

**Given** "Remove Override" destructive button is clicked in `DenyOverrideSheet`
**When** the Tenant Admin confirms the Medium-tier confirmation Dialog ("Remove this DENY override?")
**Then** `DELETE /api/tenant/users/{userId}/overrides/{overrideId}` fires
**And** the sheet closes, the `DenyOverrideBadge` disappears from the panel, and a propagation-honest toast fires: "Changes effective within 5 minutes."

**Given** `SeatUsageIndicator` renders in the Users section header
**When** seat usage is below 80%
**Then** the indicator shows "N of M seats used" in zinc-400 with no icon
**And** at ≥80% the color changes to amber-400 with a warning icon
**And** at 100% the color changes to red-400 with an alert icon AND the "New User" primary CTA is disabled with tooltip: "Seat limit reached. Contact your administrator to expand your license."
**And** the label is screen-reader safe: "42 of 50 seats used" — NOT "42/50" (split numbers break screen reader announcement)
**And** `SeatUsageIndicator` vitest-axe test passes for all three states (normal, warning, limit-reached)

---

### Story 5b.6: DimensionalScopeSummary Component

**Design reference:** UX-DR23

As a Tenant Admin assigning a role with dimensional restrictions to a user,
I want to see a plain-language sentence summarising what the user can access,
So that I can confirm the assignment is correct before saving and trust the saved state afterward.

**Acceptance Criteria:**

**Given** a `DimensionalScopeSummary` React component is implemented
**When** a developer inspects its props interface
**Then** it accepts `restrictions: Record<DimensionAxis, string[]>` and `roleName: string` and renders a summary string
**And** the component is a pure presentational component — it derives its output entirely from props; no API calls, no TanStack Query

**Given** `DimensionalScopeSummary` receives restrictions with specific values
**When** it renders
**Then** the summary follows the template: `"[Role Name] — restricted to [Axis: value1, value2] and [Axis: value1]"`
**And** examples: "Sales Manager — restricted to Location: Amsterdam, Utrecht and Make: BMW, Audi"; "Inventory Viewer — restricted to Make: BMW (all locations)"

**Given** all values are selected for a given axis
**When** the component renders
**Then** that axis is described as "all [locations / makes / etc.]" rather than listing every value (e.g., "restricted to Make: all makes")

**Given** a value list for an axis exceeds 3 items
**When** the component renders
**Then** the summary shows the first 3 values followed by "+N more" (e.g., "Location: Amsterdam, Utrecht, Rotterdam +2 more")
**And** a Tooltip or Popover reveals the full list on hover/focus of the "+N more" element
**And** the tooltip trigger has `aria-label="Show all [axis] values"` for screen reader accessibility

**Given** a single value is selected for an axis
**When** the component renders
**Then** the axis label is singular (e.g., "Location: Amsterdam", not "Locations: Amsterdam")

**Given** no dimensional restrictions are applied to the role assignment
**When** the component renders
**Then** it displays "no dimensional restrictions (full scope)" — explicit, not blank

**Given** the `DimensionalScopeSummary` is integrated into the role assignment edit form (Step 3 of the New User stepper, Story 5c.2)
**When** the Tenant Admin selects or deselects dimension values
**Then** the summary updates live without requiring a save or API call
**And** the summary is also displayed in read-only form on the User detail view and on the assignment confirmation step

**Given** a `DimensionalScopeSummary.test.tsx` vitest suite runs
**When** it covers all cases: specific values, all-values shorthand, >3 truncation, single-value singular, no restrictions
**Then** all assertions pass
**And** a vitest-axe accessibility test passes for the truncated state (tooltip trigger is keyboard-accessible and announced by screen readers)

---

## Epic 5c Stories: Admin Pages & Accessibility

**Goal:** All Internal Admin and Tenant Admin management pages are complete and WCAG AA contrast validated. Audit log is readable. `CommandPalette` (⌘K) is available. Automated accessibility tests pass in CI. Manual pre-POC accessibility checklist is completed.

---

### Story 5c.1: Tenant Admin Management Pages

As a Tenant Admin,
I want fully functional management pages for Users, Groups, Roles, Role Sets, and Dimension assignments,
So that I can manage my organization's authorization structure entirely through the React console.

**Acceptance Criteria:**

**Given** the Users page renders at `/tenants/:tenantId/users`
**When** a Tenant Admin navigates to it
**Then** a `DataTable` lists all users in the Tenant with columns: name, email, status, group count, last login
**And** `SeatUsageIndicator` is visible in the page header (Story 5b.5)
**And** each row links to a User detail page showing group memberships, dimension assignments, and `EffectivePermissionsPanel` in live mode
**And** `DataTable` renders Skeleton rows during initial fetch and `EmptyState` when no users exist

**Given** the Groups page renders at `/tenants/:tenantId/groups`
**When** a Tenant Admin views a Group detail page
**Then** assigned Roles and Role Sets are listed with their permission counts
**And** Group members are listed with remove-member actions using the Low-tier confirmation (no confirm dialog — UX-DR19)
**And** adding/removing Roles or Role Sets from a Group uses `useFormMutation` with propagation toast

**Given** the Roles page renders at `/tenants/:tenantId/roles`
**When** a Tenant Admin creates or edits a Role
**Then** a multi-select combobox (checkbox + search) allows selecting permissions from the global catalog
**And** form validation fires on blur per field + on submit (UX-DR18) — NOT on every keystroke
**And** attempting to delete a Role assigned to a Group shows HTTP 409 surfaced as an inline error: "This role is assigned to: [Group names]"

**Given** the Role Sets page renders at `/tenants/:tenantId/role-sets`
**When** a Tenant Admin creates or edits a Role Set
**Then** a multi-select combobox allows selecting Roles from the Tenant's role list
**And** the same deletion guard pattern applies as Roles (409 → inline error listing Groups)

**Given** the Dimension Assignments page renders on the User detail page
**When** a Tenant Admin assigns a dimension value to a user
**Then** a multi-select combobox for each axis shows only active values from the Tenant's reference lists
**And** existing assignments render as removable badges below each axis input
**And** all five axes are shown even when empty — no axis is hidden

---

### Story 5c.2: F-1 New User Stepper with Real-Time Permissions Preview

**Prerequisite:** Story 4a.7 (User Lifecycle Management — Tenant Admin) must be complete and its API contract (`POST /api/tenant/users`, `PATCH /api/tenant/users/{id}`, `DELETE /api/tenant/users/{id}`) must be deployed before this story can be verified end-to-end.

As a Tenant Admin,
I want a guided multi-step New User flow that shows me the user's effective permissions before I save,
So that I can verify the user will have the right access level before their account is created.

**Acceptance Criteria:**

**Given** the New User flow is opened from the Users page
**When** it renders
**Then** a vertical stepper presents numbered sections: (1) User Details, (2) Group Assignments, (3) Dimension Assignments, (4) Review & Confirm
**And** "Next" validates the current section before advancing — a section with validation errors cannot be left
**And** the stepper does NOT use `unstable_useBlocker` — only F-3 (Tenant provisioning) uses the blocker (UX-DR17 scope note)

**Given** the user is on step 2 (Group Assignments)
**When** they select or deselect a Group
**Then** `EffectivePermissionsPanel` in preview mode updates in real time (debounced POST, 300ms) showing the projected permission set
**And** if no groups are assigned, the amber "This user will have no permissions" Alert is visible (UX-DR20)
**And** `SeatUsageIndicator` is visible above the stepper — if at seat limit, the "Create User" CTA on the Review step is disabled with tooltip

**Given** the user reaches step 4 (Review & Confirm)
**When** they review the form
**Then** all sections are read-only with an "Edit" link back to each section
**And** `EffectivePermissionsPanel` in preview mode is shown one final time with the complete payload
**And** submitting `POST /api/tenant/users` creates the user and navigates to the new User detail page
**And** form validation errors from the server (e.g. duplicate email) surface as inline errors under the relevant field — not as a generic toast

**Given** the form validation rules for User Details
**When** any field is validated
**Then** validation fires on blur per field + on submit for all fields (UX-DR18)
**And** required fields show a red asterisk in the label
**And** the "Next" / "Create User" button is NEVER pre-disabled based on field completion — it only disables at the seat-limit gate

---

### Story 5c.3: Internal Admin Management Pages

As an Internal Admin,
I want fully functional management pages for Tenants, the global Permission catalog, Licenses, IDP federation configuration, and Tenant Admin designation,
So that I can administer all organizations from a single console without direct database access.

**Acceptance Criteria:**

**Given** the Tenants page renders at `/tenants`
**When** an Internal Admin navigates to it
**Then** a `DataTable` lists all Tenants with columns: name, status, seat usage (used/max), created date
**And** each row links to a Tenant detail page where the Internal Admin can: edit tenant details, manage its license, designate Tenant Admins, and configure IDP federation
**And** Tenant suspension uses the Medium-tier confirmation Dialog: "Suspending [Tenant Name] will immediately revoke all active sessions. Continue?" (UX-DR19)
**And** Tenant deletion (if implemented) uses the High-tier type-to-confirm Dialog requiring the tenant name to be typed (UX-DR19)

**Given** the Permissions page renders at `/permissions`
**When** an Internal Admin views it
**Then** the global `od.*` permission catalog is displayed in a `DataTable` grouped by domain
**And** creating a new permission validates the dot-notation format client-side before submit
**And** deactivating a permission uses the Medium-tier confirmation Dialog: "Deactivating this permission removes it from future token issuances."

**Given** the License management section on a Tenant detail page
**When** an Internal Admin creates or updates a license
**Then** the `maxSeats` field is a numeric input with min=1 validation
**And** `effectiveDate` defaults to today
**And** saving uses `useFormMutation` with success toast: "License updated. Changes effective within 5 minutes."

**Given** the IDP federation configuration section on a Tenant detail page
**When** an Internal Admin configures Okta or Azure AD (stub — full federation is Epic 6)
**Then** the section renders a placeholder card: "Federation — Available in Epic 6" with a disabled "Configure" button
**And** the placeholder is visually consistent with the rest of the page — not a `TODO` comment or missing section

**Given** the Tenant Admin designation section on a Tenant detail page
**When** an Internal Admin views the admin list
**Then** existing Tenant Admins are listed with a "Remove" action
**And** "Remove" is disabled (with tooltip) when only one Tenant Admin remains: "A tenant must have at least one administrator."
**And** adding a new Tenant Admin shows a user-search combobox scoped to users in that Tenant

---

### Story 5c.4: F-3 Tenant Provisioning Stepper

As an Internal Admin,
I want a guided multi-step Tenant provisioning flow with an unsaved-changes guard,
So that I can configure a new Tenant completely in one session without losing partial work on accidental navigation.

**Acceptance Criteria:**

**Given** the Tenant provisioning flow is opened from the Tenants page
**When** it renders
**Then** a vertical stepper presents: (1) Tenant Details, (2) License Configuration, (3) Initial Tenant Admin, (4) Review & Confirm
**And** "Next" validates the current section before advancing
**And** the Review step shows all sections read-only with "Edit" links

**Given** the user has entered data on any step and attempts to navigate away (browser back, sidebar link, "← All Tenants")
**When** `unstable_useBlocker` fires
**Then** a confirmation Dialog appears: "You have unsaved changes. Leaving will discard this new tenant. Continue?"
**And** the blocker fires only for user-entered content — step navigation between stepper sections does NOT trigger it
**And** a code comment documents: `// unstable_useBlocker — API unstable in RR7 minor versions, does not intercept tab close. Scope: F-3 only.`

**Given** step 2 (License Configuration)
**When** the Internal Admin fills in `maxSeats`
**Then** a live preview shows "This tenant will allow up to N active users"
**And** leaving `maxSeats` blank is valid — submitting without a license creates the Tenant with no seat limit (matching Story 3.3 backend behaviour)

**Given** the final "Create Tenant" submit on the Review step
**When** the form is submitted
**Then** `POST /api/internal/tenants` fires (and optionally `POST /api/internal/tenants/{id}/license` if license was configured)
**And** on success the user is navigated to the new Tenant detail page
**And** on server validation error the relevant step is scrolled to and its field shows the inline error

---

### Story 5c.5: Audit Log UI

As an Internal Admin or Tenant Admin,
I want a read-only audit log showing all significant management actions,
So that I can investigate changes without needing database access.

**Acceptance Criteria:**

**Given** the Audit Log page renders at `/tenants/:tenantId/audit` (Tenant Admin) and `/audit` (Internal Admin)
**When** a user navigates to it
**Then** a `DataTable` lists audit events with columns: timestamp, actor (name + email), action type, entity type, entity ID
**And** the Internal Admin view shows all tenants' events; the Tenant Admin view shows only their Tenant's events (scoped via `ITenantContext`)
**And** `DataTable` renders Skeleton rows during fetch and `EmptyState` when no events exist

**Given** audit events are displayed
**When** a user clicks a row
**Then** a detail panel or sheet expands showing the full event payload: timestamp, actor, action, entity ID, and any before/after diff if available
**And** all data is read-only — no edit or delete controls are present (FR-22: audit log is read-only in POC)

**Given** `GET /api/tenant/audit` or `GET /api/internal/audit` is called
**When** the response is measured under load
**Then** p95 response time is under 1 second — satisfying NFR-3 for management UI operations
**And** a `AuditLogPerformanceTest` in the backend asserts this ceiling with a Stopwatch

**Given** the audit log has more than 50 events
**When** the page renders
**Then** server-side pagination is used — the full event history is NOT loaded in a single request
**And** `DataTable` pagination prop connects to `onPaginationChange` which triggers a new fetch with updated `page` and `pageSize` query params

---

### Story 5c.6: CommandPalette

As an Internal Admin or Tenant Admin,
I want a ⌘K CommandPalette for quick navigation and entity search,
So that I can jump to any section or find any entity without using the sidebar.

**Acceptance Criteria:**

**Given** the app is running and a user presses ⌘K (macOS) or Ctrl+K (Windows/Linux)
**When** the CommandPalette opens
**Then** a modal search input renders with focus trapped inside
**And** pressing Escape closes the palette and returns focus to the previously focused element

**Given** the CommandPalette action registry is defined
**When** it is compiled
**Then** only three action types are permitted: `NavigationAction`, `EntitySearchAction`, `QuickAction` — TypeScript compilation fails for any unrecognised type (TypeScript-enforced registry, UX-DR3)
**And** at minimum these actions are registered: navigate to each sidebar section, search Users (by name/email), search Groups, search Roles

**Given** a user types in the CommandPalette search input
**When** the query matches a `NavigationAction`
**Then** selecting it navigates to the target route and closes the palette

**Given** a user types in the CommandPalette search input
**When** the query matches an `EntitySearchAction` (e.g. "Find user: john")
**Then** results are fetched from the appropriate API endpoint and displayed in the palette
**And** selecting a result navigates to that entity's detail page

**Given** the CommandPalette is open
**When** a screen reader user navigates it
**Then** the result list uses `role="listbox"` with `role="option"` items and `aria-selected` on the focused item
**And** `CommandPalette` vitest-axe test passes: `expect(container).toHaveNoViolations()`

---

### Story 5c.7: WCAG AA Audit, Automated Accessibility Tests, and CI Playwright Activation

As a product team,
I want automated accessibility tests passing in CI and the 4 high-risk colour combinations manually verified,
So that the POC is demonstrably WCAG AA compliant before any external stakeholder review.

**Acceptance Criteria:**

**Given** the 4 high-risk colour combinations identified in UX-DR21
**When** manual contrast verification is performed
**Then** `amber-600` on `zinc-950` (AdminTierBanner) achieves ≥4.5:1 — documented with actual measured ratio
**And** `indigo-300` on `zinc-800` at 13px — if AA (4.5:1) is not met at 400 weight, font size is bumped to 14px or weight to 500 and ratio re-measured to confirm AAA (7:1) or AA pass
**And** `red-500` on `red-950` at 11–12px — if AA fails, font size is increased to minimum 13px or weight bumped to 600 and ratio re-confirmed
**And** `amber-400` on `zinc-950` (SeatUsageIndicator) achieves ≥3:1 (large text / UI component threshold)
**And** all four results are documented in `docs/accessibility-audit.md` with measured ratios and any remediation applied

**Given** `@axe-core/playwright` is configured
**When** Playwright flow tests run
**Then** each flow test calls `await page.waitForSelector('[role="dialog"]')` before `.analyze()` for Sheet/Dialog portals — axe does not run before the portal is mounted
**And** `playwright.config.ts` has `--force-color-profile=srgb` (already set in Story 5a.5) — contrast violations are detectable in CI headless mode

**Given** the `playwright-tests` CI job currently has `if: false` (Story 5a.5)
**When** this story removes the `if: false` guard
**Then** the CI job runs Playwright flow tests on every PR
**And** at minimum these 5 flows have Playwright tests: (1) Tenant Admin login → view Users list, (2) create Role → assign to Group, (3) add DENY override → remove override, (4) New User stepper (F-1) end-to-end, (5) CommandPalette keyboard navigation
**And** all 5 flow tests pass `@axe-core/playwright` analysis with zero violations

**Given** the manual pre-POC accessibility checklist from UX-DR22
**When** it is completed before POC demo
**Then** keyboard-only navigation is verified through all 5 flows listed above
**And** `DataTable` with 100+ rows is verified for keyboard navigation and screen reader announcement
**And** "Revoke DENY override keyboard-only" task-completion test is performed (can a keyboard-only user complete the full flow?)
**And** Dialog and Sheet focus traps are verified (Tab does not escape the modal)
**And** CommandPalette focus returns to the trigger element on close
**And** results are recorded in `docs/accessibility-audit.md`

---

## Epic 6 Stories: Federated Authentication ★ Stretch Goal ★

**Goal:** Tenants can configure Okta or Azure AD as an upstream IDP. Federated users authenticate through their IDP and receive OneId-managed JWTs with OneId-assigned roles (not upstream claims). IDP configs are validated before commit. This epic is a stretch goal — not committed POC scope.

---

### Story 6.1: TestFederationHandler and DevSeeder Federated Test User

As a developer,
I want a `TestFederationHandler` that validates OIDC discovery reachability and a pre-provisioned federated test user in DevSeeder,
So that Okta and Azure AD integration tests do not require live external endpoints in CI.

**Acceptance Criteria:**

**Given** `TestFederationHandler` is implemented
**When** called with an OIDC discovery URL
**Then** it validates: the URL is reachable (HTTP 200), the response is parseable as an OpenID Connect discovery document, and `authorization_endpoint` and `token_endpoint` are present
**And** an unreachable URL returns a structured error: `{ "reachable": false, "reason": "Connection refused" }`
**And** a reachable but malformed document returns: `{ "reachable": true, "valid": false, "reason": "Missing authorization_endpoint" }`
**And** `TestFederationHandler` is used in CI in place of live Okta/Azure AD endpoints — integration tests use a local OIDC stub server (e.g. `WireMock` or `IdentityServer` in-process) instead

**Given** `DevSeeder` runs in the `Development` environment
**When** it executes (after global query filters are active, per Story 1.7)
**Then** a pre-provisioned federated test user is seeded: `email: federated@oneid.dev`, `federationProvider: "test-oidc-stub"`, `externalId: "fed-test-001"`
**And** the DevSeeder note from Story 1.7 ("federated test user deferred to Epic 6") is resolved — the Skip comment is removed
**And** DevSeeder remains idempotent — seeding twice does not create duplicate federated user records

**Given** a `FederationIntegrationTestBase` is created
**When** federation integration tests run
**Then** the base class starts an in-process OIDC stub that returns valid discovery documents and issues test ID tokens
**And** test tokens from the stub are signed with a test key — not a real Okta or Azure AD key
**And** all Epic 6 integration tests extend this base class — no test directly calls external IDP endpoints

---

### Story 6.2: Okta OIDC Federation

As an Internal Admin,
I want to configure a Tenant to authenticate users via Okta,
So that dealership staff can sign in with their existing Okta identity without managing a separate OneId password.

**Acceptance Criteria:**

**Given** an Internal Admin configures Okta federation for a Tenant via `POST /api/internal/tenants/{id}/federation/okta`
**When** the request body contains `discoveryUrl`, `clientId`, `clientSecret`
**Then** `TestFederationHandler` validates the `discoveryUrl` before the record is saved — an unreachable URL returns HTTP 422 with `error: "discovery_url_unreachable"`
**And** the federation config is saved with `provider: "okta"` and the client secret is stored encrypted at rest (not plaintext)
**And** the response is HTTP 201 — the client secret is NOT included in the response body

**Given** a Tenant has Okta federation configured
**When** a user initiates login via the Okta flow
**Then** OneId acts as OIDC client, redirecting the user to Okta's `authorization_endpoint`
**And** on successful Okta callback, OneId extracts the `email` claim from the Okta ID token
**And** OneId looks up a local User by that email within the Tenant — if found, authentication proceeds; if not found, the flow returns HTTP 401 with `error: "no_matching_user"` and message: "No OneId account found for this identity. Contact your administrator."
**And** auto-provisioning is explicitly out of scope — no User record is created from the Okta identity

**Given** a federated user authenticates successfully via Okta
**When** OneId issues a JWT
**Then** the JWT is structurally identical to a standard password-auth JWT — `sub`, `iss`, `aud`, `exp`, `iat`, `jti`, `roles`, `tid`
**And** no Okta claims are propagated into the JWT — the token reflects only the OneId User's assigned roles
**And** `OktaFederationIntegrationTests.cs` uses `FederationIntegrationTestBase` (Story 6.1) — no live Okta calls

**Given** a configurable local credential fallback flag exists
**When** the Okta IDP is unreachable (stub returns connection error)
**Then** if `allowLocalFallback: true` is set on the federation config, the user can authenticate with their OneId password
**And** if `allowLocalFallback: false`, authentication fails with `error: "idp_unavailable"` and a clear message

---

### Story 6.3: Azure AD Federation

As an Internal Admin,
I want to configure a Tenant to authenticate users via Azure AD (Microsoft Entra ID),
So that dealership staff using Microsoft 365 can sign in with their existing Azure AD identity.

**Acceptance Criteria:**

**Given** an Internal Admin configures Azure AD federation for a Tenant via `POST /api/internal/tenants/{id}/federation/azure-ad`
**When** the request body contains `tenantId` (Azure AD tenant ID), `clientId`, `clientSecret`
**Then** OneId derives the discovery URL as `https://login.microsoftonline.com/{azureTenantId}/v2.0/.well-known/openid-configuration`
**And** `TestFederationHandler` validates reachability of the derived discovery URL before saving
**And** the config is saved with `provider: "azure-ad"` and client secret encrypted at rest

**Given** a Tenant has Azure AD federation configured
**When** a user initiates login via the Azure AD flow
**Then** OneId redirects to Azure AD's `authorization_endpoint`
**And** on successful callback, OneId extracts the identity using `email` claim first, falling back to `upn` (User Principal Name) claim if `email` is absent
**And** the same no-match failure behaviour applies as Okta: HTTP 401 `error: "no_matching_user"` with the same message
**And** no Azure AD claims are propagated into the issued JWT

**Given** both Okta and Azure AD are configured for the same Tenant
**When** a user initiates login
**Then** a provider selection screen is shown — the user chooses their IDP before being redirected
**And** `MultiProviderSelectionIntegrationTest` asserts both providers appear in the selection response

**Given** `AzureAdFederationIntegrationTests.cs` runs
**When** it executes
**Then** it uses `FederationIntegrationTestBase` — no live Azure AD calls
**And** the `upn` fallback path is explicitly covered: a test token with no `email` claim but a valid `upn` maps correctly to the local User

---

### Story 6.4: IDP Configuration UI

As an Internal Admin,
I want to configure Okta and Azure AD federation for a Tenant through the React console,
So that I can set up and validate federation without using the raw API.

**Acceptance Criteria:**

**Given** the Federation section on the Tenant detail page (currently a stub from Story 5c.3)
**When** this story activates it
**Then** the stub placeholder card is replaced with a functional "Configure Federation" section
**And** an "Add Provider" button opens a Sheet with a provider selector: Okta or Azure AD

**Given** the Internal Admin selects Okta in the provider Sheet
**When** the form renders
**Then** fields are: Discovery URL, Client ID, Client Secret (masked input)
**And** a "Test Connection" button calls `TestFederationHandler` via `POST /api/internal/tenants/{id}/federation/test` and displays: green "Reachable — valid OIDC discovery document" or red "Unreachable: [reason]"
**And** "Test Connection" must pass before the "Save" button becomes enabled — this is the one exception to the "never pre-disable submit" rule (UX-DR18), explicitly scoped to federation config only

**Given** the Internal Admin selects Azure AD in the provider Sheet
**When** the form renders
**Then** fields are: Azure AD Tenant ID, Client ID, Client Secret
**And** the discovery URL is derived and displayed read-only: `https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration`
**And** the same "Test Connection" gate applies before Save is enabled

**Given** a federation provider is already configured for a Tenant
**When** the Internal Admin views the Federation section
**Then** the configured provider is listed with: provider type, client ID (shown), client secret (masked — "••••••"), and last-tested timestamp
**And** "Edit" opens the Sheet pre-populated (client secret field is empty — must be re-entered to update)
**And** "Remove" uses the Medium-tier confirmation Dialog: "Removing federation will require users to log in with their OneId password. Continue?"

**Given** the IDP configuration form is submitted
**When** it saves successfully
**Then** `useFormMutation` fires a success toast: "Federation configured. Users can now sign in with [Provider]."
**And** if the discovery URL has become unreachable since testing (race condition), the server returns HTTP 422 and the Sheet shows an inline error — not a generic toast
