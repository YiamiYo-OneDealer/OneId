# Story 2.9: Session Expiry UX

Status: review

## Story

As a user of the React management console,
I want to see a clear, friendly message when my session has expired,
So that I do not think the application has crashed when I am returned to the login screen.

## Acceptance Criteria

**AC1: Silent redirect on page reload / first navigation (no in-memory token)**

**Given** a user reloads the browser tab or navigates directly to a protected route
**When** no access token exists in memory (tokens cleared by reload or session expiry)
**Then** the user is redirected to `/login` without error
**And** the original URL is preserved as a `?returnTo=` query parameter on the `/login` route

**AC2: Session-expiry banner on `LoginPage` when mid-session expiry occurred**

**Given** a user is redirected to `/login` with a `?session_expired=1` query parameter
**When** the login page renders
**Then** a non-blocking informational banner displays: "Your session has expired. Please sign in again."
**And** the banner is distinct from an error state — it uses an informational style (not red/destructive; e.g., uses `text-foreground`/`bg-muted` or a blue/info-type styling, NOT `text-red-600`)
**And** the banner element has `role="status"` for screen reader announcement

**AC3: Successful re-authentication redirects to `returnTo`**

**Given** a user successfully re-authenticates after being returned to `/login` with a `?returnTo=` parameter
**When** authentication completes
**Then** the user is redirected to the original `returnTo` URL
**And** if `returnTo` is absent or starts with `//` (open-redirect risk), the user is redirected to `/internal/tenants`

**AC4: Mid-session refresh failure redirects with expiry banner signal**

**Given** the `ky` `afterResponse` interceptor in `api-client.ts` fails to refresh the token (refresh token expired or revoked — `POST /connect/token` returns non-200)
**When** this happens mid-session (the user had active tokens, not on page load)
**Then** all in-memory tokens are cleared (`clearTokens()`)
**And** the browser is redirected to `/login?returnTo={encodedCurrentPath}&session_expired=1`
**And** on the subsequent `LoginPage` render, the session-expiry banner is shown (as per AC2)

## Tasks / Subtasks

- [x] Task 1: Update `api-client.ts` to include `session_expired=1` in redirect (AC: 4)
  - [x] In the `afterResponse` hook's catch block (where `clearTokens()` and `window.location.href = '/login'` happen), change the redirect target to `/login?returnTo=${encodeURIComponent(window.location.pathname + window.location.search)}&session_expired=1`
  - [x] Do the same in the no-refresh-token guard branch (before the try/catch) — that branch currently does `window.location.href = '/login'`; change to same pattern with `session_expired=1` + `returnTo`

- [x] Task 2: Update `LoginPage` to detect session expiry and render banner (AC: 2, 3)
  - [x] Read `session_expired` from `useSearchParams()`
  - [x] If `session_expired === '1'` (and `returnTo` is present), show the banner element: `<div role="status" className="...">Your session has expired. Please sign in again.</div>`
  - [x] Banner styling: informational, not destructive — use `bg-blue-50 border border-blue-200 text-blue-800 rounded px-4 py-2 text-sm` fixed top-center
  - [x] Confirm existing `safeReturnTo` logic already covers AC3 (it does — no change needed there)

- [x] Task 3: Verify `_authenticated.tsx` AC1 is already satisfied (AC: 1)
  - [x] Confirmed: existing guard redirects to `/login?returnTo={encodedPath}` with no `session_expired` flag — page-reload/first-visit path has no banner

- [x] Task 4: Final verification (AC: all)
  - [x] `npm run build` in `src/OneId.Web/` — zero TypeScript errors ✅
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors ✅
  - [x] `dotnet test OneId.slnx` — no regressions; AR-15 skip count remains at 3

## Dev Notes

### Key Design Decision: `?session_expired=1` Flag

The core UX challenge: how does `LoginPage` distinguish "first visit to a protected route" (no banner) from "mid-session token expiry redirected by the interceptor" (banner)?

- `_authenticated.tsx` redirects with `?returnTo=` only — this covers page reload and first-time navigation. **No banner.**
- `api-client.ts` interceptor redirects with `?returnTo=...&session_expired=1` — this is the mid-session failure case. **Banner shown.**

The `session_expired` flag is only set by the client-side interceptor. A page reload clears in-memory tokens too, but that redirect comes from `_authenticated.tsx` and does not set the flag.

### Files to Modify

```
src/OneId.Web/
  src/
    lib/
      api-client.ts           ← MODIFIED (two redirect locations: no-refresh-token branch + catch block)
    routes/
      login.tsx               ← MODIFIED (read session_expired param, render banner)

No backend changes. No EF Core migrations. No new npm packages.
```

### Current State of `api-client.ts` (redirect locations)

In `src/OneId.Web/src/lib/api-client.ts`, the `afterResponse` hook currently has two `window.location.href = '/login'` assignments:

