# Story 2.7: Password Reset via Email Link

Status: review

## Story

As an end user,
I want to reset my password via a time-limited email link without contacting an admin,
So that I can regain account access independently while keeping the process secure.

## Acceptance Criteria

**AC1: Forgot-password endpoint — no user enumeration**

**Given** a `POST /account/forgot-password` request with a registered email
**When** the request is processed
**Then** a password reset token (1-hour expiry) is generated and stored server-side on the User record
**And** an email is sent to that address containing a reset link with the token as a query parameter
**And** the response is always HTTP 202 (identical for unknown email — no user enumeration)

**Given** a `POST /account/forgot-password` request with an unregistered email
**When** the request is processed
**Then** the response is HTTP 202 with no indication that the email is unregistered
**And** no email is sent

**AC2: Reset-password endpoint — happy path**

**Given** a valid, non-expired reset token is submitted to `POST /account/reset-password` with a new password
**When** the request is processed
**Then** the password is updated (hashed with the existing `IPasswordHasher<User>` PBKDF2 scheme)
**And** the reset token is consumed (nulled out) — single-use enforced
**And** all active jti records for the user are revoked via `IUserTokenRevoker`
**And** the response is HTTP 200

**AC3: Reset-password endpoint — rejection cases**

**Given** a reset token older than 1 hour is submitted
**When** the reset is attempted
**Then** the response is HTTP 400 with `{ "error": "invalid_or_expired_token" }`

**Given** a token that does not match any user is submitted
**When** the reset is attempted
**Then** the response is HTTP 400 with `{ "error": "invalid_or_expired_token" }` (same message — no info leak)

**Given** a token that has already been used is submitted again
**When** the reset is attempted
**Then** the response is HTTP 400 with `{ "error": "invalid_or_expired_token" }`

**Given** the submitted new password is identical to the user's current password
**When** the reset is attempted
**Then** the response is HTTP 400 with `{ "error": "password_reuse" }`

**AC4: Frontend — Forgot Password page**

**Given** a user navigates to `/forgot-password`
**When** the page renders
**Then** a form with a single email input and a submit button is shown
**And** on successful submit, a confirmation message appears: "If this email is registered, you will receive a reset link" (no redirect — stay on page)
**And** the form is disabled during submission (no double-submit)

**AC5: Frontend — Reset Password page**

**Given** a user navigates to `/reset-password?token=<token>`
**When** the page renders
**Then** a form with new-password and confirm-password fields is shown
**And** on successful submit, the user is navigated to `/login` with a visible success message
**And** on error (400 from backend), an inline error message is shown: "This reset link is invalid or has expired."
**And** password and confirm-password must match before submission (client-side validation)

**Story Notes:**
- **Password hashing**: The architecture specifies Argon2id, but the current codebase uses `IPasswordHasher<User>` (PBKDF2 via ASP.NET Identity). This story uses the existing `IPasswordHasher<User>` for consistency — switching to Argon2id is a separate migration concern that would also require re-hashing existing users.
- **Email in dev**: `LoggingEmailSender` logs the reset link to Serilog — no real SMTP. The email body includes the raw link so developers can copy it from logs.
- **Token storage**: Raw GUID token stored on the `User` entity. Sufficient for POC; production hardening (HMAC, DB-side encryption) is deferred.
- **Design system polish**: Explicitly out of scope. Functional loop only.
- **UI toast**: No toast library is installed. Use a simple inline success state on the reset page + `useNavigate` to `/login` with `?reset=success` so the login page can show a message if needed. Keep it simple.
- AR-15: Skip count remains at 3. Zero new `[Fact(Skip)]` permitted.
- AR-10: No direct `IMemoryCache` injection.

## Tasks / Subtasks

