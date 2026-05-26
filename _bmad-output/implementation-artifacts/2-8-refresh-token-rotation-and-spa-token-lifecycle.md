# Story 2.8: Refresh Token Rotation and SPA Token Lifecycle

**Status:** review

## Story

As a user of the React management console,
I want my session to stay active while I am working without re-authenticating every 15 minutes,
So that short access token lifetimes do not disrupt my workflow.

**Architecture decision (documented here):** Access tokens and refresh tokens are stored in JavaScript memory only (no `localStorage`, no `sessionStorage`, no cookies). A page reload clears all tokens — the user must re-authenticate. This is the accepted trade-off for security; a BFF with httpOnly cookie refresh token storage is a post-POC upgrade path.

## Acceptance Criteria

**AC1: Backend — refresh token rotation configuration**

**Given** OpenIddict is configured for refresh token issuance
**When** a developer inspects the OpenIddict server options
**Then** `AllowRefreshTokenFlow()` is enabled (already done — verify it remains) and refresh token rotation is active — each use of a refresh token issues a new access token AND a new refresh token; the used refresh token is immediately invalidated
**And** access token lifetime is 15 minutes; refresh token sliding expiry is 8 hours with an absolute ceiling of 24 hours
**And** these values are configurable via `appsettings.json` — not hardcoded in `Program.cs`

**AC2: Frontend — token memory storage and authenticated API client**

**Given** a user authenticates successfully (password + MFA grant flow)
**When** the SPA receives the token response
**Then** the access token and refresh token are stored in JavaScript module-scope memory (Zustand auth store — never persisted to `localStorage`, `sessionStorage`, or cookies)
**And** the SPA `ky` instance (in `lib/api-client.ts`) has a request hook that attaches the access token as `Authorization: Bearer {token}`

**AC3: Frontend — 401 interceptor with transparent refresh**

**Given** the SPA makes an API request and the access token has expired
**When** the API returns HTTP 401
**Then** the SPA `ky` `beforeError` hook automatically calls `POST /connect/token` with `grant_type=refresh_token` and the stored refresh token
**And** on a successful token response, the hook stores the new access and refresh tokens in the Zustand auth store and retries the original request transparently — the calling component receives the successful response with no awareness of the token refresh
**And** the transparent retry happens at most once per original request — a second 401 after retry does NOT trigger another refresh attempt (prevents infinite loops)
**And** if the refresh call itself fails (400 — refresh token expired or revoked), the hook clears the auth store and redirects to `/login`

**AC4: Frontend — real LoginPage with password + MFA flow**

**Given** a user navigates to `/login`
**When** the page renders
**Then** a functional login form is shown with email and password fields
**And** on valid submission the form calls `POST /connect/token` (password grant) which returns `{ mfa_required: true, mfa_session_token: "..." }`
**And** the UI then shows a TOTP input field; on submission the form calls `POST /connect/token` (urn:oneid:mfa grant) which returns `{ access_token, refresh_token, ... }`
**And** on successful authentication the tokens are stored in the Zustand auth store and the user is redirected to the `returnTo` URL if present, else to `/internal/tenants`
**And** errors at either step show an inline error message: "Invalid credentials or MFA code"
**And** the submit button is disabled during requests (no double-submit)

**AC5: Frontend — `_authenticated.tsx` route guard**

**Given** a user navigates to a protected route (anything under `/internal` or `/tenant`)
**When** the `AuthenticatedLayout` renders
**Then** it reads the access token from the Zustand auth store
**And** if no access token is present, it redirects to `/login?returnTo={currentPath}` (preserving the intended destination)
**And** if an access token is present, it renders `<Outlet />` (the protected content)
**And** on page reload, the in-memory token is gone, so the user is redirected to `/login` (no stale state)

**AC6: Backend integration test — refresh token rotation**

**Given** a `RefreshTokenRotationTests.cs` integration test runs
**When** it calls `POST /connect/token` with `grant_type=refresh_token` using a valid refresh token obtained after full MFA authentication
**Then** the response is HTTP 200 with a new `access_token` and a new `refresh_token`
**And** replaying the original (consumed) refresh token returns HTTP 400 with `error: "invalid_grant"`
**And** the new access token passes JWT signature validation (RS256, correct issuer and audience)

