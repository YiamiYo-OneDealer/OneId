# Story 5a.5: CI Playwright Stub and Playwright Configuration

Status: review

## Story

As a developer,
I want the Playwright job pre-wired in CI and the config committed with sRGB colour profile enforcement,
So that Epic 5c can enable real flow tests by simply removing the `if: false` skip — no CI rewiring needed.

## Acceptance Criteria

1. **CI playwright-tests job** — The `playwright-tests` job in `.github/workflows/ci.yml` has `if: false` (skipped on every run) AND includes the full run commands: checkout, Node install, `npx playwright install --with-deps`, `npx playwright test`. The job definition is complete even though it is skipped.

2. **playwright.config.ts** — `use.launchOptions.args` includes `"--force-color-profile=srgb"`. Base URL is configured via `process.env.BASE_URL` with a `http://localhost:5173` default. Config targets Chromium only (cross-browser is post-POC).

3. **Empty test suite** — Running `npx playwright test` locally with no test files in `tests/` exits with code 0 and outputs "No tests found". A `tests/.gitkeep` ensures the tests directory exists in the repository.

4. **vitest-axe installed** — `vitest-axe` is installed as a devDependency. `expect(container).toHaveNoViolations()` is available in all vitest tests via the setup file. A smoke test added to `src/components/shared/EmptyState.test.tsx` calls `axe()` on the `EmptyState` component and passes — proving axe integration works before Epic 5b relies on it.

## Tasks / Subtasks

