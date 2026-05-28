# Story 5c-7: WCAG AA Audit, Automated Accessibility Tests & CI Playwright Activation

## Story Metadata

- **Story ID:** 5c-7
- **Story Key:** 5c-7-wcag-aa-audit-automated-accessibility-tests-and-ci-playwright-activation
- **Epic:** 5c — Admin Pages & Accessibility
- **Status:** review
- **Phase:** 8 (requires Epic 4b)
- **Estimated effort:** Medium

---

## User Story

As a developer and compliance-minded stakeholder,
I want WCAG 2.1 AA accessibility automatically enforced in CI via axe-based Playwright tests on all major pages,
So that regressions are caught before merge and the POC passes its accessibility milestone.

---

## Acceptance Criteria

### AC-1: `@axe-core/playwright` installed and configured
- `@axe-core/playwright` is listed in `package.json` devDependencies
- The existing `--force-color-profile=srgb` flag in `playwright.config.ts` is confirmed present (already done in 5a-5)

### AC-2: `playwright.config.ts` has `webServer` configured
- `webServer.command` starts `npm run dev` in `src/OneId.Web`
- `webServer.url` is `http://localhost:5173`
- `webServer.reuseExistingServer: !process.env.CI` so local dev reuses a running server and CI always starts fresh

### AC-3: Playwright auth fixture works
- A shared `test` fixture provides an `authedPage` (Playwright Page) where the user is already past the auth guard
- Auth is achieved by intercepting `/connect/token` via `page.route()` — no real backend needed
- The `passwordGrant` mock returns `{ mfa_required: true, mfa_session_token: 'test-session' }`
- The `mfaGrant` mock returns `{ access_token: 'mock-token', refresh_token: 'mock-refresh', token_type: 'Bearer', expires_in: 3600 }`
- The fixture navigates to `/login`, fills dummy credentials, submits, fills a 6-digit TOTP code, submits, and confirms arrival at a protected route

### AC-4: Playwright accessibility spec covers all major pages — no axe violations
Spec file: `src/OneId.Web/tests/a11y.spec.ts`

Pages checked (using `authedPage`):
1. `/internal/tenants` — Tenant list (Internal Admin)
2. `/internal/permissions` — Permissions catalog
3. `/internal/audit-log` — Internal audit log
4. `/tenant/acme-corp/users` — Tenant user list (Tenant Admin)
5. `/tenant/acme-corp/groups` — Groups page
6. `/tenant/acme-corp/roles` — Roles page
7. `/tenant/acme-corp/audit-log` — Tenant audit log

Each page test must:
- Wait for loading skeletons to resolve (no `aria-busy="true"` on DataTable)
- Call `checkA11y(page)` from `@axe-core/playwright` (or `new AxeBuilder({ page }).analyze()`)
- Assert zero violations

### AC-5: Missing axe tests added to high-risk components (vitest)
Three components currently lack axe coverage. Add one `has no axe violations` test to each:
- `AdminTierBanner.test.tsx` — render with `tier="internal"` (amber AdminTierBanner renders, axe passes)
- `GlobalNav.test.tsx` — render with MemoryRouter + minimal store mock, axe passes
- `DataTable.test.tsx` — render with at least one row, axe passes

### AC-6: CI `playwright-tests` job activated
- In `.github/workflows/ci.yml`, change `if: false` to `if: true` (or remove the `if` guard entirely)
- Job installs Playwright browsers with `npx playwright install --with-deps`
- Job runs `npx playwright test` (no `--pass-with-no-tests` needed once real tests exist)
- Job depends on nothing that requires the .NET backend (all data is mocked)

### AC-7: CI `vitest-tests` job added
Add a new job to `.github/workflows/ci.yml`:
```yaml
vitest-tests:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-node@v4
      with:
        node-version: '20'
    - name: Install dependencies
      run: npm ci
      working-directory: src/OneId.Web
    - name: Run vitest
      run: npm test -- --run
      working-directory: src/OneId.Web
```

### AC-8: All pre-existing vitest tests still pass
- Baseline: 135 tests passing (from 5c-2 completion)
- After adding 3 new axe tests: ≥ 138 vitest tests pass
- `npm run build` clean
- `npm run lint` no new errors

---

## Dev Notes — Read This Entire Section Before Writing a Single Line of Code

### Critical Project State

