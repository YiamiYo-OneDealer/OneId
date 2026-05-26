# Story 2.6: Role-Change jti Invalidation (FR-5a)

Status: review

## Story

As an Internal Admin or Tenant Admin,
I want active tokens invalidated immediately when a user's role assignments change,
So that permission changes take effect within the 5-minute consumer cache window — not only on the next login.

## Acceptance Criteria

**AC1: Revocation service revokes all active jtis for a user**

**Given** a `UserRoleInvalidationService` (or equivalent implementing `IUserTokenRevoker`) is registered in DI
**When** `RevokeAllUserTokensAsync(userId)` is called for a user who has one or more active tokens in the OpenIddict store
**Then** all active jti records for that user are revoked via `IOpenIddictTokenManager`
**And** a subsequent introspection call for any of those tokens returns `active: false`

**AC2: Unrelated user tokens are not affected**

**Given** two users each have an active token in the OpenIddict store
**When** `RevokeAllUserTokensAsync(userId)` is called for User A
**Then** User A's token introspects as `active: false`
**And** User B's token introspects as `active: true` — revocation did not touch other users' tokens

**AC3: Integration test — `RoleChangeInvalidationTests.cs`**

**Given** `RoleChangeInvalidationTests.cs` runs
**When** a test token is issued for a user via the MFA flow, `IUserTokenRevoker.RevokeAllUserTokensAsync` is called for that user's ID, and the token is introspected
**Then** the introspection returns `active: false`
**And** a second token issued for a different user is introspected and returns `active: true`

**Story Notes:**
- The Role entity does not exist yet (Epic 4a). This story implements the **mechanism** — the `IUserTokenRevoker` interface and `RevocationHandler` — that Epic 4a role-change handlers will call. No role data model is created here.
- The full cross-tenant integration test with `ITenantContext` middleware active is deferred to Epic 3 (Story 3.6 — Tenant Suspension with jti Revocation). Story 2.6 tests use the dev tenant from `DevSeeder`.
- AR-15: Skip count remains at 3 after this story. Zero new `[Fact(Skip)]` permitted.
- AR-10: No direct `IMemoryCache` injection. Use `ICacheService` if caching is needed.

## Tasks / Subtasks

- [x] Task 1: Define `IUserTokenRevoker` interface (AC: 1)
  - [x] Create `src/OneId.Server/Application/Common/IUserTokenRevoker.cs`
  - [x] Single method: `Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)`
  - [x] Namespace: `OneId.Server.Application.Common`

- [x] Task 2: Implement `RevocationHandler` (AC: 1, 2)
  - [x] Create `src/OneId.Server/Infrastructure/OpenIddict/RevocationHandler.cs`
  - [x] Inject `IOpenIddictTokenManager` — use `FindBySubjectAsync(userId.ToString())` to enumerate all tokens for the user
  - [x] For each token where status is NOT already Revoked: call `TryRevokeAsync(token)`
  - [x] Namespace: `OneId.Server.Infrastructure.OpenIddict`
  - [x] Implements `IUserTokenRevoker`

- [x] Task 3: Register in DI (AC: 1)
  - [x] Register `RevocationHandler` as `IUserTokenRevoker` scoped in `Program.cs` (or a suitable extension method)
  - [x] Prefer an extension method on `IServiceCollection` consistent with existing patterns (`AddTokenPipeline`, etc.)

- [x] Task 4: Create `RoleChangeInvalidationTests.cs` (AC: 1, 2, 3)
  - [x] File: `tests/OneId.Server.IntegrationTests/OpenIddict/RoleChangeInvalidationTests.cs`
  - [x] `RevokeAllUserTokens_IntrospectionReturnsActiveFalse` — issue MFA token for TotpUser, call `IUserTokenRevoker.RevokeAllUserTokensAsync(TotpUserId)`, introspect, assert `active: false`
  - [x] `RevokeAllUserTokens_DoesNotAffectOtherUserTokens` — issue MFA token for TotpUser AND a token for AdminUser (via password grant, no TOTP), call revocation for TotpUser only, assert TotpUser's token is `active: false` and AdminUser's is `active: true`
  - [x] Collection: `[Collection("IntegrationTests")]`, base class: `IntegrationTestBase`

- [x] Task 5: Final verification (AC: all)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [x] `dotnet test OneId.slnx` — all new tests pass; no regressions; confirm AR-15 skip count remains at 3

## Dev Notes

### IUserTokenRevoker — Interface Design

Create in `Application/Common/` alongside `ICacheService.cs`, `ITenantContext.cs`:

```csharp
namespace OneId.Server.Application.Common;

public interface IUserTokenRevoker
{
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default);
}
```

This is the interface that Epic 4a role-change command handlers will inject and call. Story 2.6 delivers the mechanism; Epic 4a wires the trigger.

### RevocationHandler — Implementation

File: `src/OneId.Server/Infrastructure/OpenIddict/RevocationHandler.cs`