- [x] Task 1: Add `IEmailSender` interface and `LoggingEmailSender` implementation (AC: 1)
  - [x] Create `src/OneId.Server/Application/Common/IEmailSender.cs`
  - [x] Create `src/OneId.Server/Infrastructure/Email/LoggingEmailSender.cs` — logs the email to Serilog at Information level
  - [x] Create `src/OneId.Server/Infrastructure/Email/EmailExtensions.cs` — `AddEmailSender()` registers `IEmailSender` as `LoggingEmailSender` (scoped)
  - [x] Register in `Program.cs`: `builder.Services.AddEmailSender()`

- [x] Task 2: Add password reset fields to `User` entity and migrate (AC: 1, 2, 3)
  - [x] Add `PasswordResetToken` (string?) and `PasswordResetTokenExpiry` (DateTimeOffset?) to `User.cs`
  - [x] Run `dotnet ef migrations add AddPasswordResetFields --project src/OneId.Server --startup-project src/OneId.Server`
  - [x] Verify migration file generated correctly (two nullable columns on the users table)
  - [x] Run `dotnet build` to confirm no issues

- [x] Task 3: Create `AccountController` with forgot-password and reset-password endpoints (AC: 1, 2, 3)
  - [x] Create `src/OneId.Server/Controllers/AccountController.cs`
  - [x] `POST /account/forgot-password` — accepts `{ email }`, returns 202 always
  - [x] `POST /account/reset-password` — accepts `{ token, newPassword }`, returns 200 or 400
  - [x] Wire `IEmailSender` and `IUserTokenRevoker` via constructor injection

- [x] Task 4: Create integration tests `PasswordResetTests.cs` (AC: 1, 2, 3)
  - [x] File: `tests/OneId.Server.IntegrationTests/PasswordResetTests.cs`
  - [x] `ForgotPassword_RegisteredEmail_Returns202AndStoresToken`
  - [x] `ForgotPassword_UnregisteredEmail_Returns202WithNoTokenStored`
  - [x] `ResetPassword_ValidToken_UpdatesPasswordAndRevokesTokens`
  - [x] `ResetPassword_ExpiredToken_Returns400`
  - [x] `ResetPassword_AlreadyUsedToken_Returns400`
  - [x] `ResetPassword_PasswordReuse_Returns400`
  - [x] `ResetPassword_InvalidToken_Returns400`

- [x] Task 5: Frontend — add `/forgot-password` page (AC: 4)
  - [x] Create `src/OneId.Web/src/routes/forgot-password.tsx` — `ForgotPasswordPage` component
  - [x] Email input + submit button; on success show confirmation message (stay on page)
  - [x] `fetch` POST to `/account/forgot-password`

- [x] Task 6: Frontend — add `/reset-password` page (AC: 5)
  - [x] Create `src/OneId.Web/src/routes/reset-password.tsx` — `ResetPasswordPage` component
  - [x] Read `?token` from `useSearchParams()` (React Router 7)
  - [x] New-password + confirm-password fields; client-side match validation
  - [x] On success: `navigate('/login?reset=success')`
  - [x] On 400: show inline error "This reset link is invalid or has expired."
  - [x] `fetch` POST to `/account/reset-password`

- [x] Task 7: Register frontend routes in router (AC: 4, 5)
  - [x] Update `src/OneId.Web/src/routes/index.tsx` — add `/forgot-password` and `/reset-password` routes inside the root route alongside `login`
  - [x] Import and register `ForgotPasswordPage` and `ResetPasswordPage`

- [x] Task 8: Final verification (AC: all)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [x] `dotnet test OneId.slnx` — all new tests pass; no regressions; AR-15 skip count remains at 3

## Dev Notes

### IEmailSender Interface

Create minimal interface in `Application/Common/`:

```csharp
namespace OneId.Server.Application.Common;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
```

### LoggingEmailSender — Dev Implementation

Logs the full email to Serilog. Developers read the reset link from the structured log output (Seq in Docker dev stack):

```csharp
using Microsoft.Extensions.Logging;
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Email;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL] To: {To} | Subject: {Subject} | Body: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}
```