1. **No-refresh-token branch** (before try/catch):
   ```typescript
   // CURRENT:
   window.location.href = '/login'
   return response
   
   // CHANGE TO:
   const currentPath = encodeURIComponent(window.location.pathname + window.location.search)
   window.location.href = `/login?returnTo=${currentPath}&session_expired=1`
   return response
   ```

2. **Catch block** (refresh failed):
   ```typescript
   // CURRENT:
   window.location.href = '/login'
   return response
   
   // CHANGE TO:
   const currentPath = encodeURIComponent(window.location.pathname + window.location.search)
   window.location.href = `/login?returnTo=${currentPath}&session_expired=1`
   return response
   ```

Note from Story 2.8 dev notes: ky v2 `afterResponse` hook uses `{request, response, retryCount}` state object, not positional args. The hook uses `retryCount === 0` guard + `ky.retry()`. Read the actual `api-client.ts` to confirm exact current implementation before editing — the story 2.8 notes describe what was actually built, which differed from the story scaffold.

### Current State of `login.tsx`

The `LoginPage` already:
- Reads `returnTo` from `useSearchParams()`
- Validates it as same-origin with `startsWith('/')` and `!startsWith('//')`
- Uses `safeReturnTo` for post-auth navigation

**Add** reading `session_expired`:
```typescript
const [searchParams] = useSearchParams()
const returnTo = searchParams.get('returnTo')
const sessionExpired = searchParams.get('session_expired') === '1'

const safeReturnTo =
  returnTo && returnTo.startsWith('/') && !returnTo.startsWith('//')
    ? returnTo
    : '/internal/tenants'
```

**Banner element** (place above the form, inside the outer `div`):
```tsx
{sessionExpired && (
  <div
    role="status"
    className="bg-blue-50 border border-blue-200 text-blue-800 rounded px-4 py-2 text-sm mb-4"
  >
    Your session has expired. Please sign in again.
  </div>
)}
```

Alternatively, if the project uses shadcn/ui's `Alert` component (check `src/OneId.Web/src/components/ui/alert.tsx`):
```tsx
import { Alert, AlertDescription } from '@/components/ui/alert'

{sessionExpired && (
  <Alert role="status" variant="default">
    <AlertDescription>Your session has expired. Please sign in again.</AlertDescription>
  </Alert>
)}
```

Use `Alert` if available — it's consistent with the design system. Fall back to the plain `div` if not present.

### Current State of `_authenticated.tsx`

No changes needed. The existing guard already:
- Redirects to `/login?returnTo={encodedPath}` for unauthenticated access to protected routes
- Excludes `/login`, `/forgot-password`, `/reset-password`, `/suspended` from redirect

The absence of `session_expired=1` in these redirects is intentional — it means the banner will NOT show on page reloads, only on mid-session interceptor-driven expiry. This satisfies the AC "as opposed to a first visit" distinction.

### What NOT to Change

- `_authenticated.tsx` — no code changes; behavior already correct for AC1
- `auth.ts` — token exchange helpers are not involved in this story
- `auth-store.ts` — store interface unchanged
- Backend: zero changes. This story is purely frontend UX.
- No new npm packages required.
- AR-10: No direct `IMemoryCache` injection (not applicable, but maintained)
- AR-15: Skip count remains at 3. Zero new skips permitted.

### AR-15 Deferred-Skip Governance Tracker

| Skip | Owner Story | Status after 2.9 |
|---|---|---|
| `DevSigningKeyStabilityTest` | Story 2.1 (infra) | OPEN |
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 3 / 3 cap** — zero new skips permitted.

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- No `Alert` component in `src/OneId.Web/src/components/ui/` — used plain `div` with `role="status"` and Tailwind blue info styling instead.
- ky v2 `afterResponse` hook uses `{ request, response, retryCount }` state object (confirmed from actual file). Both redirect locations updated with `returnTo` + `session_expired=1`.
- Banner positioned `fixed top-4 left-1/2 -translate-x-1/2` so it floats above the login form without disrupting layout.

### Completion Notes List

- AC1: `_authenticated.tsx` already redirects with `?returnTo=` only (no `session_expired`) — page reload / first visit gets no banner. No change needed.
- AC2: `LoginPage` reads `session_expired` from search params. When `=1`, renders an informational `div[role="status"]` with blue styling ("Your session has expired. Please sign in again."). Not shown on plain `?returnTo=` redirects.
- AC3: Existing `safeReturnTo` logic unchanged — validates same-origin, falls back to `/internal/tenants`. Post-auth redirect preserved.
- AC4: Both `window.location.href` assignments in `api-client.ts` changed to `/login?returnTo={encodedCurrentPath}&session_expired=1`. Covers both the no-refresh-token path and the catch (refresh failed) path.
- Build: `npm run build` zero TypeScript errors. `dotnet build` zero errors. ✅
- AR-15: Skip count unchanged at 3. ✅

### File List

- src/OneId.Web/src/lib/api-client.ts (MODIFIED — both `/login` redirects now include `returnTo` + `session_expired=1`)
- src/OneId.Web/src/routes/login.tsx (MODIFIED — reads `session_expired` param, renders informational banner)