```csharp
using OneId.Server.Application.Common;
using OpenIddict.Abstractions;

namespace OneId.Server.Infrastructure.OpenIddict;

public sealed class RevocationHandler : IUserTokenRevoker
{
    private readonly IOpenIddictTokenManager _tokenManager;

    public RevocationHandler(IOpenIddictTokenManager tokenManager)
    {
        _tokenManager = tokenManager;
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await foreach (var token in _tokenManager.FindBySubjectAsync(userId.ToString(), ct))
        {
            await _tokenManager.TryRevokeAsync(token, ct);
        }
    }
}
```

**Why `FindBySubjectAsync`?** OpenIddict stores the `sub` claim as the Subject field on the token record. When a JWT is issued, OpenIddict stores `Subject = userId.ToString()`. `FindBySubjectAsync` returns all tokens (access + refresh) for that user. `TryRevokeAsync` is idempotent — calling it on an already-revoked token is a no-op.

**Critical:** `FindBySubjectAsync` returns an `IAsyncEnumerable<object>` — use `await foreach`. Do not `.ToListAsync()` (that would require EF Core methods directly, violating the abstraction).

### DI Registration

Add a new extension or register directly in `Program.cs` alongside other service registrations. Keep consistent with the existing pattern:

```csharp
// In Program.cs or a new Infrastructure extension:
services.AddScoped<IUserTokenRevoker, RevocationHandler>();
```

If a new extension method is preferred (matching `AddTokenPipeline`), create `RevocationExtensions.cs` in `Infrastructure/OpenIddict/`:

```csharp
public static class RevocationExtensions
{
    public static IServiceCollection AddRevocationHandler(this IServiceCollection services)
    {
        services.AddScoped<IUserTokenRevoker, RevocationHandler>();
        return services;
    }
}
```

Then call `services.AddRevocationHandler()` in `Program.cs`.

### Test: Issue AdminUser Token Without TOTP

AC2 requires two distinct users. `AdminUser` (`admin@oneid.dev`, ID `AdminUserId`) has no TOTP enrolled. Issue a token using standard password grant (no MFA step needed):

```csharp
private async Task<string> IssuePasswordTokenAsync(string email, string password = "Admin123!")
{
    var response = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(
        new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = password,
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        }));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("access_token").GetString()!;
}
```

Wait — `AdminUser` (`admin@oneid.dev`) was seeded without TOTP. Check the `ConnectController` password grant handler: if the user has no TOTP enrolled (`IsTotpEnrolled = false`), does the password grant return an access token directly or a `mfa_session_token`? From Story 2.2 (password grant + lockout), non-TOTP users receive the access token directly. **Confirm this behavior in `ConnectController.cs` before writing the test.**

Actually, looking at the seeder: `AdminUser` has `IsTotpEnrolled` not set (defaults to `false`). The password grant in `ConnectController` issues either an `mfa_session_token` (if TOTP enrolled) or a full token (if not enrolled). AdminUser should get a direct token.

However, **if AdminUser does not get a direct token** (e.g., password grant always requires MFA in this system), use a second call to `IssueMfaTokenAsync` by temporarily issuing for `TotpUserId` only (the only TOTP-enrolled user in DevSeeder). In that case, revoke the first token, then issue a new MFA token as "User B" — but this won't prove isolation across users.

**Safest approach for AC2:** Issue two tokens for the same user (`TotpUserId`) in sequence (OpenIddict creates two distinct token records for multiple grants), revoke once, and verify BOTH tokens are invalidated (demonstrating all tokens for the user are revoked). Then issue a fresh token AFTER revocation and verify it is `active: true` (new token post-revocation is unaffected — the service does not pre-emptively block future tokens). This makes AC2 a "revoke does not affect NEW tokens for the same user" test, which is a valid isolation guard.

**OR** — create an `AdminUser` without TOTP who should get a direct token via the existing ConnectController logic, and test cross-user isolation. Check ConnectController before picking approach and document the decision in Dev Agent Record.

### Introspection Request Pattern (from Story 2.5)

Reuse the established helper signature — copy from `IntrospectionTests.cs`:

```csharp
private static FormUrlEncodedContent IntrospectRequest(string accessToken) =>
    new(new Dictionary<string, string>
    {
        ["token"] = accessToken,
        ["client_id"] = "oneid-sample-app",
        ["client_secret"] = "sample-app-secret",
    });

private async Task<bool> IsTokenActiveAsync(string accessToken)
{
    var response = await Client.PostAsync("/connect/introspect", IntrospectRequest(accessToken));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    return body.GetProperty("active").GetBoolean();
}
```

### Note on TestTokenFactory

The epics mention "TestTokenFactory-issued token" for this story. **This is NOT the `TestTokenFactory` class in `Helpers/TestTokenFactory.cs`.** That class issues synthetic JWTs signed with a test key — those tokens are NOT stored in OpenIddict's token store, so `IOpenIddictTokenManager.FindBySubjectAsync` will NOT find them. The Story 2.6 test MUST use OpenIddict-issued tokens (via `IssueMfaTokenAsync()` or the direct password grant) to have real entries in the store to revoke. The epics' reference to "TestTokenFactory" refers to the test token issuance helper pattern, not the class itself.