| Metric | Value |
|--------|-------|
| Vitest tests passing | 135 (as of 5c-2 completion) |
| TypeScript errors | 0 |
| Build | clean |
| Playwright tests | 0 (tests/ has only `.gitkeep`) |

### Architecture: The App Is Fully Mocked

**This is the most important fact for Playwright tests.** All data queries (`useTenants`, `useUsers`, `useGroups`, etc.) call `mockStore` functions directly — **no real backend API calls for data**. The only real network calls are auth endpoints (`/connect/token`). Therefore:

- Playwright tests need NO running .NET backend
- Only need to intercept `/connect/token` with `page.route()`
- The Vite dev server alone is sufficient for full E2E accessibility testing

### Auth Flow for Playwright Tests

The login page (`/login`) goes through two steps:
1. **Step 1 — credentials:** POST to `/connect/token` with `grant_type=password` → returns `{ mfa_required: true, mfa_session_token: '...' }`
2. **Step 2 — TOTP:** POST to `/connect/token` with `grant_type=mfa_otp` → returns `{ access_token, refresh_token, ... }`

The app routes are protected by `AuthenticatedLayout` which checks `useAuthStore().accessToken`. If null and not a public path, redirects to `/login`.

**Playwright route mock pattern:**
```typescript
await page.route('/connect/token', async (route) => {
  const body = await route.request().postData() ?? ''
  if (body.includes('grant_type=password')) {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ mfa_required: true, mfa_session_token: 'test-mfa' }),
    })
  } else {
    // mfa_otp grant
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        access_token: 'mock-playwright-token',
        refresh_token: 'mock-refresh',
        token_type: 'Bearer',
        expires_in: 3600,
      }),
    })
  }
})
```

After mocking, navigate to `/login`, fill email + password, click "Sign in", fill 6-digit TOTP (any 6 digits e.g. `'123456'`), click "Verify". Confirm redirect away from `/login`.

### Playwright `webServer` Config

Update `playwright.config.ts` to add:
```typescript
webServer: {
  command: 'npm run dev',
  url: 'http://localhost:5173',
  reuseExistingServer: !process.env.CI,
  timeout: 120_000,
},
```

The `command` runs from the `testDir`'s parent or the config file's directory — since `playwright.config.ts` is in `src/OneId.Web/`, `npm run dev` runs in that directory. Confirm this works by checking `package.json` has a `dev` script.

### `@axe-core/playwright` Usage

Install: `npm install --save-dev @axe-core/playwright`

**Pattern for accessibility check in Playwright:**
```typescript
import AxeBuilder from '@axe-core/playwright'

// Inside a test:
const results = await new AxeBuilder({ page }).analyze()
expect(results.violations).toEqual([])
```

**Important:** wait for page to finish loading before running axe. The DataTable shows a loading skeleton (check `aria-busy`). Use:
```typescript
// Wait for the main content area to be visible (no loading indicator)
await page.waitForSelector('[aria-busy="true"]', { state: 'detached', timeout: 5000 }).catch(() => {})
```

Or simply `await page.waitForLoadState('networkidle')` (works well with mocked data since no real network).

### Tenant ID for Playwright Tests

From `fixtures.ts`, the mock tenant ID is `'acme-corp'`. Tenant-scoped routes use this ID:
- `/tenant/acme-corp/users`
- `/tenant/acme-corp/groups`
- `/tenant/acme-corp/roles`
- `/tenant/acme-corp/audit-log`

The `TenantAdminLayout` likely uses `useParams().tenantId` and sets tenant context. Verify the layout renders correctly by checking that a known element appears on the page.

### Vitest Axe Tests to Add

#### 1. AdminTierBanner — `src/components/shared/AdminTierBanner.test.tsx`

Add at bottom of the existing `describe` block:
```typescript
it('has no axe violations', async () => {
  // AdminTierBanner renders in a MemoryRouter context
  const { container } = render(<AdminTierBanner />, { wrapper: MemoryRouter })
  expect(await axe(container)).toHaveNoViolations()
})
```

Check the existing test file to understand the current render setup (it may use a store mock). The `AdminTierBanner` has `aria-live="polite"` per UX spec — this should pass axe.

#### 2. GlobalNav — `src/components/shared/GlobalNav.test.tsx`