### EmailExtensions DI Registration

```csharp
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Email;

public static class EmailExtensions
{
    public static IServiceCollection AddEmailSender(this IServiceCollection services)
    {
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        return services;
    }
}
```

Call `builder.Services.AddEmailSender()` in `Program.cs` next to `AddRevocationHandler()`.

### User Entity — New Fields

Add to `User.cs`:
```csharp
public string? PasswordResetToken { get; set; }
public DateTimeOffset? PasswordResetTokenExpiry { get; set; }
```

EF Core migration command (run from repo root):
```
dotnet ef migrations add AddPasswordResetFields --project src/OneId.Server --startup-project src/OneId.Server
```

The migration will add two nullable columns to the `users` table. No configuration needed in `UserConfiguration.cs` — default nullable mapping is correct.

### AccountController — Full Design

```csharp
[ApiController]
[Route("account")]
public class AccountController(
    AppDbContext db,
    IEmailSender emailSender,
    IPasswordHasher<User> hasher,
    IUserTokenRevoker revoker) : ControllerBase
```

**POST /account/forgot-password**:
1. Accept `ForgotPasswordRequest { string Email }` (JSON body)
2. Lookup user: `db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null)`
3. If user is null → return `Accepted()` immediately (no email sent)
4. Generate token: `Guid.NewGuid().ToString("N")` (32 hex chars, 128-bit entropy)
5. Set `user.PasswordResetToken = token`, `user.PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddHours(1)`
6. `db.SaveChangesAsync()`
7. Build reset link: `$"http://localhost:3000/reset-password?token={token}"` — use `IConfiguration` or a hardcoded dev URL. For dev, hardcode as `http://localhost:3000/reset-password?token={token}` (production URL injection is future work)
8. Send email: `emailSender.SendAsync(user.Email, "Reset your OneId password", $"Click to reset: {resetLink}")`
9. Return `Accepted()`

**POST /account/reset-password**:
1. Accept `ResetPasswordRequest { string Token, string NewPassword }` (JSON body)
2. Lookup user: `db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token && u.DeletedAt == null)`
3. If user is null OR `user.PasswordResetTokenExpiry < DateTimeOffset.UtcNow` → return `BadRequest(new { error = "invalid_or_expired_token" })`
4. No-reuse check: `hasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, request.NewPassword) != PasswordVerificationResult.Failed` → return `BadRequest(new { error = "password_reuse" })`
5. Update password: `user.PasswordHash = hasher.HashPassword(user, request.NewPassword)`
6. Consume token: `user.PasswordResetToken = null; user.PasswordResetTokenExpiry = null`
7. `user.UpdatedAt = DateTimeOffset.UtcNow`
8. `db.SaveChangesAsync()`
9. Revoke all active jtis: `await revoker.RevokeAllUserTokensAsync(user.Id, ct)`
10. Return `Ok()`

**IMPORTANT — expiry check ordering**: Check token lookup first (returns user or null by matching token string). If found, THEN check expiry. An expired token's user record has a non-null `PasswordResetToken` that still matches — the lookup succeeds but the expiry check fails. This correctly treats expired tokens as "invalid_or_expired_token" without leaking whether the token was valid.

**IMPORTANT — single-use enforcement**: The token is nulled on step 6. A second request with the same token will not find a matching user (token is null) → returns "invalid_or_expired_token". No separate "used" flag needed.

### Integration Test Design

File: `tests/OneId.Server.IntegrationTests/PasswordResetTests.cs`

**Test helper — trigger forgot-password and extract token from log**:

The `LoggingEmailSender` logs the token to Serilog. In integration tests, we can't easily intercept Serilog output. **Better approach**: in the test, directly read the `PasswordResetToken` from the database after calling `/account/forgot-password`.

```csharp
// After POST /account/forgot-password:
using var scope = Factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var user = await db.Users.IgnoreQueryFilters()
    .FirstAsync(u => u.Email == DevSeeder.TotpUserEmail);
var token = user.PasswordResetToken!;
```

