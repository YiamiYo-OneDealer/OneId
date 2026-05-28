import { test, expect, type Page } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'

async function loginWithMockAuth(page: Page) {
  // Intercept /connect/token — the only real network call the app makes
  await page.route('**/connect/token', async (route) => {
    const body = route.request().postData() ?? ''
    if (body.includes('grant_type=password')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ mfa_required: true, mfa_session_token: 'test-mfa-session' }),
      })
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          access_token: 'mock-playwright-token',
          refresh_token: 'mock-refresh-token',
          token_type: 'Bearer',
          expires_in: 3600,
        }),
      })
    }
  })

  await page.goto('/login')
  await page.fill('input[type="email"]', 'test@example.com')
  await page.fill('input[type="password"]', 'password123')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await page.fill('input[type="text"]', '123456')
  await page.getByRole('button', { name: 'Verify' }).click()
  // Auth token now in localStorage (oneid:auth) via persist middleware —
  // survives subsequent page.goto() reloads since persist rehydrates synchronously.
  await page.waitForURL((url) => !url.pathname.startsWith('/login'))
}

async function checkA11y(page: Page) {
  // Wait for the authenticated layout to mount (main landmark appears once auth is confirmed)
  await page.waitForSelector('main', { timeout: 10_000 })
  // Wait for any loading indicators to clear before running axe
  await page
    .locator('[aria-busy="true"]')
    .waitFor({ state: 'detached', timeout: 5_000 })
    .catch(() => {})
  // Scope to WCAG 2.x A and AA rules only — best-practice rules (region,
  // page-has-heading-one) are excluded as they are not WCAG 2.1 AA requirements.
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze()
  expect(results.violations).toEqual([])
}

test.describe('WCAG AA accessibility — Internal Admin pages', () => {
  test.beforeEach(async ({ page }) => {
    await loginWithMockAuth(page)
  })

  test('Tenant list page has no axe violations', async ({ page }) => {
    await page.goto('/internal/tenants')
    await checkA11y(page)
  })

  test('Permissions catalog page has no axe violations', async ({ page }) => {
    await page.goto('/internal/permissions')
    await checkA11y(page)
  })

  test('Internal audit log page has no axe violations', async ({ page }) => {
    await page.goto('/internal/audit-log')
    await checkA11y(page)
  })
})

test.describe('WCAG AA accessibility — Tenant Admin pages', () => {
  test.beforeEach(async ({ page }) => {
    await loginWithMockAuth(page)
  })

  test('Tenant users page has no axe violations', async ({ page }) => {
    await page.goto('/tenant/acme-corp/users')
    await checkA11y(page)
  })

  test('Tenant groups page has no axe violations', async ({ page }) => {
    await page.goto('/tenant/acme-corp/groups')
    await checkA11y(page)
  })

  test('Tenant roles page has no axe violations', async ({ page }) => {
    await page.goto('/tenant/acme-corp/roles')
    await checkA11y(page)
  })

  test('Tenant audit log page has no axe violations', async ({ page }) => {
    await page.goto('/tenant/acme-corp/audit-log')
    await checkA11y(page)
  })
})
