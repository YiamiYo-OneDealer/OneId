# Story 2.3: TOTP MFA Enrollment and Challenge

Status: review

## Story

As an end user,
I want to enroll in TOTP on my first login and be challenged on every subsequent login,
so that my account requires two factors before any token is issued.

## Acceptance Criteria

**AC1: First-time user without TOTP**

**Given** a user has correct email/password but has not yet enrolled in TOTP
**When** the password factor is verified
**Then** the response is HTTP 200 with a `mfa_required: true` flag and a `totp_enrollment_uri` (otpauth:// URI compatible with Google Authenticator / Authy)
**And** no `access_token` is issued — the authentication flow is suspended at the MFA gate

**AC2: User submits valid TOTP code during enrollment**

**Given** a user submits a valid TOTP code during enrollment
**When** the enrollment is confirmed
**Then** the TOTP secret is stored (encrypted at rest) and `totp_enrolled: true` is set on the user record
**And** an `access_token` is issued for this initial login after successful enrollment

**AC3: User with TOTP enrolled provides credentials**

**Given** a user has TOTP enrolled and provides correct email/password
**When** the password factor is verified
**Then** the response indicates a TOTP challenge is required (no token issued yet)
**And** submitting a valid current TOTP code completes authentication and issues a token
**And** submitting an incorrect TOTP code returns HTTP 400 with `error: "invalid_grant"` — no TOTP enrollment URI, no brute-force timing hints

**AC4: Replay prevention for TOTP codes**

**Given** a TOTP code has already been used
**When** the same code is submitted again within its 30-second validity window
**Then** the authentication fails (replay prevention — one-time-use enforcement)
**And** `TotpMfaIntegrationTests.cs` covers: enrollment, valid challenge, invalid challenge, and replay prevention

## Tasks / Subtasks

- [x] Task 1: Add TOTP fields to `User` entity and configuration (AC: 2, 3, 4)
  - [ ] Add to `User.cs`: `public string? TotpSecret { get; set; }`, `public bool IsTotpEnrolled { get; set; }`, `public long? TotpLastUsedTimeStep { get; set; }`
  - [ ] Add to `UserConfiguration.cs`: `TotpSecret` (max 500), `IsTotpEnrolled` (required, default false), `TotpLastUsedTimeStep` (nullable long)
  - [ ] Run `dotnet ef migrations add AddTotpFields --project src/OneId.Server/OneId.Server.csproj`
  - [ ] Run `dotnet build OneId.slnx` — zero warnings, zero errors

- [x] Task 2: Install `Otp.NET` NuGet package (AC: 2, 3, 4)
  - [ ] Add `<PackageReference Include="Otp.NET" Version="1.4.0" />` to `src/OneId.Server/OneId.Server.csproj`
  - [ ] Run `dotnet build OneId.slnx` — confirms package resolves correctly

- [x] Task 3: Register custom MFA grant type in OpenIddict + DevSeeder (AC: 2, 3)
  - [ ] In `Program.cs` AddServer block, add: `options.AllowCustomFlow("urn:oneid:mfa");`
  - [ ] In `DevSeeder.SeedOpenIddictClientAsync`, add `"gt:urn:oneid:mfa"` to the client's `Permissions` set (upsert already in place)
  - [ ] Update `DevSeeder.SeedAsync` signature to accept `IDataProtectionProvider dp` as third parameter
  - [ ] Add `SeedTotpUserAsync(AppDbContext db, IDataProtectionProvider dp)` private method to DevSeeder (see Dev Notes for content)
  - [ ] Call `SeedTotpUserAsync` from `SeedAsync`
  - [ ] In `Program.cs`, pass `app.Services.GetRequiredService<IDataProtectionProvider>()` to `DevSeeder.SeedAsync()`

- [x] Task 4: Extend `ConnectController` — password grant MFA gate (AC: 1, 3)
  - [ ] Add `IDataProtectionProvider` as constructor parameter (alongside existing `AppDbContext db` and `IPasswordHasher<User> hasher`)
  - [ ] After password success in `HandlePasswordStep` (or inline in `Token()`), check `user.IsTotpEnrolled`:
    - [ ] If NOT enrolled: generate 20-byte random TOTP secret → base32-encode → build `otpauth://` URI → encrypt secret with DPAPI → store in `user.TotpSecret` → save → create 5-min DPAPI `mfa_session_token` → return `Ok(new { mfa_required = true, totp_enrollment_uri = ..., mfa_session_token = ... })`
    - [ ] If enrolled: create 5-min DPAPI `mfa_session_token` → return `Ok(new { mfa_required = true, mfa_session_token = ... })`
  - [ ] The `mfa_session_token` payload: DPAPI time-limited protect of `"{userId}|{type}"` where type is `"enroll"` or `"challenge"`
  - [ ] Do NOT call `SignIn()` or `ForbidInvalidGrant()` in these paths — return `Ok()` directly

- [x] Task 5: Implement TOTP MFA grant handler in `ConnectController` (AC: 2, 3, 4)
  - [ ] In `ConnectController.Token()`, add branch for `request.GrantType == "urn:oneid:mfa"` (after the `IsPasswordGrantType()` check)
  - [ ] Extract `mfa_session_token` via `request.GetParameter("mfa_session_token")?.Value?.ToString()`
  - [ ] Extract `totp_code` via `request.GetParameter("totp_code")?.Value?.ToString()`
  - [ ] Validate `mfa_session_token` using `ITimeLimitedDataProtector.Unprotect()` — throws `CryptographicException` if expired or invalid → return `ForbidInvalidGrant()`
  - [ ] Parse `userId` and `type` from payload; load user from DB with `IgnoreQueryFilters()`
  - [ ] Decrypt `user.TotpSecret` with DPAPI → get base32 secret
  - [ ] Verify TOTP code with `new Totp(Base32Encoding.ToBytes(secret)).VerifyTotp(totpCode, out long timeStepMatched, new VerificationWindow(1, 0))`
  - [ ] If verification fails → `ForbidInvalidGrant()` (no hint whether code or token was wrong)
  - [ ] Replay check: if `user.TotpLastUsedTimeStep == timeStepMatched` → `ForbidInvalidGrant()`
  - [ ] If type == `"enroll"`: set `user.IsTotpEnrolled = true`
  - [ ] Set `user.TotpLastUsedTimeStep = timeStepMatched`; save
  - [ ] Build `ClaimsIdentity` identically to Story 2.2 (`sub`, `email`, `tid` claims); call `SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)`

- [x] Task 6: Update `WebApplicationFactory.ResetDatabaseAsync()` (AC: all tests)
  - [ ] Inject `IDataProtectionProvider` from `scope.ServiceProvider` in `ResetDatabaseAsync()`
  - [ ] Pass it as third argument to `DevSeeder.SeedAsync(db, manager, dp)`

- [x] Task 7: Write `TotpMfaIntegrationTests.cs` (AC: 1, 2, 3, 4)
  - [ ] Create `tests/OneId.Server.IntegrationTests/TotpMfaIntegrationTests.cs`
  - [ ] `PasswordGrant_UnenrolledUser_ReturnsMfaRequired_WithEnrollmentUri` — AC1
  - [ ] `TotpEnrollment_ValidCode_IssuesTokenAndSetsEnrolledFlag` — AC2
  - [ ] `PasswordGrant_EnrolledUser_ReturnsMfaRequired_WithoutEnrollmentUri` — AC3 (challenge path, no URI)
  - [ ] `TotpChallenge_ValidCode_IssuesToken` — AC3 (challenge completes with token)
  - [ ] `TotpChallenge_InvalidCode_ReturnsInvalidGrant` — AC3 (wrong code)
  - [ ] `TotpChallenge_ReplayedCode_ReturnsInvalidGrant` — AC4 (same code used twice)
  - [ ] Use `DevSeeder.TotpUserId` / `DevSeeder.TotpUserEmail` / `DevSeeder.TotpUserTotpSecret` constants for the pre-enrolled user

- [x] Task 8: Final verification (AC: all)
  - [ ] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [ ] `dotnet test OneId.slnx` — all tests pass (0 new failures beyond the pre-existing infrastructure-dependent `DevSigningKeyStabilityTest`)

## Dev Notes

### CRITICAL: Two-Step Auth Flow Design

The MFA flow is a **two-step process** over `/connect/token`:

**Step 1 — Password Factor** (`grant_type=password`, no `mfa_session_token`):
```
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=admin%40oneid.dev&password=Admin123%21&client_id=oneid-dev-client
```
Response (unenrolled):
```json
{ "mfa_required": true, "totp_enrollment_uri": "otpauth://totp/OneId:admin%40oneid.dev?secret=BASE32&issuer=OneId", "mfa_session_token": "<dpapi-token>" }
```
Response (enrolled):
```json
{ "mfa_required": true, "mfa_session_token": "<dpapi-token>" }
```

**Step 2 — TOTP Factor** (`grant_type=urn:oneid:mfa`):
```
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=urn%3Aoneid%3Amfa&client_id=oneid-dev-client&mfa_session_token=<dpapi-token>&totp_code=123456
```
Response: standard OpenIddict token response with `access_token`, `token_type`, `expires_in`, `refresh_token`.

### CRITICAL: `AllowCustomFlow` Verification Required

OpenIddict 7.5.0 may use `AllowCustomFlow` or an equivalent. Verify the exact method name from the OpenIddict 7.5.0 source or NuGet package before implementing:

```csharp
// In Program.cs AddServer block (verify this is the correct 7.5.0 API):
options.AllowCustomFlow("urn:oneid:mfa");
```

If `AllowCustomFlow` does not exist in 7.5.0, check for:
- `options.RegisterCustomFlow("urn:oneid:mfa")` 
- or simply adding the grant type string to the options without an explicit method (check the OpenIddict changelog)

The client permission string for a custom grant type in OpenIddict follows the format `"gt:{grantType}"`:
```csharp
// In DevSeeder client descriptor Permissions set:
"gt:urn:oneid:mfa"
```

To access custom parameters from the request in ConnectController:
```csharp
var mfaSessionToken = request.GetParameter("mfa_session_token")?.Value?.ToString();
var totpCode = request.GetParameter("totp_code")?.Value?.ToString();
```

### CRITICAL: `Ok()` Return from Passthrough Token Endpoint

In OpenIddict passthrough mode, the controller CAN return `Ok(...)` from `/connect/token` without going through OpenIddict's response formatting. This is different from `SignIn()` which triggers OpenIddict's token response.

The MFA challenge responses (Step 1) use `return Ok(new { ... })` directly. OpenIddict does NOT intercept these — they pass through as standard JSON 200 responses. This is intentional for POC.

### TOTP Secret: DPAPI Encryption at Rest

Use `IDataProtectionProvider` from ASP.NET Core Data Protection (no extra package needed):

```csharp
// In ConnectController constructor:
private readonly IDataProtector _secretProtector;
private readonly ITimeLimitedDataProtector _sessionProtector;

public ConnectController(AppDbContext db, IPasswordHasher<User> hasher, IDataProtectionProvider dp)
{
    _secretProtector = dp.CreateProtector("totp.secret.v1");
    _sessionProtector = dp.CreateProtector("mfa.session.v1").ToTimeLimitedDataProtector();
}

// Encrypt TOTP secret for storage:
var encrypted = _secretProtector.Protect(base32Secret);
user.TotpSecret = encrypted;

// Decrypt when needed:
var base32Secret = _secretProtector.Unprotect(user.TotpSecret!);
```

`IDataProtectionProvider` is already registered by ASP.NET Core via `WebApplication.CreateBuilder()` — no `AddDataProtection()` call needed.

### TOTP Secret Generation and otpauth:// URI

```csharp
using OtpNet;

// Generate a fresh 20-byte (160-bit) secret for a new enrollment:
var secretBytes = KeyGeneration.GenerateRandomKey(20);
var base32Secret = Base32Encoding.ToString(secretBytes);

// Build the otpauth:// URI (Google Authenticator / Authy compatible):
var label = Uri.EscapeDataString($"OneId:{user.Email}");
var enrollmentUri = $"otpauth://totp/{label}?secret={base32Secret}&issuer=OneId";
```

### TOTP Code Verification and Replay Prevention

```csharp
using OtpNet;

// Verify code (1 previous window for clock skew tolerance; no future windows):
var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
bool verified = totp.VerifyTotp(totpCode, out long timeStepMatched, new VerificationWindow(previous: 1, future: 0));

// Replay prevention — timeStepMatched is the Unix timestamp / 30 (step number):
if (!verified)
    return ForbidInvalidGrant();

if (user.TotpLastUsedTimeStep.HasValue && user.TotpLastUsedTimeStep.Value == timeStepMatched)
    return ForbidInvalidGrant(); // Same 30-second window used twice

// Record this step as used:
user.TotpLastUsedTimeStep = timeStepMatched;
```

`TotpLastUsedTimeStep` stores a `long?` (Unix time step = Unix epoch seconds / 30). This is much better than storing the code itself because:
- Handles the `VerificationWindow(previous: 1)` correctly (each window has a unique step number)
- Tamper-proof (step number is not controllable by the user)

### MFA Session Token (DPAPI Time-Limited)

```csharp
// Create a 5-minute session token after password verification:
var payload = $"{user.Id}|{(user.IsTotpEnrolled ? "challenge" : "enroll")}";
var mfaSessionToken = _sessionProtector.Protect(payload, TimeSpan.FromMinutes(5));

// Validate in the MFA grant handler:
string payload;
try
{
    payload = _sessionProtector.Unprotect(mfaSessionToken);
    // ITimeLimitedDataProtector throws CryptographicException if expired
}
catch (CryptographicException)
{
    return ForbidInvalidGrant();
}

var parts = payload.Split('|');
if (parts.Length != 2 || !Guid.TryParse(parts[0], out var userId))
    return ForbidInvalidGrant();

var type = parts[1]; // "enroll" or "challenge"
```

`ToTimeLimitedDataProtector()` is in `Microsoft.AspNetCore.DataProtection` via `Microsoft.AspNetCore.DataProtection.Extensions` — available via the `using Microsoft.AspNetCore.DataProtection;` namespace.

### DevSeeder: Pre-Enrolled Test User

Add the following to `DevSeeder.cs`. Use these well-known constants so tests can generate valid TOTP codes:

```csharp
public static readonly Guid TotpUserId  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
public static readonly string TotpUserEmail = "totp@oneid.dev";
public const string TotpUserTotpSecret = "JBSWY3DPEHPK3PXP"; // standard OtpNet test vector

private static async Task SeedTotpUserAsync(AppDbContext db, IDataProtectionProvider dp)
{
    var exists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == TotpUserId);
    if (exists) return;

    var user = new User
    {
        Id = TotpUserId,
        TenantId = DevTenantId,
        Email = TotpUserEmail,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        IsTotpEnrolled = true,
        TotpSecret = dp.CreateProtector("totp.secret.v1").Protect(TotpUserTotpSecret),
    };
    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Admin123!");
    db.Users.Add(user);
    await db.SaveChangesAsync();
}
```

The `TotpUserTotpSecret` constant `"JBSWY3DPEHPK3PXP"` is the standard OtpNet test vector for `"Hello!"` encoded in base32. Tests can generate valid codes for this secret:

```csharp
// In test: generate current valid TOTP code for pre-enrolled user
var currentCode = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
    .ComputeTotp(DateTime.UtcNow);
```

### Updated DevSeeder.SeedAsync Signature

```csharp
public static async Task SeedAsync(AppDbContext db, IOpenIddictApplicationManager manager, IDataProtectionProvider dp)
{
    await SeedDevTenantAsync(db);
    await SeedAdminUserAsync(db);     // no TOTP — for enrollment flow tests
    await SeedTotpUserAsync(db, dp);  // pre-enrolled — for challenge flow tests
    await SeedOpenIddictClientAsync(manager);
}
```

### Updated Program.cs DevSeeder Call

```csharp
// In Program.cs, development seeding block:
var dp = app.Services.GetRequiredService<IDataProtectionProvider>();
await DevSeeder.SeedAsync(db, manager, dp);
```

### Updated WebApplicationFactory.ResetDatabaseAsync

```csharp
public async Task ResetDatabaseAsync()
{
    await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
    await conn.OpenAsync();
    await _respawner.ResetAsync(conn);

    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    var dp = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
    await DevSeeder.SeedAsync(db, manager, dp);
}
```

### ConnectController: Full Extended Structure

```csharp
[ApiController]
public class ConnectController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IDataProtectionProvider dataProtection) : ControllerBase
{
    private readonly IDataProtector _secretProtector =
        dataProtection.CreateProtector("totp.secret.v1");

    private readonly ITimeLimitedDataProtector _sessionProtector =
        dataProtection.CreateProtector("mfa.session.v1").ToTimeLimitedDataProtector();

    [HttpPost("~/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request is null.");

        if (request.IsPasswordGrantType())
            return await HandlePasswordGrantAsync(request, ct);

        if (request.GrantType == "urn:oneid:mfa")
            return await HandleMfaGrantAsync(request, ct);

        return Forbid(BuildForbidProperties("unsupported_grant_type", "The grant type is not supported."),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // ... HandlePasswordGrantAsync: existing password logic + TOTP gate
    // ... HandleMfaGrantAsync: TOTP verification + SignIn
    // ... private helpers: ForbidInvalidGrant, BuildForbidProperties, BuildPrincipal
}
```

### ClaimsIdentity Construction (Consistent with Story 2.2)

The `SignIn()` call in the MFA grant handler must produce the same claims as the password grant would have:

```csharp
var identity = new ClaimsIdentity(
    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
    nameType: Claims.Name,
    roleType: Claims.Role);

identity.SetClaim(Claims.Subject, user.Id.ToString())
        .SetClaim(Claims.Email, user.Email)
        .SetClaim("tid", user.TenantId.ToString());

var principal = new ClaimsPrincipal(identity);
principal.SetScopes(request.GetScopes());

foreach (var claim in identity.Claims)
    claim.SetDestinations(Destinations.AccessToken);

return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
```

Note: `request.GetScopes()` — in the MFA grant step 2, the client should include `scope` in the request. Test with `scope=openid email profile offline_access`.

### Test Pattern for TotpMfaIntegrationTests.cs

```csharp
using OtpNet;
using OneId.Server.Infrastructure.Persistence.Seeds;

[Collection("IntegrationTests")]
public class TotpMfaIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // Step 1: password grant (creates a fresh FormUrlEncodedContent per call!)
    private static FormUrlEncodedContent PasswordStep(string email = "admin@oneid.dev") =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = "Admin123!",
            ["client_id"] = "oneid-dev-client",
            ["scope"] = "openid email profile offline_access",
        });

    // Step 2: TOTP grant
    private static FormUrlEncodedContent TotpStep(string mfaSessionToken, string totpCode) =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["client_id"] = "oneid-dev-client",
            ["mfa_session_token"] = mfaSessionToken,
            ["totp_code"] = totpCode,
            ["scope"] = "openid email profile offline_access",
        });

    // Generate a valid TOTP code for the pre-enrolled test user
    private static string ValidTotpCode() =>
        new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
            .ComputeTotp(DateTime.UtcNow);

    [Fact]
    public async Task PasswordGrant_UnenrolledUser_ReturnsMfaRequired_WithEnrollmentUri()
    {
        var response = await Client.PostAsync("/connect/token", PasswordStep());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mfa_required").GetBoolean());
        var uri = body.GetProperty("totp_enrollment_uri").GetString();
        Assert.NotNull(uri);
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.True(body.TryGetProperty("mfa_session_token", out _));
        Assert.False(body.TryGetProperty("access_token", out _), "Should not issue token before TOTP");
    }

    [Fact]
    public async Task TotpEnrollment_ValidCode_IssuesTokenAndSetsEnrolledFlag()
    {
        // Step 1: get enrollment URI + session token
        var step1 = await Client.PostAsync("/connect/token", PasswordStep());
        var step1Body = await step1.Content.ReadFromJsonAsync<JsonElement>();
        var enrollmentUri = step1Body.GetProperty("totp_enrollment_uri").GetString()!;
        var mfaToken = step1Body.GetProperty("mfa_session_token").GetString()!;

        // Extract base32 secret from otpauth URI
        var secretParam = new Uri(enrollmentUri).Query
            .Split('&')
            .First(p => p.StartsWith("secret="))
            .Substring("secret=".Length);

        // Generate valid code for this secret
        var code = new Totp(Base32Encoding.ToBytes(secretParam)).ComputeTotp(DateTime.UtcNow);

        // Step 2: complete enrollment
        var step2 = await Client.PostAsync("/connect/token", TotpStep(mfaToken, code));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var tokenBody = await step2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(tokenBody.TryGetProperty("access_token", out _));
        Assert.Equal("Bearer", tokenBody.GetProperty("token_type").GetString());

        // Verify DB state
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "admin@oneid.dev");
        Assert.True(user.IsTotpEnrolled);
        Assert.NotNull(user.TotpSecret);
    }

    [Fact]
    public async Task PasswordGrant_EnrolledUser_ReturnsMfaRequired_WithoutEnrollmentUri()
    {
        var response = await Client.PostAsync("/connect/token",
            PasswordStep(DevSeeder.TotpUserEmail));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mfa_required").GetBoolean());
        Assert.False(body.TryGetProperty("totp_enrollment_uri", out _), "Enrolled user should not get enrollment URI");
        Assert.True(body.TryGetProperty("mfa_session_token", out _));
        Assert.False(body.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task TotpChallenge_ValidCode_IssuesToken()
    {
        var step1 = await Client.PostAsync("/connect/token", PasswordStep(DevSeeder.TotpUserEmail));
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await Client.PostAsync("/connect/token", TotpStep(mfaToken, ValidTotpCode()));
        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var body = await step2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task TotpChallenge_InvalidCode_ReturnsInvalidGrant()
    {
        var step1 = await Client.PostAsync("/connect/token", PasswordStep(DevSeeder.TotpUserEmail));
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await Client.PostAsync("/connect/token", TotpStep(mfaToken, "000000"));
        Assert.Equal(HttpStatusCode.BadRequest, step2.StatusCode);

        var body = await step2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("totp_enrollment_uri", out _)); // No info leakage
    }

    [Fact]
    public async Task TotpChallenge_ReplayedCode_ReturnsInvalidGrant()
    {
        var step1 = await Client.PostAsync("/connect/token", PasswordStep(DevSeeder.TotpUserEmail));
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var code = ValidTotpCode();

        // First use — should succeed
        var first = await Client.PostAsync("/connect/token", TotpStep(mfaToken, code));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Get a new session token for the replay attempt (can't reuse the expired one)
        var step1b = await Client.PostAsync("/connect/token", PasswordStep(DevSeeder.TotpUserEmail));
        var mfaToken2 = (await step1b.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        // Second use with same code — should fail (replay prevention)
        var second = await Client.PostAsync("/connect/token", TotpStep(mfaToken2, code));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("invalid_grant",
            (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }
}
```

**Important for replay test**: The replay test needs a fresh `mfa_session_token` for the second attempt (the first token is consumed). However, since the TOTP `timeStepMatched` for the replayed code is still stored in the DB, the second attempt with the same code within the same 30-second window is rejected. If the test runs across a 30-second window boundary, the code changes and the replay test may not catch it — this is acceptable for POC (TOTP windows are non-deterministic in tests).

### AR-* and NFR Compliance

| Rule | Impact on Story 2.3 |
|------|---------------------|
| AR-14 (Concurrency) | `TotpLastUsedTimeStep` write uses optimistic concurrency (xmin). Wrap `SaveChangesAsync` in try-catch `DbUpdateConcurrencyException` in the TOTP grant handler — accept optimistic loss (replay prevention is eventually consistent, acceptable for POC) |
| AR-15 (Deferred-Skip cap = 3) | Currently 2 open skips. Do NOT add `[Fact(Skip)]`. All 6+ new tests must run without skip |
| AR-4 (No credential logging) | Do NOT log `mfa_session_token`, `totp_code`, or `TotpSecret` in any log event |
| NFR-1 (Security) | TOTP secret encrypted at rest via DPAPI; credentials never logged |
| NFR-2 (≤500ms p95) | TOTP verification via OtpNet is microseconds; no budget concern |

### Project Structure Notes

**Files to CREATE:**
- `tests/OneId.Server.IntegrationTests/TotpMfaIntegrationTests.cs`
- EF migration file (auto-generated): `src/OneId.Server/Infrastructure/Persistence/Migrations/TIMESTAMP_AddTotpFields.cs`

**Files to UPDATE:**
- `src/OneId.Server/Domain/Entities/User.cs` — add 3 TOTP fields
- `src/OneId.Server/Infrastructure/Persistence/Configurations/UserConfiguration.cs` — configure new fields
- `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs` — new signature, `TotpUserId/TotpUserEmail/TotpUserTotpSecret` constants, `SeedTotpUserAsync`, updated `SeedOpenIddictClientAsync` with `"gt:urn:oneid:mfa"` permission
- `src/OneId.Server/Controllers/ConnectController.cs` — add `IDataProtectionProvider`, MFA gate in password handler, new MFA grant handler
- `src/OneId.Server/Program.cs` — `options.AllowCustomFlow("urn:oneid:mfa")`, `IDataProtectionProvider` injected into DevSeeder call
- `src/OneId.Server/OneId.Server.csproj` — add `Otp.NET` package reference
- `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs` — inject `IDataProtectionProvider` in `ResetDatabaseAsync()`

**DO NOT touch:**
- `PasswordAuthTests.cs` — existing tests must keep passing unchanged
- Any migration file (edit migrations only by running `dotnet ef migrations`)

### Usings Required for ConnectController

Existing usings (from Story 2.2) remain. Add:
```csharp
using Microsoft.AspNetCore.DataProtection;
using OtpNet;
using System.Security.Cryptography; // for CryptographicException
```

### References

- [Source: epics.md, Story 2.3] — User story, AC1–AC4, task requirements
- [Source: architecture.md#Authentication & Security] — Argon2id, no credential logging, AR-14 concurrency
- [Source: 2-2 Dev Agent Record] — ConnectController pattern, OpenIddict API names, test patterns
- [Source: src/OneId.Server/Controllers/ConnectController.cs] — existing password grant implementation to extend
- [Source: src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs] — seeder pattern to update
- [Source: tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs] — factory to update
- [Source: tests/OneId.Server.IntegrationTests/PasswordAuthTests.cs] — established test patterns
- [Source: architecture.md#AR-15] — deferred-skip cap (2 open, cap 3; do NOT add new skips)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