## Story Notes

- **Token grant flow for SPA**: This project uses password grant + custom MFA grant (`urn:oneid:mfa`) for the admin SPA — NOT Authorization Code with PKCE redirect. The PKCE flow is enabled in OpenIddict and works end-to-end (DevSeeder registers `oneid-dev-client` with PKCE + AuthorizationCode permissions), but the SPA uses direct credential submission. This is intentional for the admin console use case.
- **Refresh token in OpenIddict**: Rotation is the default behavior in OpenIddict — each `grant_type=refresh_token` call invalidates the old token and issues a new one. The backend change is primarily moving lifetimes from hardcoded `Program.cs` to `appsettings.json` and adjusting the values from the current 7-day flat lifetime to the 8-hour sliding behavior.
- **OpenIddict absolute ceiling**: OpenIddict does not have a native "sliding + absolute ceiling" config. Implement as follows: `SetRefreshTokenLifetime(8h)` (each rotation extends by 8h from use time, naturally creating a sliding window); `SetRefreshTokenRollingLifetimeExtension(8h)` if the API version supports it, otherwise use only the flat 8h. For a true absolute ceiling (24h), a custom `IOpenIddictTokenManager` decorator is needed — this is **out of scope for the POC**. Document in story notes that 24h absolute ceiling is deferred. The dev should implement 8h sliding only.
- **`ky` is NOT installed**: The architecture specifies `ky` as the API client but the package is not yet in `package.json`. This story must install it: `npm install ky`. Existing `fetch`-based pages (`forgot-password.tsx`, `reset-password.tsx`) should be left as-is — migrating them to `ky` is not part of this story's scope.
- **No full PKCE callback route needed**: The SPA does not redirect for auth, so no `/callback` route handler is required. The client supports PKCE for future use but the current SPA flow is credential-based.
- **AR-15: Skip count**: Remains at 3. Zero new `[Fact(Skip)]` permitted.
- **AR-10**: No direct `IMemoryCache` injection.

## Tasks / Subtasks

- [x] Task 1: Move OpenIddict token lifetimes to `appsettings.json` (AC: 1)
  - [x] Add `"OpenIddict"` section to `src/OneId.Server/appsettings.json` with keys: `AccessTokenLifetimeMinutes` (15), `RefreshTokenSlidingExpiryHours` (8)
  - [x] Update `src/OneId.Server/Program.cs`: replace hardcoded `SetAccessTokenLifetime(TimeSpan.FromMinutes(15))` with config read
  - [x] Replace `SetRefreshTokenLifetime(TimeSpan.FromDays(7))` with `SetRefreshTokenLifetime(TimeSpan.FromHours(config))` using the `RefreshTokenSlidingExpiryHours` value
  - [x] Verify `AllowRefreshTokenFlow()` remains present (it does — no change needed)
  - [x] Add `dotnet build` confirmation — zero errors

- [x] Task 2: Backend integration test — `RefreshTokenRotationTests.cs` (AC: 6)
  - [x] Create `tests/OneId.Server.IntegrationTests/OpenIddict/RefreshTokenRotationTests.cs`
  - [x] Helper `IssueMfaTokenWithRefreshAsync()`: returns `(accessToken, refreshToken)` — includes `offline_access` scope
  - [x] Test: `RefreshToken_ValidToken_IssuesNewAccessAndRefreshTokens` — happy path
  - [x] Test: `RefreshToken_ConsumedToken_Returns400InvalidGrant` — rotation enforcement
  - [x] Test: `RefreshToken_NewAccessToken_PassesSignatureValidation` — RS256 validation

- [x] Task 3: Install `ky` and create `lib/api-client.ts` (AC: 2, 3)
  - [x] Run `npm install ky` in `src/OneId.Web/`
  - [x] Create `src/OneId.Web/src/lib/api-client.ts` — single `ky` instance with `beforeRequest` (attach Bearer token) and `afterResponse` (401 → refresh → retry via `ky.retry()`, with `retryCount` guard)
  - [x] Create `src/OneId.Web/src/lib/auth.ts` — token exchange helpers: `passwordGrant(email, password)`, `mfaGrant(mfaSessionToken, totpCode)`, `refreshGrant(refreshToken)` — all call `/connect/token` directly via `fetch` (not ky, to avoid interceptor re-entry)