Add at bottom of existing describe block:
```typescript
it('has no axe violations', async () => {
  const { container } = render(<GlobalNav />, { wrapper: MemoryRouter })
  expect(await axe(container)).toHaveNoViolations()
})
```

GlobalNav likely requires a store mock for `useAuthStore` / tenant context. Follow the existing test setup pattern already in the file.

#### 3. DataTable — `src/components/shared/DataTable.test.tsx`

Add at bottom of existing describe block:
```typescript
it('has no axe violations with data', async () => {
  const columns = [{ accessorKey: 'name', header: 'Name' }]
  const data = [{ name: 'Alice' }]
  const { container } = render(<DataTable columns={columns} data={data} />)
  expect(await axe(container)).toHaveNoViolations()
})
```

### Playwright Test File Structure

**File:** `src/OneId.Web/tests/a11y.spec.ts`

```typescript
import { test, expect } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'

// Auth setup helper
async function loginWithMockAuth(page) {
  await page.route('/connect/token', async (route) => { ... })
  await page.goto('/login')
  await page.fill('[name="email"]', 'admin@example.com')
  await page.fill('[name="password"]', 'password')
  await page.click('button[type="submit"]')
  await page.fill('[name="totpCode"]', '123456')  // check actual input name/selector
  await page.click('button[type="submit"]')
  await page.waitForURL((url) => !url.pathname.startsWith('/login'))
}
```

> **Note:** Verify the actual form field selectors (name/id attributes) by reading `src/routes/login.tsx` before implementing. The above are approximate.

### CI Job Dependencies

- `vitest-tests` job: no backend dependency, runs independently
- `playwright-tests` job: no backend dependency (data all mocked), runs independently
- Neither job needs `needs: build` or `needs: test` (backend jobs)

Consider running vitest-tests in parallel with the build job since they're independent.

### Existing axe Tests (Do NOT Duplicate)

These components already have axe tests — do not add more:
- `EmptyState.test.tsx` ✅
- `DisabledButtonWithTooltip.test.tsx` ✅
- `SeatUsageIndicator.test.tsx` ✅
- `DenyOverrideBadge.test.tsx` ✅
- `DenyOverrideSheet.test.tsx` ✅
- `CommandPalette.test.tsx` ✅
- `DimensionalScopeSummary.test.tsx` ✅

### Files to Modify

| File | Change |
|------|--------|
| `src/OneId.Web/package.json` | Add `@axe-core/playwright` to devDependencies |
| `src/OneId.Web/playwright.config.ts` | Add `webServer` config block |
| `src/OneId.Web/tests/a11y.spec.ts` | **CREATE** — Playwright axe accessibility spec |
| `src/OneId.Web/src/components/shared/AdminTierBanner.test.tsx` | Add 1 axe test |
| `src/OneId.Web/src/components/shared/GlobalNav.test.tsx` | Add 1 axe test |
| `src/OneId.Web/src/components/shared/DataTable.test.tsx` | Add 1 axe test |
| `.github/workflows/ci.yml` | Activate playwright job + add vitest job |

### Known WCAG Risk Areas (for Playwright to catch)

Per UX spec, four high-risk contrast combinations were flagged:
1. `amber-600` + `zinc-950` (AdminTierBanner) — must meet 4.5:1
2. `indigo-300` permission IDs on `zinc-800` at 13px — AAA target (7:1)
3. `red-500` DENY badge on `red-950` at 11–12px — **likely AA failure, escalate to 13px/weight-600**
4. `amber-400` SeatUsageIndicator on `zinc-950` — must meet 3:1

The `--force-color-profile=srgb` flag in `playwright.config.ts` (already set in 5a-5) is critical for detecting contrast failures in headless CI. If axe catches contrast violations, investigate the failing element and report to the team rather than silencing the violation.

### Design-Token ESLint Rule Reminder

Raw Tailwind color utilities (`text-amber-600`, `bg-zinc-950`) are forbidden in JSX. Use semantic tokens (`text-[var(--color-warning-fg)]`). This rule is already enforced — don't introduce violations in new test helpers or config.

---

## Previous Story Learnings (from 5c-2)

1. `activeTenantId` from tenant store is `string | null` — use `?? ''` pattern
2. Pre-existing `TS6133` errors (unused `React` imports) may surface during incremental rebuild — fix by removing unused imports
3. Test count was 126 before 5c-2, is now 135 — the 3 new axe tests bring target to ≥ 138