This is the correct integration test pattern — the endpoint stores the token in the DB, and the test reads it from there.

**Test: `ForgotPassword_RegisteredEmail_Returns202AndSendsEmail`**
1. POST `/account/forgot-password` with `DevSeeder.TotpUserEmail`
2. Assert 202
3. Read `PasswordResetToken` from DB — assert not null
4. Assert `PasswordResetTokenExpiry` is within 1 hour from now (approximately)

**Test: `ForgotPassword_UnregisteredEmail_Returns202WithNoEmail`**
1. POST `/account/forgot-password` with `nobody@unknown.dev`
2. Assert 202
3. Assert `TotpUser.PasswordResetToken` is still null (no DB write occurred)

**Test: `ResetPassword_ValidToken_UpdatesPasswordAndRevokesTokens`**
1. Issue MFA token for TotpUser (for jti revocation assertion)
2. POST `/account/forgot-password` for TotpUser
3. Read token from DB
4. POST `/account/reset-password` with token + new password `"NewPassword456!"`
5. Assert 200
6. Assert user's `PasswordResetToken` is null (consumed)
7. Assert introspection of the issued token returns `active: false` (jti revoked)
8. Assert login with new password works: POST `/connect/token` with new password returns 200 with `mfa_session_token`

**Test: `ResetPassword_ExpiredToken_Returns400`**
1. POST forgot-password for TotpUser
2. Read token from DB
3. Manually set `PasswordResetTokenExpiry` to `DateTimeOffset.UtcNow.AddHours(-2)` via DB scope
4. POST reset-password with that token
5. Assert 400 with `{ "error": "invalid_or_expired_token" }`

**Test: `ResetPassword_AlreadyUsedToken_Returns400`**
1. POST forgot-password for TotpUser
2. Read token from DB
3. POST reset-password with valid token (first use) → assert 200
4. POST reset-password again with same token → assert 400 with `invalid_or_expired_token`