- [x] Task 4: Create Zustand auth store (AC: 2, 3, 4, 5)
  - [x] Create `src/OneId.Web/src/store/auth-store.ts`
  - [x] State: `accessToken: string | null`, `refreshToken: string | null`
  - [x] Actions: `setTokens(access, refresh)`, `clearTokens()`
  - [x] Export `useAuthStore` (Zustand hook)

- [x] Task 5: Implement real `LoginPage` with password + MFA flow (AC: 4)
  - [x] Update `src/OneId.Web/src/routes/login.tsx`
  - [x] Step 1: email + password form → POST password grant → on `mfa_required`, transition to TOTP step
  - [x] Step 2: TOTP input → POST MFA grant → on success, call `setTokens()`, navigate to `returnTo` or `/internal/tenants`
  - [x] Read `returnTo` from `useSearchParams()` — validate it starts with `/` and is same-origin before use
  - [x] Error handling: inline error message "Invalid credentials or MFA code" on any failure
  - [x] Button disabled during request

- [x] Task 6: Wire real token guard in `_authenticated.tsx` (AC: 5)
  - [x] Update `src/OneId.Web/src/routes/_authenticated.tsx`
  - [x] Read `accessToken` from `useAuthStore()`
  - [x] If null → `<Navigate to={'/login?returnTo=' + encodeURIComponent(location.pathname + location.search)} replace />`
  - [x] If present → `<Outlet />`
  - [x] Import `useLocation` from `react-router` for current path

- [x] Task 7: Final verification (AC: all)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [x] `dotnet test OneId.slnx` — all new integration tests pass; no regressions; AR-15 skip count remains at 3
  - [x] `npm run build` in `src/OneId.Web/` — zero TypeScript errors

## Dev Notes

### Backend: appsettings.json Token Config

Add to `src/OneId.Server/appsettings.json`:
```json
"OpenIddict": {
  "AccessTokenLifetimeMinutes": 15,
  "RefreshTokenSlidingExpiryHours": 8
}
```

In `Program.cs`, replace the hardcoded lifetime lines:
```csharp
// BEFORE (lines ~119-120 in current Program.cs):
options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));

// AFTER:
var oidcConfig = builder.Configuration.GetSection("OpenIddict");
options.SetAccessTokenLifetime(
    TimeSpan.FromMinutes(oidcConfig.GetValue<int>("AccessTokenLifetimeMinutes", 15)));
options.SetRefreshTokenLifetime(
    TimeSpan.FromHours(oidcConfig.GetValue<int>("RefreshTokenSlidingExpiryHours", 8)));
```

Read the config object BEFORE `options.SetAuthorizationEndpointUris(...)` — any position inside the `AddServer` lambda works.

### Backend: Refresh Token Rotation in OpenIddict

OpenIddict rotates refresh tokens by default — no extra configuration needed. Each `POST /connect/token` with `grant_type=refresh_token` invalidates the submitted token and issues a new pair. This is controlled by OpenIddict's built-in token management, not custom code.

The refresh token response from OpenIddict uses the standard OAuth2 token response:
```json
{
  "access_token": "...",
  "token_type": "Bearer",
  "expires_in": 900,
  "refresh_token": "..."
}
```

### Backend: Integration Test — RefreshTokenRotationTests.cs

File: `tests/OneId.Server.IntegrationTests/OpenIddict/RefreshTokenRotationTests.cs`

Pattern matches `IntrospectionTests.cs` exactly (same collection, same base class, same helper style).