From 5a-5 (Playwright setup story):
- `npm install` must run from `src/OneId.Web/`, not repo root
- Test files are co-located with components (no separate `__tests__` directory)
- The `toHaveNoViolations` custom matcher is registered in `src/test-setup.ts` and works for vitest-axe
- The `playwright-tests` CI job was left with `if: false` intentionally for this story to activate

---

## Test Count Summary

| Category | Before | After |
|----------|--------|-------|
| Vitest tests | 135 | ≥ 138 (+3 axe tests) |
| Playwright tests | 0 | ≥ 7 (one per page) |

---

## Definition of Done

- [ ] `@axe-core/playwright` installed
- [ ] `playwright.config.ts` has `webServer`
- [ ] `tests/a11y.spec.ts` created with ≥ 7 page accessibility checks
- [ ] Auth mock fixture navigates past login guard
- [ ] All Playwright tests pass locally (`npx playwright test`)
- [ ] 3 new vitest axe tests added (AdminTierBanner, GlobalNav, DataTable)
- [ ] ≥ 138 vitest tests pass (`npm test -- --run`)
- [ ] `npm run build` clean
- [ ] `npm run lint` no new errors
- [ ] `playwright-tests` CI job activated (no `if: false`)
- [ ] `vitest-tests` CI job added to ci.yml

---

## Completion Record

**Model used:** claude-sonnet-4-6
**Completion date:** 2026-05-28

**Key learnings:**
1. Zustand v5 `persist` middleware hydrates asynchronously — `AuthenticatedLayout` must check `useAuthStore.persist.hasHydrated()` before making auth decisions, otherwise a page refresh causes a spurious redirect to `/login` (real UX bug fixed as part of this story).
2. Auth store was missing `persist` middleware entirely — tokens were lost on page reload. Added `persist` middleware with `name: 'oneid:auth'` to fix this.
3. Playwright's `page.route('**/connect/token', ...)` with `page.goto()` correctly mocks auth without any backend. After login, `localStorage` persists the token via Zustand persist, so subsequent `page.goto()` calls (full reloads) remain authenticated.
4. Vitest picks up `*.spec.ts` files by default — added `exclude: ['**/tests/**']` to `vite.config.ts` to prevent it from picking up Playwright specs.
5. Scoping axe to `withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])` is the correct approach for a WCAG AA audit — best-practice rules (`region`, `page-has-heading-one`) are not WCAG requirements.
6. `--destructive` CSS variable was `red-500` (#ef4444) at 3.76:1 with white text — fails WCAG AA at 12px/normal. Changed to `red-700` equivalent (0 72.2% 44%) which passes at 5.6:1.
7. Breadcrumbs used `<span>` wrapper around `<li>` (BreadcrumbItem), breaking list structure. Fixed by replacing with `React.Fragment`.
8. Empty `header: ''` on DataTable actions columns violated `empty-table-header` axe rule. Fixed with `header: () => <span className="sr-only">Actions</span>`.

**Files modified:**
- `src/OneId.Web/package.json` — added `@axe-core/playwright@^4.11.3`
- `src/OneId.Web/playwright.config.ts` — added `webServer` config
- `src/OneId.Web/vite.config.ts` — added `exclude: ['**/tests/**']` to vitest config
- `src/OneId.Web/src/store/auth-store.ts` — added Zustand `persist` middleware
- `src/OneId.Web/src/routes/_authenticated.tsx` — added `hasHydrated()` guard
- `src/OneId.Web/src/index.css` — darkened `--destructive` to red-700 equivalent in both light and dark modes
- `src/OneId.Web/src/components/shared/Breadcrumbs.tsx` — replaced `<span>` with `React.Fragment`
- `src/OneId.Web/src/routes/internal/tenants/TenantListPage.tsx` — fixed empty actions column header
- `src/OneId.Web/src/components/shared/AdminTierBanner.test.tsx` — added axe test
- `src/OneId.Web/src/components/shared/GlobalNav.test.tsx` — added axe test
- `src/OneId.Web/src/components/shared/DataTable.test.tsx` — added axe test
- `src/OneId.Web/tests/a11y.spec.ts` — **CREATED** Playwright accessibility spec
- `.github/workflows/ci.yml` — activated `playwright-tests` job, added `vitest-tests` job

**Final test count:** 138 vitest tests ✅ | 7 Playwright tests ✅