**Test: `ResetPassword_PasswordReuse_Returns400`**
1. POST forgot-password for TotpUser
2. Read token from DB
3. POST reset-password with token + "Admin123!" (TotpUser's current password)
4. Assert 400 with `{ "error": "password_reuse" }`

**Test: `ResetPassword_InvalidToken_Returns400`**
1. POST reset-password with token `"aaaabbbbccccddddaaaabbbbccccdddd"` (random invalid token)
2. Assert 400 with `{ "error": "invalid_or_expired_token" }`

### IssueMfaTokenAsync Helper

Copy from `IntrospectionTests.cs` / `RoleChangeInvalidationTests.cs` — same pattern needed for `ResetPassword_ValidToken_UpdatesPasswordAndRevokesTokens`:

```csharp
private async Task<string> IssueMfaTokenAsync()
{
    var step1 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
        new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = DevSeeder.TotpUserEmail,
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
    var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("mfa_session_token").GetString()!;
    var totpCode = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
        .ComputeTotp(DateTime.UtcNow);
    var step2 = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
        new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["mfa_session_token"] = mfaToken,
            ["totp_code"] = totpCode,
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
    Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
    return (await step2.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("access_token").GetString()!;
}
```

### Required Usings for Test File

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
```

### Frontend — ForgotPasswordPage

Simple, no-polish form. Use `useState` + `fetch`. The `FormUrlEncodedContent` pattern used in tests doesn't apply to frontend; use `Content-Type: application/json`.

```tsx
// src/routes/forgot-password.tsx
import { useState } from 'react'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      await fetch('/account/forgot-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      })
      // Always show confirmation — no user enumeration
      setSubmitted(true)
    } finally {
      setLoading(false)
    }
  }

  if (submitted) {
    return (
      <div className="min-h-screen bg-background text-foreground flex items-center justify-center p-8">
        <div>
          <h1 className="text-2xl font-semibold mb-2">Check your email</h1>
          <p className="text-muted-foreground">
            If this email is registered, you will receive a reset link.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-background text-foreground flex items-center justify-center p-8">
      <form onSubmit={handleSubmit} className="flex flex-col gap-4 w-80">
        <h1 className="text-2xl font-semibold">Reset password</h1>
        <input
          type="email"
          placeholder="Email"
          value={email}
          onChange={e => setEmail(e.target.value)}
          required
          disabled={loading}
          className="border rounded px-3 py-2"
        />
        <button type="submit" disabled={loading} className="bg-primary text-primary-foreground rounded px-4 py-2">
          {loading ? 'Sending…' : 'Send reset link'}
        </button>
      </form>
    </div>
  )
}
```

### Frontend — ResetPasswordPage

Reads `?token` from the URL. Uses React Router 7's `useSearchParams`:

```tsx
// src/routes/reset-password.tsx
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (password !== confirm) {
      setError('Passwords do not match.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      const res = await fetch('/account/reset-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, newPassword: password }),
      })
      if (res.ok) {
        navigate('/login?reset=success')
      } else {
        setError('This reset link is invalid or has expired.')
      }
    } catch {
      setError('Something went wrong. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-background text-foreground flex items-center justify-center p-8">
      <form onSubmit={handleSubmit} className="flex flex-col gap-4 w-80">
        <h1 className="text-2xl font-semibold">Set new password</h1>
        {error && <p className="text-red-600 text-sm">{error}</p>}
        <input
          type="password"
          placeholder="New password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          required
          disabled={loading}
          className="border rounded px-3 py-2"
        />
        <input
          type="password"
          placeholder="Confirm new password"
          value={confirm}
          onChange={e => setConfirm(e.target.value)}
          required
          disabled={loading}
          className="border rounded px-3 py-2"
        />
        <button type="submit" disabled={loading} className="bg-primary text-primary-foreground rounded px-4 py-2">
          {loading ? 'Saving…' : 'Set password'}
        </button>
      </form>
    </div>
  )
}
```

### Router Update

In `src/OneId.Web/src/routes/index.tsx`, add two routes inside the root route (alongside `login` and `suspended`):

```tsx
import { ForgotPasswordPage } from './forgot-password'
import { ResetPasswordPage } from './reset-password'

// In the router children array (next to login/suspended):
{ path: 'forgot-password', element: <ForgotPasswordPage /> },
{ path: 'reset-password', element: <ResetPasswordPage /> },
```

### Vite Dev Proxy (IMPORTANT)

The frontend `fetch('/account/forgot-password')` uses a relative URL. In development, Vite must proxy this to the backend. Check `vite.config.ts` for an existing proxy config. If missing, add:

```ts
server: {
  proxy: {
    '/account': 'http://localhost:5000',
    '/connect': 'http://localhost:5000',
  }
}
```

Read `vite.config.ts` before making changes — do not overwrite existing config.

### AR-15 Deferred-Skip Governance Tracker

| Skip | Owner Story | Status after 2.7 |
|---|---|---|
| `DevSigningKeyStabilityTest` | Story 2.1 (infra) | OPEN |
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 3 / 3 cap** — zero new skips permitted.

### What NOT to Change

- AR-10: No direct `IMemoryCache` injection.
- `ConnectController.cs`: No changes needed.
- `DevSeeder.cs`: No changes needed.
- `RoleClaimsEnricher.cs`: No changes.
- **Do NOT** switch the global password hasher to Argon2id — this would break existing seeded user login. The reset endpoint uses the same `IPasswordHasher<User>` as the login flow.
- **Do NOT** add a MailKit or SendGrid NuGet package — `LoggingEmailSender` is the dev implementation for this story.

### Project Structure

```
src/OneId.Server/
  Application/
    Common/
      IEmailSender.cs         ← NEW
      IUserTokenRevoker.cs    ← exists (Story 2.6)
  Controllers/
    AccountController.cs      ← NEW
    ConnectController.cs      ← exists (no changes)
  Domain/
    Entities/
      User.cs                 ← MODIFIED (2 new nullable fields)
  Infrastructure/
    Email/
      EmailExtensions.cs      ← NEW
      LoggingEmailSender.cs   ← NEW
  Program.cs                  ← MODIFIED (AddEmailSender)