- [x] Update CI `playwright-tests` job with full commands (AC: #1)
  - [x] Edit `.github/workflows/ci.yml`: keep `if: false`, replace `echo` placeholder with real steps
  - [x] Add: `actions/checkout@v4`, `actions/setup-node@v4` (node 20), `npm ci` (working-directory: `src/OneId.Web`), `npx playwright install --with-deps`, `npx playwright test --pass-with-no-tests`
  - [x] Verify: job still skipped on every CI run (`if: false` remains)
- [x] Update `playwright.config.ts` (AC: #2)
  - [x] File is at `src/OneId.Web/playwright.config.ts`
  - [x] Add `testDir: './tests'`
  - [x] Add `baseURL: process.env.BASE_URL ?? 'http://localhost:5173'`
  - [x] Add `projects` array with Chromium only (using `devices['Desktop Chrome']`)
  - [x] Preserve existing `use.launchOptions.args: ['--force-color-profile=srgb']`
- [x] Create `tests/.gitkeep` (AC: #3)
  - [x] Create empty file at `src/OneId.Web/tests/.gitkeep`
  - [x] Verify `npx playwright test --pass-with-no-tests` (from `src/OneId.Web/`) exits 0 with "No tests found"
- [x] Install and wire `vitest-axe` (AC: #4)
  - [x] From `src/OneId.Web/`: `npm install --save-dev vitest-axe`
  - [x] In `src/test-setup.ts`: defined custom `toHaveNoViolations` matcher via `expect.extend` (vitest-axe@0.1.0 does not ship a matcher — only `axe`/`configureAxe`)
  - [x] Add axe smoke test to `src/components/shared/EmptyState.test.tsx`
  - [x] Verify `npm test` passes — all 33 tests green
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: all)

## Dev Notes

### CRITICAL: Current State (READ FIRST)

**CI workflow** — `.github/workflows/ci.yml` already has `playwright-tests` job with `if: false`, but the job body is just `echo "Playwright tests activated in Epic 5a"`. This must be replaced with real steps (but keep `if: false`).

**`playwright.config.ts`** — `src/OneId.Web/playwright.config.ts` already exists with `--force-color-profile=srgb`. It is incomplete — missing `testDir`, `baseURL`, and `projects`. Update it, do NOT create a new file.

**No `tests/` directory** — Does not exist. Create `src/OneId.Web/tests/.gitkeep` (empty file).

**vitest-axe NOT installed** — `vitest-axe` is absent from `package.json` devDependencies. Install it from inside `src/OneId.Web/`.

**`EmptyState.test.tsx` exists** — At `src/OneId.Web/src/components/shared/EmptyState.test.tsx` with 8 existing tests. Add axe smoke test as a new `it()` block — do NOT modify existing tests.

**Note on epics typo** — The epics file says "a smoke test in `src/components/ui/EmptyState.test.tsx`". This is a typo. `EmptyState` lives in `shared/`, not `ui/`. The correct file is `src/components/shared/EmptyState.test.tsx`.

---

### Updated CI Workflow — Full playwright-tests Job

**File: `.github/workflows/ci.yml`** — MODIFY only the `playwright-tests` job

Replace the current job body:
```yaml
playwright-tests:
  if: false
  runs-on: ubuntu-latest
  steps:
    - name: Placeholder
      run: echo "Playwright tests activated in Epic 5a"
```

With:
```yaml
playwright-tests:
  if: false
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-node@v4
      with:
        node-version: '20'
    - name: Install dependencies
      run: npm ci
      working-directory: src/OneId.Web
    - name: Install Playwright browsers
      run: npx playwright install --with-deps
      working-directory: src/OneId.Web
    - name: Run Playwright tests
      run: npx playwright test
      working-directory: src/OneId.Web
```

Do NOT touch any other jobs (build, test, migration-bundle, migration-validate).

---

### Updated playwright.config.ts — Full File

**File: `src/OneId.Web/playwright.config.ts`** — OVERWRITE entirely

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5173',
    launchOptions: {
      args: ['--force-color-profile=srgb'],
    },
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
```

**Key points:**
- `testDir: './tests'` — Playwright looks for tests in `src/OneId.Web/tests/`
- `baseURL` via env var with localhost default — no hardcoded URLs
- `devices['Desktop Chrome']` spread — Chromium only for Phase 1
- `--force-color-profile=srgb` preserved — required for CI contrast detection (UX-DR22)

---

### vitest-axe Installation and Setup

**Install command (from `src/OneId.Web/` directory):**
```bash
npm install --save-dev vitest-axe
```

**`vitest-axe` API:**
```typescript
import { axe } from 'vitest-axe'
import { toHaveNoViolations } from 'vitest-axe'
```

`axe(container)` is async — it returns a Promise<AxeResults>. Must `await` it.

**Step 1 — Add to `src/test-setup.ts`:**
```typescript
import '@testing-library/jest-dom'
import { expect } from 'vitest'
import { toHaveNoViolations } from 'vitest-axe'
expect.extend(toHaveNoViolations)
```

This makes `expect(results).toHaveNoViolations()` available globally in ALL vitest test files — no per-file import of `toHaveNoViolations` needed.

**Step 2 — Add smoke test to `src/components/shared/EmptyState.test.tsx`:**

Add this at the END of the existing `describe('EmptyState', ...)` block, after the 8 existing tests:

```typescript
it('has no axe violations', async () => {
  const { container } = render(<EmptyState variant="no-data" title="No users" />)
  const results = await axe(container)
  expect(results).toHaveNoViolations()
})
```

Also add the `axe` import at the top of `EmptyState.test.tsx`:
```typescript
import { axe } from 'vitest-axe'
```

**Why a smoke test here:** The EmptyState was already implemented in Story 5a-4. Adding the axe test proves the `vitest-axe` integration works before Epic 5b writes new components that depend on accessibility validation.

---

### Empty Playwright Test Suite Behavior

Playwright 1.48 (installed version) exits with code 0 when no test files are found in the `testDir`. The `.gitkeep` ensures the `tests/` directory exists in the repo (Git does not track empty directories). When the dev runs `npx playwright test` from `src/OneId.Web/`, they should see output like:
```
Running 0 tests using 1 worker
No tests found.
```
Exit code: 0.

If for some reason Playwright exits non-zero, pass `--allow-empty` flag to `npx playwright test` in the CI job definition (and document this).

---

### File Structure

```
src/OneId.Web/
├── playwright.config.ts          ← UPDATE (already exists)
├── tests/
│   └── .gitkeep                  ← NEW (empty file)
├── src/
│   ├── test-setup.ts             ← UPDATE (add vitest-axe extend)
│   └── components/
│       └── shared/
│           └── EmptyState.test.tsx  ← UPDATE (add axe smoke test)
└── package.json                  ← UPDATE (vitest-axe added by npm install)

.github/
└── workflows/
    └── ci.yml                    ← UPDATE (playwright-tests job body)
```

---

### Previous Story Learnings (5a-1 through 5a-4)

1. **`npm install` location matters** — Always run `npm install` from inside `src/OneId.Web/`, not the repo root. Installing at root level causes duplicate React instance errors ("Invalid hook call").
2. **shadcn install bug** — `npx shadcn add` creates files at `./@/components/ui/` literally. Not relevant to this story but documented for awareness.
3. **Design-token ESLint rule** — Bans raw Tailwind colors in JSX className. Not directly relevant here (no new components), but preserve this in any incidental edits.
4. **vitest globals** — `vi.fn()`, `describe`, `it`, `expect` are globally available in tests (no imports needed). However, `axe` from `vitest-axe` MUST be imported explicitly.
5. **Test co-location** — All tests live alongside their component files (`DataTable.test.tsx` next to `DataTable.tsx`). No separate `__tests__` directory.

---

### Architecture Notes

- This story is purely CI and test infrastructure — no application logic changes.
- The `playwright.config.ts` is at the `src/OneId.Web/` root, not `src/OneId.Web/src/`.
- Playwright e2e tests in Epic 5c will live in `src/OneId.Web/tests/` (flat directory, not nested).
- `@axe-core/playwright` (for Playwright-level accessibility) is deferred to Epic 5c Story 5c.7. This story only installs `vitest-axe` (for vitest component-level accessibility).
- Base URL `http://localhost:5173` matches Vite's default dev server port.

### References

- Story 5a.4: [5a-4-datatable-and-emptystate-components.md](./5a-4-datatable-and-emptystate-components.md) — shadcn install patterns, test setup
- Story 1.6: [CI/CD Pipeline](../planning-artifacts/epics.md) — original CI job definitions
- UX-DR22: [epics.md](../planning-artifacts/epics.md) — `--force-color-profile=srgb` requirement for CI headless contrast detection
- Epics 5a-5: [epics.md](../planning-artifacts/epics.md) — Story 5a.5 acceptance criteria

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6 (dev-story workflow, 2026-05-23)

### Debug Log References

- `vitest-axe@0.1.0` only exports `axe` and `configureAxe` — no `toHaveNoViolations`. Defined the matcher manually in `test-setup.ts` via `expect.extend`. TypeScript augmentation added in `src/vitest.d.ts` with `/* eslint-disable */` to suppress unavoidable empty-interface warnings on vitest matcher extensions.
- `playwright.config.ts` `passWithNoTests: true` is silently ignored at runtime in Playwright 1.48 (CLI-only flag). Removed from config; `--pass-with-no-tests` applied in the CI command instead. Confirmed `npx playwright test --pass-with-no-tests` exits 0 with no test files.
- Transient IDE ETIMEDOUT diagnostic on `playwright.config.ts` on every edit — language server connection timeout, not a code error. TypeScript compilation (`tsc -b`) passes clean.

### Completion Notes List

- Updated `.github/workflows/ci.yml`: replaced the `echo` placeholder in `playwright-tests` job with full steps (checkout, node@v4, `npm ci`, `npx playwright install --with-deps`, `npx playwright test --pass-with-no-tests`). `if: false` kept — job still skipped on every run.
- Updated `src/OneId.Web/playwright.config.ts`: added `testDir: './tests'`, `baseURL: process.env.BASE_URL ?? 'http://localhost:5173'`, Chromium-only `projects` with `devices['Desktop Chrome']`. Preserved `--force-color-profile=srgb`.
- Created `src/OneId.Web/tests/.gitkeep` (empty file so Git tracks the tests directory).
- Installed `vitest-axe@0.1.0` as devDependency. Defined `toHaveNoViolations` custom matcher in `src/test-setup.ts` (package doesn't ship one). Added `src/vitest.d.ts` for TypeScript augmentation of vitest `Assertion` type.
- Added axe smoke test to `src/components/shared/EmptyState.test.tsx` — imports `axe` from `vitest-axe`, awaits result, asserts `toHaveNoViolations()`. Test passes.
- Final: build ✅ lint ✅ (0 errors, 1 pre-existing TanStack Table warning) tests ✅ 33/33 passed.

### File List

- `.github/workflows/ci.yml` (modified — playwright-tests job body replaced)
- `src/OneId.Web/playwright.config.ts` (modified — testDir, baseURL, projects added)
- `src/OneId.Web/tests/.gitkeep` (new — empty placeholder for tests directory)
- `src/OneId.Web/package.json` (modified — vitest-axe added to devDependencies)
- `src/OneId.Web/package-lock.json` (modified — lockfile updated)
- `src/OneId.Web/src/test-setup.ts` (modified — toHaveNoViolations custom matcher added)
- `src/OneId.Web/src/vitest.d.ts` (new — TypeScript augmentation for vitest Assertion type)
- `src/OneId.Web/src/components/shared/EmptyState.test.tsx` (modified — axe smoke test added)

## Change Log

- 2026-05-23: Story implemented — CI playwright-tests job wired (if: false), playwright.config.ts updated (testDir/baseURL/Chromium), tests/.gitkeep created, vitest-axe installed with custom toHaveNoViolations matcher, axe smoke test on EmptyState passes. 33/33 tests green.