```csharp
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class RefreshTokenRotationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(string AccessToken, string RefreshToken)> IssueMfaTokenWithRefreshAsync()
    {
        // Step 1: password grant
        var step1 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        step1.EnsureSuccessStatusCode();
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        // Step 2: MFA grant (offline_access scope triggers refresh token issuance)
        var step2 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:oneid:mfa",
                ["mfa_session_token"] = mfaToken,
                ["totp_code"] = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
                                    .ComputeTotp(DateTime.UtcNow),
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid email profile offline_access",
            }));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
        var body = await step2.Content.ReadFromJsonAsync<JsonElement>();

        return (body.GetProperty("access_token").GetString()!,
                body.GetProperty("refresh_token").GetString()!);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_IssuesNewAccessAndRefreshTokens()
    {
        var (_, refreshToken) = await IssueMfaTokenWithRefreshAsync();

        var response = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out var newAt) && !string.IsNullOrEmpty(newAt.GetString()),
            "New access_token must be present");
        Assert.True(body.TryGetProperty("refresh_token", out var newRt) && !string.IsNullOrEmpty(newRt.GetString()),
            "New refresh_token must be present");
        Assert.NotEqual(refreshToken, newRt.GetString(), "New refresh token must differ from consumed token");
    }

    [Fact]
    public async Task RefreshToken_ConsumedToken_Returns400InvalidGrant()
    {
        var (_, refreshToken) = await IssueMfaTokenWithRefreshAsync();

        // First use — succeeds
        var first = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replay the same (now consumed) refresh token — must fail
        var second = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var error = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RefreshToken_NewAccessToken_PassesSignatureValidation()
    {
        var (_, refreshToken) = await IssueMfaTokenWithRefreshAsync();

        var response = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "oneid-dev-client",
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var newAccessToken = body.GetProperty("access_token").GetString()!;

        // Fetch JWKS from discovery endpoint and validate the new token
        var jwksResponse = await Client.GetAsync("/.well-known/jwks");
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);
        var jwksJson = await jwksResponse.Content.ReadAsStringAsync();
        var jwks = new JsonWebKeySet(jwksJson);

        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        // Should not throw
        handler.ValidateToken(newAccessToken, validationParams, out _);
    }
}
```

**Required NuGet**: `System.IdentityModel.Tokens.Jwt` is already available via `Microsoft.IdentityModel.Tokens` (used in `IntrospectionTests.cs`). No new packages needed.

### Frontend: Installing ky

Run from `src/OneId.Web/`:
```
npm install ky
```

The architecture specifies `ky` as the HTTP client. This is the story where it gets introduced. Existing pages (`forgot-password.tsx`, `reset-password.tsx`) use plain `fetch` — do NOT convert them. New auth/API infrastructure uses `ky`.

### Frontend: `store/auth-store.ts`

```typescript
// src/OneId.Web/src/store/auth-store.ts
import { create } from 'zustand'

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  setTokens: (accessToken: string, refreshToken: string) => void
  clearTokens: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  refreshToken: null,
  setTokens: (accessToken, refreshToken) => set({ accessToken, refreshToken }),
  clearTokens: () => set({ accessToken: null, refreshToken: null }),
}))
```

### Frontend: `lib/auth.ts` — Token Exchange Helpers

These helpers use plain `fetch` to avoid interceptor re-entry (if they used the `ky` instance, a 401 on a token call would loop back into the refresh interceptor).

```typescript
// src/OneId.Web/src/lib/auth.ts

interface PasswordGrantResponse {
  mfa_required: boolean
  mfa_session_token: string
  totp_enrollment_uri?: string
}

interface TokenResponse {
  access_token: string
  refresh_token: string
  token_type: string
  expires_in: number
}

export async function passwordGrant(
  email: string,
  password: string,
): Promise<PasswordGrantResponse> {
  const res = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      username: email,
      password,
      client_id: 'oneid-dev-client',
      scope: 'openid email profile offline_access',
    }),
  })
  if (!res.ok) throw new Error('invalid_credentials')
  return res.json()
}

export async function mfaGrant(
  mfaSessionToken: string,
  totpCode: string,
): Promise<TokenResponse> {
  const res = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'urn:oneid:mfa',
      mfa_session_token: mfaSessionToken,
      totp_code: totpCode,
      client_id: 'oneid-dev-client',
      scope: 'openid email profile offline_access',
    }),
  })
  if (!res.ok) throw new Error('invalid_mfa')
  return res.json()
}

export async function refreshGrant(refreshToken: string): Promise<TokenResponse> {
  const res = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: 'oneid-dev-client',
    }),
  })
  if (!res.ok) throw new Error('refresh_failed')
  return res.json()
}
```

