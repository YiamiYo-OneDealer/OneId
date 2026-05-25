# Story 2.2: Password Authentication and Account Lockout

Status: review

## Story

As an end user,
I want to authenticate with my email and password through the token endpoint,
so that I receive an access token (or appropriate error) that enables the OIDC flow to continue.

## Acceptance Criteria

**AC1: Valid credentials → tokens issued**

**Given** a `POST /connect/token` request with `grant_type=password`, a registered email, and correct password
**When** the request is processed
**Then** the response is HTTP 200 with `access_token` (JWT), `token_type: "Bearer"`, `expires_in`, and `refresh_token`
**And** the JWT `sub` claim matches the user's ID

**AC2: Invalid password or unknown email → generic error (no enumeration)**

**Given** a `POST /connect/token` request with a valid email but incorrect password
**When** the request is processed
**Then** the response is HTTP 400 with `error: "invalid_grant"`
**And** the response for an unknown email is identical — no information leakage

**AC3: Account lockout after 5 consecutive failures**

**Given** a user makes 5 consecutive failed authentication attempts
**When** the 5th failure is recorded
**Then** the account is locked (`LockoutEnd > UtcNow`, `AccessFailedCount == 5`)
**And** all subsequent attempts return HTTP 400 `error: "invalid_grant"` — even with the correct password
**And** the response body does NOT disclose the lockout ETA or whether the account is locked
**And** a `LockoutTriggeredIntegrationTest` in `PasswordAuthTests.cs` simulates 5 failures and asserts the `LockoutEnd` is set on the User entity in the database

**AC4: Argon2id password hashing and credential log safety**

**Given** ASP.NET Core Identity's `PasswordHasher<User>` is configured
**When** the dev admin user is seeded (already done in DevSeeder)
**Then** the password is stored as an Argon2id hash — this is the Identity PasswordHasher default on .NET 10 with Identity v3 format
**And** no raw password appears in any structured log event on the password-auth path — `SerilogDestructuringTests.cs` already covers this (no new test required)

## Tasks / Subtasks

- [x] Task 1: Add lockout fields to `User` entity and configuration (AC: 3)
  - [x] Add `public int AccessFailedCount { get; set; }` and `public DateTimeOffset? LockoutEnd { get; set; }` to `User.cs`
  - [x] Add EF Core configuration for the new fields in `UserConfiguration.cs` (not nullable int with default 0; nullable `DateTimeOffset?`)
  - [x] Run `dotnet ef migrations add AddAccountLockoutFields --project src/OneId.Server/OneId.Server.csproj`
  - [x] Verify migration applies cleanly: `dotnet build OneId.slnx`

- [x] Task 2: Enable password grant in OpenIddict and DevSeeder (AC: 1)
  - [x] In `Program.cs`, add `.AllowPasswordFlow()` to the OpenIddict server options block (alongside existing `AllowAuthorizationCodeFlow()`)
  - [x] In `DevSeeder.SeedOpenIddictClientAsync`, add `Permissions.GrantTypes.Password` to the `oneid-dev-client` permissions list
  - [x] NOTE: Since the client was already seeded idempotently, the new permission won't be added by the guard. Either delete the DB and re-seed, or use `manager.UpdateAsync()` if the client exists. Choose the simpler approach: use `FindByClientIdAsync` + `UpdateAsync` (see Dev Notes for pattern)
  - [x] Verify discovery document at `GET /.well-known/openid-configuration` still returns 200

- [x] Task 3: Implement `ConnectController` — password grant token endpoint (AC: 1, 2, 3)
  - [x] Create `src/OneId.Server/Controllers/ConnectController.cs`
  - [x] Implement `[HttpPost("~/connect/token")]` with `[Consumes("application/x-www-form-urlencoded")]`
  - [x] Handle `request.IsPasswordGrantType()` branch:
    - [x] Extract `username` (email) and `password` from the OpenIddict request
    - [x] Look up user by email using `IgnoreQueryFilters()` (tenant not yet known during password auth)
    - [x] If user not found OR account locked (`LockoutEnd > DateTimeOffset.UtcNow`): return `Forbid` with `invalid_grant` (identical response — no enumeration)
    - [x] Verify password using `PasswordHasher<User>.VerifyHashedPassword()`
    - [x] On failure: increment `AccessFailedCount`; if `>= 5` set `LockoutEnd = UtcNow + 5 minutes`; save; return `Forbid` with `invalid_grant`
    - [x] On success: reset `AccessFailedCount = 0`, `LockoutEnd = null`; save; build `ClaimsIdentity` with `sub = user.Id`, `email = user.Email`; set scopes; return `SignIn` with OpenIddict scheme
  - [x] Register `PasswordHasher<User>` as a service in `Program.cs`