src/OneId.Web/src/
  routes/
    forgot-password.tsx       ← NEW
    reset-password.tsx        ← NEW
    index.tsx                 ← MODIFIED (2 new routes)

tests/OneId.Server.IntegrationTests/
  PasswordResetTests.cs       ← NEW
```

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

Password hashing note: `IPasswordHasher<User>` is PBKDF2 (not Argon2id as architecture specifies). Switching to Argon2id globally would break existing seeded user login. Used PBKDF2 for consistency; Argon2id migration is a future concern outside this story's scope.

Token strategy: Raw GUID (`Guid.NewGuid().ToString("N")`) stored as-is on `User.PasswordResetToken`. Single-use enforced by nulling the field on successful reset. Expired tokens: lookup succeeds (token matches) but expiry check fails → same `invalid_or_expired_token` response as invalid tokens (no info leak).

Vite proxy: Added `/account` and `/connect` proxy rules to `vite.config.ts` so frontend `fetch` calls reach the backend in dev mode.

### Completion Notes List

- AC1: `POST /account/forgot-password` always returns 202. For registered users: stores GUID token + 1-hour expiry on User entity, sends email via `LoggingEmailSender` (Serilog). For unknown emails: 202 with no DB write.
- AC2: `POST /account/reset-password` with valid token: updates password hash (PBKDF2), nulls token (single-use), sets `UpdatedAt`, revokes all jtis via `IUserTokenRevoker`. Returns 200.
- AC3: Expired token → 400 `invalid_or_expired_token`. Already-used token → 400 `invalid_or_expired_token`. Wrong token → 400 `invalid_or_expired_token`. Same password reuse → 400 `password_reuse`.
- AC4: `ForgotPasswordPage` at `/forgot-password` — email form, stays on page showing confirmation message after submit. No double-submit (button disabled during fetch).
- AC5: `ResetPasswordPage` at `/reset-password?token=...` — password + confirm fields, client-side match check, navigates to `/login?reset=success` on success, inline error on 400.
- AR-15: Skip count remains at 3. No new skips introduced. ✅
- Build: zero warnings, zero errors. ✅
- Tests: 7 new PasswordResetTests all pass. 38 pre-existing tests pass. Pre-existing DevSigningKeyStabilityTest infrastructure failure unchanged.

### File List

- src/OneId.Server/Application/Common/IEmailSender.cs (NEW)
- src/OneId.Server/Infrastructure/Email/LoggingEmailSender.cs (NEW)
- src/OneId.Server/Infrastructure/Email/EmailExtensions.cs (NEW)
- src/OneId.Server/Controllers/AccountController.cs (NEW)
- src/OneId.Server/Domain/Entities/User.cs (MODIFIED — 2 new nullable fields)
- src/OneId.Server/Infrastructure/Persistence/Migrations/[timestamp]_AddPasswordResetFields.cs (NEW — generated)
- src/OneId.Server/Infrastructure/Persistence/AppDbContextModelSnapshot.cs (MODIFIED — generated)
- src/OneId.Server/Program.cs (MODIFIED — AddEmailSender + using)
- src/OneId.Web/src/routes/forgot-password.tsx (NEW)
- src/OneId.Web/src/routes/reset-password.tsx (NEW)
- src/OneId.Web/src/routes/index.tsx (MODIFIED — 2 new routes + imports)
- src/OneId.Web/vite.config.ts (MODIFIED — dev proxy for /account and /connect)
- tests/OneId.Server.IntegrationTests/PasswordResetTests.cs (NEW)