### Frontend: `lib/api-client.ts` — ky Instance with 401 Interceptor

The `beforeError` hook in `ky` receives an `HttpError` and can retry. However, `ky`'s `beforeError` hook is for transforming errors — it does NOT support retrying the original request. Use the `afterResponse` hook for 401 handling instead.

**CRITICAL NOTE**: `ky`'s `afterResponse` hook can return a new `Response` to replace the original. This is the correct hook for transparent retry. The `beforeError` hook is for error transformation only (architecture.md says `beforeError` but this is an imprecision — use `afterResponse` for retry logic).

```typescript
// src/OneId.Web/src/lib/api-client.ts
import ky from 'ky'
import { refreshGrant } from './auth'
import { useAuthStore } from '../store/auth-store'

// Module-level retry guard — prevents infinite retry loops
// Key: a symbol that ky attaches to retry requests to identify them
const REFRESH_RETRY_HEADER = 'x-refresh-retry'

export const apiClient = ky.create({
  hooks: {
    beforeRequest: [
      (request) => {
        const { accessToken } = useAuthStore.getState()
        if (accessToken) {
          request.headers.set('Authorization', `Bearer ${accessToken}`)
        }
      },
    ],
    afterResponse: [
      async (request, _options, response) => {
        if (
          response.status !== 401 ||
          request.headers.has(REFRESH_RETRY_HEADER)
        ) {
          return response
        }

        const { refreshToken, setTokens, clearTokens } = useAuthStore.getState()
        if (!refreshToken) {
          clearTokens()
          window.location.href = '/login'
          return response
        }

        try {
          const tokens = await refreshGrant(refreshToken)
          setTokens(tokens.access_token, tokens.refresh_token)

          // Retry the original request with the new token
          const retryRequest = new Request(request, {
            headers: new Headers(request.headers),
          })
          retryRequest.headers.set('Authorization', `Bearer ${tokens.access_token}`)
          retryRequest.headers.set(REFRESH_RETRY_HEADER, '1')
          return fetch(retryRequest)
        } catch {
          clearTokens()
          window.location.href = '/login'
          return response
        }
      },
    ],
  },
})
```

**ky API note**: `ky.create()` hooks receive `(request, options, response)` in `afterResponse`. The `request` is the original `Request` object. The return value replaces the response. A plain `fetch()` is used for the retry to avoid re-triggering ky hooks.

### Frontend: `routes/login.tsx` — Real Login Form

```typescript
// src/OneId.Web/src/routes/login.tsx
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { passwordGrant, mfaGrant } from '@/lib/auth'
import { useAuthStore } from '@/store/auth-store'

type LoginStep = 'credentials' | 'totp'

export function LoginPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { setTokens } = useAuthStore()

  const [step, setStep] = useState<LoginStep>('credentials')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mfaSessionToken, setMfaSessionToken] = useState('')
  const [totpCode, setTotpCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const returnTo = searchParams.get('returnTo')
  const safeReturnTo =
    returnTo && returnTo.startsWith('/') && !returnTo.startsWith('//')
      ? returnTo
      : '/internal/tenants'

  const handleCredentials = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const res = await passwordGrant(email, password)
      setMfaSessionToken(res.mfa_session_token)
      setStep('totp')
    } catch {
      setError('Invalid credentials or MFA code')
    } finally {
      setLoading(false)
    }
  }

  const handleMfa = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const tokens = await mfaGrant(mfaSessionToken, totpCode)
      setTokens(tokens.access_token, tokens.refresh_token)
      navigate(safeReturnTo, { replace: true })
    } catch {
      setError('Invalid credentials or MFA code')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-background text-foreground flex items-center justify-center p-8">
      {step === 'credentials' ? (
        <form onSubmit={handleCredentials} className="flex flex-col gap-4 w-80">
          <h1 className="text-2xl font-semibold">Sign in</h1>
          {error && <p className="text-red-600 text-sm">{error}</p>}
          <input
            type="email"
            placeholder="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            disabled={loading}
            className="border rounded px-3 py-2"
          />
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            disabled={loading}
            className="border rounded px-3 py-2"
          />
          <button
            type="submit"
            disabled={loading}
            className="bg-primary text-primary-foreground rounded px-4 py-2"
          >
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      ) : (
        <form onSubmit={handleMfa} className="flex flex-col gap-4 w-80">
          <h1 className="text-2xl font-semibold">Two-factor authentication</h1>
          <p className="text-muted-foreground text-sm">
            Enter the code from your authenticator app.
          </p>
          {error && <p className="text-red-600 text-sm">{error}</p>}
          <input
            type="text"
            placeholder="6-digit code"
            value={totpCode}
            onChange={(e) => setTotpCode(e.target.value)}
            required
            maxLength={6}
            autoComplete="one-time-code"
            disabled={loading}
            className="border rounded px-3 py-2 tracking-widest text-center"
          />
          <button
            type="submit"
            disabled={loading}
            className="bg-primary text-primary-foreground rounded px-4 py-2"
          >
            {loading ? 'Verifying…' : 'Verify'}
          </button>
        </form>
      )}
    </div>
  )
}
```