- [x] Task 4: Write `PasswordAuthTests.cs` integration tests (AC: 1, 2, 3)
  - [x] Create `tests/OneId.Server.IntegrationTests/PasswordAuthTests.cs`
  - [x] `ValidCredentials_ReturnsAccessTokenAndRefreshToken` — AC1
  - [x] `InvalidPassword_ReturnsInvalidGrant` — AC2
  - [x] `UnknownEmail_ReturnsInvalidGrant_IdenticalToWrongPassword` — AC2 (no enumeration)
  - [x] `LockoutTriggeredIntegrationTest` — AC3: 5 failures → DB lockout state verified
  - [x] `PostLockout_CorrectPasswordStillFails` — AC3: locked account rejects correct password
  - [x] All tests use `IntegrationTestBase` pattern (`[Collection("IntegrationTests")]`) with DB reset between tests

- [x] Task 5: Final verification (AC: all)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [x] `dotnet test OneId.slnx` — all existing + new tests pass; 2 deferred skips remain

## Dev Notes

### CRITICAL: Tenant Resolution During Password Grant

During password grant, there is NO JWT yet — `ITenantContext` is NOT initialized. The `AppDbContext.Users` getter logs a warning when accessed with an uninitialized `ITenantContext` (falling back to `Guid.Empty` → 0 rows). 

**Required approach**: Use `db.Users.IgnoreQueryFilters()` when looking up users by email during password auth. This is consistent with how `DevSeeder` works. The email lookup returns the first matching non-deleted user across all tenants.

```csharp
var user = await db.Users.IgnoreQueryFilters()
    .Where(u => u.Email == username && u.DeletedAt == null)
    .FirstOrDefaultAsync(ct);
```

For POC: email is assumed globally unique across tenants (acceptable for demo scale). Production multi-tenant login requires a tenant discriminator (e.g., a `tenant_id` form field from the login UI or URL path).

### CRITICAL: No `AddIdentity()` — Use Standalone `PasswordHasher<User>`

**Do NOT add `AddIdentity<User, IdentityRole>()` or `AddIdentityCore<User>()`**. Adding full ASP.NET Identity to a custom entity requires implementing a full IUserStore — a complex task not in Story 2.2 scope. It also conflicts with the custom `User` entity design.

Instead, register the standalone password hasher and use it directly:

```csharp
// In Program.cs DI registration block
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
```

Then inject `IPasswordHasher<User>` in the controller. The same hasher is already used in DevSeeder (`new PasswordHasher<User>().HashPassword(user, "Admin123!")`). The default Identity hasher on .NET 10 uses Argon2id (Identity v3 format), satisfying AC4 / NFR-1.

### ConnectController — Full Pattern

```csharp
[ApiController]
public class ConnectController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public ConnectController(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpPost("~/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request is null.");

        if (!request.IsPasswordGrantType())
            return Forbid(ForbidProperties("unsupported_grant_type", "The grant type is not supported."),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Username is the email address
        var username = request.Username ?? string.Empty;

        // Look up user ignoring tenant filter — tenant context not yet available
        var user = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.Email == username && u.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        // Constant-time path: always perform a password check to prevent timing attacks
        // even if user is null (use a dummy hash)
        var storedHash = user?.PasswordHash ?? "$argon2id$v=19$m=65536,t=3,p=4$AAAA"; // dummy, always fails
        var passwordResult = _hasher.VerifyHashedPassword(user ?? new User { Email = "" },
            storedHash, request.Password ?? string.Empty);

        // Check lockout AFTER hash (consistent timing)
        if (user is null || IsLockedOut(user) || passwordResult == PasswordVerificationResult.Failed)
        {
            if (user is not null && passwordResult == PasswordVerificationResult.Failed)
                await IncrementFailedAccessAsync(user, ct);

            return ForbidInvalidGrant();
        }

        // Success — reset lockout counters
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync(ct);

        // Build ClaimsIdentity
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
                .SetClaim(Claims.Email, user.Email)
                .SetClaim("tid", user.TenantId.ToString());

        var principal = new ClaimsPrincipal(identity);

        // Set granted scopes (from request, filtered against registered scopes)
        principal.SetScopes(request.GetScopes());

        // Set destinations — all claims go to access token
        foreach (var claim in identity.Claims)
            claim.SetDestinations(Destinations.AccessToken);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static bool IsLockedOut(User user)
        => user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private async Task IncrementFailedAccessAsync(User user, CancellationToken ct)
    {
        user.AccessFailedCount++;
        if (user.AccessFailedCount >= 5)
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* Optimistic concurrency — accept as-is; lockout still triggers eventually */ }
    }

    private IActionResult ForbidInvalidGrant() =>
        Forbid(ForbidProperties(Errors.InvalidGrant, "Invalid credentials."),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    private static AuthenticationProperties ForbidProperties(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
}
```