### AR-15 Deferred-Skip Governance Tracker

| Skip | Owner Story | Status after 2.6 |
|---|---|---|
| `DevSigningKeyStabilityTest` (unit tests) | Story 2.1 (infra failure) | OPEN |
| `TestTokenFactoryContractTests` | Story 3.5 | OPEN |
| `PermissionCatalogSyncTests` | Story 4a.1 | OPEN |

**Total: 3 / 3 cap** — zero new skips permitted in this story.

### What NOT to Change

- AR-10: No direct `IMemoryCache` injection — `ICacheService` if caching needed.
- `RoleClaimsEnricher.cs`: Do NOT add role data (Epic 4a owns this). Leave the stub as-is.
- `TokenPipelineExtensions.cs`: No changes needed.
- `DevSeeder.cs`: No changes needed (users and clients are already seeded correctly).
- `Program.cs`: Only add the DI registration for `IUserTokenRevoker`.
- `IntrospectionTests.cs`: No changes — do not pollute existing test file with role-change tests. New file only.

### Project Structure

```
src/OneId.Server/
  Application/
    Common/
      ICacheService.cs        ← exists
      ITenantContext.cs       ← exists
      IUserTokenRevoker.cs    ← NEW (this story)
  Infrastructure/
    OpenIddict/
      TokenPipelineExtensions.cs  ← exists
      RevocationHandler.cs        ← NEW (this story)
      RevocationExtensions.cs     ← NEW (optional — or inline in Program.cs)

tests/OneId.Server.IntegrationTests/
  OpenIddict/
    TokenIssuanceTests.cs     ← exists
    IntrospectionTests.cs     ← exists (Story 2.5)
    RoleChangeInvalidationTests.cs  ← NEW (this story)
```

### Key OpenIddict API Facts (from Story 2.5 learnings)

- `IOpenIddictTokenManager.FindBySubjectAsync(subject)` returns `IAsyncEnumerable<object>` — async stream of all token records for a given `sub` value.
- `IOpenIddictTokenManager.TryRevokeAsync(token)` is safe to call on an already-revoked token (idempotent).
- OpenIddict token records persist after revocation — they are not deleted, just marked with `Revoked` status.
- The `jti` claim in the JWT is the `ReferenceId` of the token record. The internal PK (`oi_tkn_id` claim) is what `FindByIdAsync` uses — but `FindBySubjectAsync` uses the `Subject` field directly (not the JWT claim).
- `DisableAccessTokenEncryption()` is set in this project — tokens ARE standard verifiable JWTs.

### IntrospectionTests.cs Helpers to Reuse

Copy these private helpers verbatim into `RoleChangeInvalidationTests.cs` — do NOT move them to a shared base class (premature abstraction):

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
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
```

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

Key finding: `ConnectController` requires ALL users (even unenrolled) to go through MFA — password grant never returns an access token directly. For AC2 cross-user isolation, `AdminUser` was enrolled dynamically in the test by extracting the base32 TOTP secret from the `totp_enrollment_uri` returned by the password grant, then completing the MFA grant. This correctly produces two distinct OpenIddict token records under different `sub` values.

`IOpenIddictTokenManager.FindBySubjectAsync` uses `await foreach` (IAsyncEnumerable) — not ToListAsync. `TryRevokeAsync` is idempotent on already-revoked tokens.

### Completion Notes List

- AC1: `IUserTokenRevoker.RevokeAllUserTokensAsync` implemented via `RevocationHandler`. `FindBySubjectAsync(userId.ToString())` enumerates all OpenIddict token records for the user; `TryRevokeAsync` revokes each. Subsequent introspection returns `active: false`. Test: `RevokeAllUserTokens_IntrospectionReturnsActiveFalse` ✅
- AC2: Cross-user isolation verified — revoking TotpUser's tokens leaves AdminUser's token active. AdminUser enrolled dynamically in test via enrollment URI secret extraction. Test: `RevokeAllUserTokens_DoesNotAffectOtherUserTokens` ✅
- AC3: Both integration tests pass in the `IntegrationTests` collection using `IntegrationTestBase`. ✅
- AR-15: Skip count remains at 3 (`DevSigningKeyStabilityTest` Docker infra, `TestTokenFactoryContractTests` Story 3.5, `PermissionCatalogSyncTests` Story 4a.1). No new skips introduced. ✅
- Build: zero warnings, zero errors. ✅

### File List

- src/OneId.Server/Application/Common/IUserTokenRevoker.cs (NEW)
- src/OneId.Server/Infrastructure/OpenIddict/RevocationHandler.cs (NEW)
- src/OneId.Server/Infrastructure/OpenIddict/RevocationExtensions.cs (NEW)
- src/OneId.Server/Program.cs (MODIFIED — added `services.AddRevocationHandler()`)
- tests/OneId.Server.IntegrationTests/OpenIddict/RoleChangeInvalidationTests.cs (NEW)