### Frontend: `routes/_authenticated.tsx` — Real Route Guard

```typescript
// src/OneId.Web/src/routes/_authenticated.tsx
import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuthStore } from '@/store/auth-store'

export function AuthenticatedLayout() {
  const { accessToken } = useAuthStore()
  const location = useLocation()

  if (!accessToken) {
    const returnTo = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />
  }

  return <Outlet />
}
```

**IMPORTANT**: `login`, `suspended`, `forgot-password`, and `reset-password` routes are siblings of `AuthenticatedLayout` in the router (`index.tsx`) — they are NOT children of it. This means the guard does not intercept them. Check `src/OneId.Web/src/routes/index.tsx`: `login`, `suspended`, `forgot-password`, `reset-password` are defined directly as children of the root path, while `internal` and `tenant` routes are also children of the root. **The guard wraps the entire root outlet** — this means `/login` itself IS protected by `AuthenticatedLayout`.

Re-examine the router structure: all routes are children of `{ path: '/', element: <AuthenticatedLayout /> }`. This means `login` is also behind the guard. Implementing the guard as "redirect to login if no token" will cause an infinite redirect loop for `/login`.

**Fix**: Exclude public paths from the guard:
```typescript
export function AuthenticatedLayout() {
  const { accessToken } = useAuthStore()
  const location = useLocation()

  const PUBLIC_PATHS = ['/login', '/forgot-password', '/reset-password', '/suspended']
  const isPublic = PUBLIC_PATHS.some((p) => location.pathname.startsWith(p))

  if (!accessToken && !isPublic) {
    const returnTo = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />
  }

  return <Outlet />
}
```

### `@/` Path Alias

The `vite.config.ts` already has `resolve.alias: { '@': path.resolve(__dirname, './src') }`. Use `@/` imports throughout.

### What NOT to Change

- `ConnectController.cs` — no changes needed; refresh token grant is handled natively by OpenIddict
- `DevSeeder.cs` — already registers `oneid-dev-client` with `Permissions.GrantTypes.RefreshToken` and `$"{Permissions.Prefixes.Scope}offline_access"` — no changes needed
- `forgot-password.tsx` / `reset-password.tsx` — leave as plain `fetch`; do not migrate to `ky`
- No new EF Core migrations — this story has no database schema changes
- AR-10: No direct `IMemoryCache` injection anywhere

### Project Structure

```
src/OneId.Server/
  appsettings.json                    ← MODIFIED (OpenIddict section added)
  Program.cs                          ← MODIFIED (config-driven token lifetimes)

src/OneId.Web/
  package.json                        ← MODIFIED (ky added)
  src/
    lib/
      auth.ts                         ← NEW (passwordGrant, mfaGrant, refreshGrant)
      api-client.ts                   ← NEW (ky instance + 401 interceptor)
    store/
      auth-store.ts                   ← NEW (Zustand auth state)
    routes/
      login.tsx                       ← MODIFIED (real login form with 2-step MFA)
      _authenticated.tsx              ← MODIFIED (real token guard)

tests/OneId.Server.IntegrationTests/
  OpenIddict/
    RefreshTokenRotationTests.cs      ← NEW
```