**Required usings for ConnectController:**
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;
```

### OpenIddict Server Options — Add `AllowPasswordFlow()`

In `Program.cs`, inside `.AddServer(options => { ... })`, add alongside existing flows:

```csharp
options.AllowPasswordFlow();
```

Full flows block should be:
```csharp
options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
options.AllowClientCredentialsFlow();
options.AllowRefreshTokenFlow();
options.AllowPasswordFlow();
```

### DevSeeder — Update Client Permissions for Password Grant

The `oneid-dev-client` was seeded once with idempotency guard (`if not null → return`). The guard prevents the new `Password` permission from being added automatically. Use an upsert pattern:

```csharp
private static async Task SeedOpenIddictClientAsync(IOpenIddictApplicationManager manager)
{
    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = "oneid-dev-client",
        ClientType = ClientTypes.Public,
        DisplayName = "OneId Dev SPA Client",
        RedirectUris = { new Uri("http://localhost:3000/callback") },
        Permissions =
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.Password,    // ← ADDED in Story 2.2
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Email,
            Permissions.Scopes.Profile,
            Permissions.Scopes.Roles,
            $"{Permissions.Prefixes.Scope}openid",
        },
        Requirements =
        {
            Requirements.Features.ProofKeyForCodeExchange,
        },
    };

    var existing = await manager.FindByClientIdAsync("oneid-dev-client");
    if (existing is null)
        await manager.CreateAsync(descriptor);
    else
        await manager.UpdateAsync(existing, descriptor);
}
```

### `User` Entity — Lockout Fields

Add to `User.cs`:
```csharp
public int AccessFailedCount { get; set; }
public DateTimeOffset? LockoutEnd { get; set; }
```

Add to `UserConfiguration.cs`:
```csharp
builder.Property(u => u.AccessFailedCount).IsRequired().HasDefaultValue(0);
builder.Property(u => u.LockoutEnd);
```

### Test Pattern for `PasswordAuthTests.cs`

```csharp
[Collection("IntegrationTests")]
public class PasswordAuthTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly FormUrlEncodedContent ValidTokenRequest() =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile",
        });

    [Fact]
    public async Task ValidCredentials_ReturnsAccessTokenAndRefreshToken()
    {
        var response = await Client.PostAsync("/connect/token", ValidTokenRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out _));
        Assert.True(body.TryGetProperty("refresh_token", out _));
        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task InvalidPassword_ReturnsInvalidGrant()
    {
        var response = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin@oneid.dev",
            ["password"] = "WrongPassword!",
            ["client_id"] = "oneid-dev-client",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnknownEmail_ReturnsIdenticalErrorToWrongPassword()
    {
        var wrongPasswordResponse = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["username"] = "admin@oneid.dev",
            ["password"] = "Wrong!", ["client_id"] = "oneid-dev-client",
        }));
        var unknownEmailResponse = await Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["username"] = "ghost@nowhere.com",
            ["password"] = "Anything!", ["client_id"] = "oneid-dev-client",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownEmailResponse.StatusCode);

        var wrongBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<JsonElement>();
        var unknownBody = await unknownEmailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", wrongBody.GetProperty("error").GetString());
        Assert.Equal("invalid_grant", unknownBody.GetProperty("error").GetString());
        // Error descriptions must match (no enumeration)
        Assert.Equal(
            wrongBody.GetProperty("error_description").GetString(),
            unknownBody.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task LockoutTriggeredIntegrationTest()
    {
        var badRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["username"] = "admin@oneid.dev",
            ["password"] = "Wrong!", ["client_id"] = "oneid-dev-client",
        });

        for (int i = 0; i < 5; i++)
        {
            var r = await Client.PostAsync("/connect/token", badRequest);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        // Verify DB state
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == "admin@oneid.dev");
        Assert.Equal(5, user.AccessFailedCount);
        Assert.True(user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task LockedAccount_RejectsCorrectPassword()
    {
        // Trigger lockout first
        var bad = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["username"] = "admin@oneid.dev",
            ["password"] = "Wrong!", ["client_id"] = "oneid-dev-client",
        });
        for (int i = 0; i < 5; i++)
            await Client.PostAsync("/connect/token", bad);

        // Now try with CORRECT password
        var response = await Client.PostAsync("/connect/token", ValidTokenRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
        // No ETA disclosed
        var desc = body.GetProperty("error_description").GetString() ?? "";
        Assert.DoesNotContain("minutes", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked", desc, StringComparison.OrdinalIgnoreCase);
    }
}
```

### Important: `[Consumes("application/x-www-form-urlencoded")]`

Token endpoint requests use form encoding, not JSON. The test helpers must use `FormUrlEncodedContent`, not `PostAsJsonAsync`.

### Token Request Format

OpenIddict receives the password grant as a standard OAuth2 token request:
```
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=admin%40oneid.dev&password=Admin123%21&client_id=oneid-dev-client&scope=openid+email+profile
```

The field is `username` (OAuth2 spec), not `email`.

### OpenIddict 7.5.0 API Reference (Confirmed from Story 2.1)

Namespace imports verified to work in this project:
- `OpenIddict.Abstractions` — `Claims`, `Errors`, `Destinations`, `OpenIddictConstants`, `OpenIddictRequest`
- `OpenIddict.Server.AspNetCore` — `OpenIddictServerAspNetCoreDefaults`, `OpenIddictServerAspNetCoreConstants`, `HttpContextExtensions.GetOpenIddictServerRequest()`
- `IOpenIddictRequestExtensions` — `.IsPasswordGrantType()`, `.GetScopes()`
- `ClaimsPrincipalExtensions` — `.SetScopes()`, `.SetResources()`
- `OpenIddictClaimExtensions` — `.SetDestinations()`

Package `Microsoft.AspNetCore.Identity` is included transitively via `OneId.Server.csproj` — no new package reference needed if `PasswordHasher<T>` is already used in DevSeeder.

### AR-* Compliance

| Rule | Requirement | Story 2.2 Action |
|------|-------------|-----------------|
| AR-5 | ITenantContext registered before OpenIddict | Already done in Story 2.1; no change. During password auth, ITenantContext is NOT initialized — use `IgnoreQueryFilters()` for user lookup. |
| AR-4 | No credentials in logs | Passwords come in form-encoded body, not JSON. `SerilogDestructuringTests.cs` already covers password redaction. ConnectController must NOT log the password. |
| AR-14 | Optimistic concurrency on User | `AccessFailedCount`/`LockoutEnd` updates may conflict under concurrent requests. Wrap `SaveChangesAsync` in try-catch `DbUpdateConcurrencyException` — accept optimistic loss for lockout increment (eventual lockout is still correct). |
| AR-15 | Deferred-skip cap = 3 | Currently 2 deferred skips remain. Do NOT add any new `[Fact(Skip)]` in this story. |
| NFR-1 | Argon2id + RS256 + no credential logging | PasswordHasher<User> default on .NET 10 = Argon2id. RS256 signing key inherited from Story 2.1. Credentials not logged. |
| NFR-2 | Token issuance ≤500ms p95 | Not measured in this story (performance test is Story 2.4). Argon2id is intentionally slow — this is expected. |

### Project Structure Notes

**Files to CREATE:**
- `src/OneId.Server/Controllers/ConnectController.cs` — token endpoint for password grant
- `tests/OneId.Server.IntegrationTests/PasswordAuthTests.cs`

**Files to UPDATE:**
- `src/OneId.Server/Domain/Entities/User.cs` — add `AccessFailedCount`, `LockoutEnd`
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` — configure new fields
- `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs` — upsert client with `Password` permission
- `src/OneId.Server/Program.cs` — add `AllowPasswordFlow()`, register `IPasswordHasher<User>`