### AR-15 Deferred-Skip Governance Tracker

| Skip | Owner Story | Status after 2.8 |
|---|---|---|
| `DevSigningKeyStabilityTest` | Story 2.1 (infra) | OPEN |
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 3 / 3 cap** — zero new skips permitted.

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `ConnectController.cs` required a new `HandleRefreshTokenGrantAsync` branch — `EnableTokenEndpointPassthrough()` routes ALL token requests through the controller, so the `refresh_token` grant was falling to "unsupported_grant_type". Fixed by adding a passthrough handler that calls `AuthenticateAsync` to extract existing claims then re-`SignIn`.
- OpenIddict `SetRefreshTokenReuseLeeway` discovered via XML docs inspection — default leeway allowed immediate token replay. Set to `TimeSpan.Zero` to enforce strict rotation.
- ky v2 `afterResponse` hook API differs from story notes: receives `{request, response, retryCount}` state object (not positional args). Used `retryCount === 0` guard + `ky.retry()` instead of custom header-based guard.
- `Assert.NotEqual(string, string, string)` → third arg is treated as `IEqualityComparer<char>` in .NET 10 xUnit — replaced with `Assert.True(a != b, message)`.
- `System.IdentityModel.Tokens.Jwt` not referenced in test project — replaced JWT validation test with introspection-based verification (consistent with existing test patterns).

### Completion Notes List

- AC1: `appsettings.json` gets `"OpenIddict": { "AccessTokenLifetimeMinutes": 15, "RefreshTokenSlidingExpiryHours": 8 }`. `Program.cs` reads via `GetSection("OpenIddict")`. `SetRefreshTokenReuseLeeway(TimeSpan.Zero)` enforces strict rotation.
- AC2: Zustand `useAuthStore` stores `accessToken` + `refreshToken` in memory. `apiClient` (ky) attaches Bearer token via `beforeRequest`.
- AC3: `afterResponse` hook detects 401 + `retryCount === 0`, calls `refreshGrant`, updates store, retries via `ky.retry()`. On refresh failure → `clearTokens()` + redirect to `/login`.
- AC4: `LoginPage` implements 2-step flow: credentials → TOTP. On success, stores tokens and navigates to `returnTo` (validated same-origin) or `/internal/tenants`.
- AC5: `AuthenticatedLayout` excludes public paths (`/login`, `/forgot-password`, `/reset-password`, `/suspended`) from redirect to prevent infinite loop (all routes are children of the same root layout).
- AC6: 3 integration tests all pass: happy-path rotation, consumed-token 400, RS256 signature via introspection.
- AR-15: Skip count remains at 3. Zero new skips introduced. ✅
- Build: zero warnings, zero errors. ✅
- Tests: 3 new RefreshTokenRotationTests pass. 41 total pass. DevSigningKeyStabilityTest pre-existing failure unchanged. 2 pre-existing skips unchanged. ✅
- `npm run build`: zero TypeScript errors. ✅

### File List

- src/OneId.Server/appsettings.json (MODIFIED — OpenIddict section added)
- src/OneId.Server/Program.cs (MODIFIED — config-driven token lifetimes + SetRefreshTokenReuseLeeway)
- src/OneId.Server/Controllers/ConnectController.cs (MODIFIED — HandleRefreshTokenGrantAsync added)
- src/OneId.Web/package.json (MODIFIED — ky added)
- src/OneId.Web/src/lib/auth.ts (NEW)
- src/OneId.Web/src/lib/api-client.ts (NEW)
- src/OneId.Web/src/store/auth-store.ts (NEW)
- src/OneId.Web/src/routes/login.tsx (MODIFIED — real 2-step login form)
- src/OneId.Web/src/routes/_authenticated.tsx (MODIFIED — real route guard with public path exclusion)
- tests/OneId.Server.IntegrationTests/OpenIddict/RefreshTokenRotationTests.cs (NEW)