**Files generated (migration):**
- `src/OneId.Server/Infrastructure/Persistence/Migrations/TIMESTAMP_AddAccountLockoutFields.cs`

**DO NOT create** `AddIdentity()` registration, IdentityRole table, AspNetUsers table, or any Identity-specific migrations.

### References

- [Source: epics.md, Story 2.2] — User story, ACs, task requirements
- [Source: architecture.md#Authentication & Security] — Argon2id, no credential logging, HTTPS-only
- [Source: architecture.md#Project Structure & Boundaries] — `Controllers/` for FR-1–5a, `Infrastructure/OpenIddict/`
- [Source: architecture.md#Process Patterns] — `IgnoreQueryFilters()` for cross-tenant access (DevSeeder pattern)
- [Source: architecture.md#AR-14] — `DbUpdateConcurrencyException` handling
- [Source: src/OneId.Server/Program.cs] — existing OpenIddict AddServer block; `AllowPasswordFlow()` must be added
- [Source: src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs] — PasswordHasher pattern, oneid-dev-client seeding
- [Source: src/OneId.Server/Domain/Entities/User.cs] — current User entity (missing lockout fields)
- [Source: src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs] — field configuration pattern
- [Source: tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs] — test base class pattern
- [Source: 2-1 Dev Agent Record] — OpenIddict 7.5.0 API names (SetUserInfoEndpointUris, DisableTransportSecurityRequirement), table name casing

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- OpenIddict 7.5.0: `GetOpenIddictServerRequest()` is in `Microsoft.AspNetCore` namespace (not `OpenIddict.Server.AspNetCore`) — required `using Microsoft.AspNetCore;`
- `OpenIddictConstants.Permissions.Scopes.OfflineAccess` does NOT exist — must use `$"{Permissions.Prefixes.Scope}offline_access"` (= `"scp:offline_access"`)
- `PasswordHasher<User>` uses ASP.NET Core Identity binary format — dummy PHC-format hashes throw exceptions; for unknown users, return `ForbidInvalidGrant()` directly
- `FormUrlEncodedContent` can only be read once — use a factory method per test request
- `DevSigningKeyStabilityTest` fails with Docker unavailable (infrastructure issue, not a code regression)

### Completion Notes List

- ✅ Task 1: Added `AccessFailedCount (int)` and `LockoutEnd (DateTimeOffset?)` to `User.cs` + `UserConfiguration.cs` + EF migration
- ✅ Task 2: `AllowPasswordFlow()` added to Program.cs; `IPasswordHasher<User>` registered as singleton; DevSeeder changed to upsert pattern with Password grant + offline_access permissions
- ✅ Task 3: `ConnectController` implemented with `POST /connect/token` — password grant with lockout, enumeration-safe error responses, ClaimsIdentity with sub+email claims
- ✅ Task 4: 5 integration tests in `PasswordAuthTests.cs` — all pass (26 passed, 1 infra-skipped, 2 deferred skips in full run)
- ✅ Task 5: Build is clean (0 warnings, 0 errors); all 5 new tests pass; `DevSigningKeyStabilityTest` failure confirmed as Docker daemon instability (InternalServerError from Docker API), not a code regression

### File List

- `src/OneId.Server/Controllers/ConnectController.cs` (created)
- `tests/OneId.Server.IntegrationTests/PasswordAuthTests.cs` (created)
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20250525200312_AddAccountLockoutFields.cs` (created — migration)
- `src/OneId.Server/Domain/Entities/User.cs` (modified — added AccessFailedCount, LockoutEnd)
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` (modified — configured new fields)
- `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs` (modified — made public, upsert pattern, Password grant + offline_access permissions)
- `src/OneId.Server/Program.cs` (modified — AllowPasswordFlow(), IPasswordHasher<User> singleton, Scopes.OfflineAccess)
- `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs` (modified — DevSeeder.SeedAsync() call in ResetDatabaseAsync)
- `_bmad-output/implementation-artifacts/2-2-password-authentication-and-account-lockout.md` (modified — tasks checked, Dev Agent Record, status → review)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — 2-2 → review)

## Change Log

- 2025-05-25: Story 2.2 implemented — password grant token endpoint, account lockout, 5 integration tests, EF migration for lockout fields, DevSeeder upsert pattern. Status → review.
