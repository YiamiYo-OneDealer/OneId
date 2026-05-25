# Story 2.4: JWT Issuance with ITokenClaimsEnricher Pipeline

Status: review

## Story

As a developer,
I want a formal `ITokenClaimsEnricher` pipeline wired into OpenIddict token issuance,
So that Epic 4b can add authorization claims additively without touching token issuance code, and the issuance performance budget is verified.

## Acceptance Criteria

**AC1: Pipeline interface and ordering guarantee**

**Given** `ITokenClaimsEnricher` is defined
**When** the solution compiles
**Then** the interface is in `Application/TokenPipeline/ITokenClaimsEnricher.cs` with signature: `Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)`
**And** `TokenEnrichmentContext` carries at minimum: `UserId`, `TenantId`, `GrantType`
**And** an `EnricherPipelineOrderTest` registers two `ITokenClaimsEnricher` stubs in a known sequence: `StubEnricherA` (adds claim `test-marker-a` to the identity) followed by `StubEnricherB` (asserts `test-marker-a` is already present before adding `test-marker-b`); the test issues a token and asserts both claims are present, confirming A ran before B

**AC2: JWT claim set**

**Given** a token is issued via the `urn:oneid:mfa` grant
**When** the `ITokenClaimsEnricher` pipeline runs
**Then** the resulting JWT contains: `sub` (user ID), `iss`, `aud`, `exp`, `iat`, `jti`, and `roles` (present as a JSON array — may be empty in Epic 2; populated in Epic 4a)
**And** `roles` is added by the first `ITokenClaimsEnricher` stage (`RoleClaimsEnricher`) — this is the only stage in Epic 2
**And** a `JwtClaimsIntegrationTest` decodes the issued JWT and asserts all required claims are present with correct types

**AC3: Performance gate**

**Given** `TokenIssuanceTests.cs` runs
**When** token issuance (the MFA grant step) is measured under test load (single-threaded sequential calls)
**Then** 95th percentile issuance time is under 400ms (100ms headroom against NFR-2 500ms gate)
**And** the test uses a `Stopwatch` per issuance call and fails if the p95 exceeds the ceiling
**And** the test uses a minimum of 50 samples

## Tasks / Subtasks

- [ ] Task 1: Create `ITokenClaimsEnricher`, `TokenEnrichmentContext`, `RoleClaimsEnricher` (AC: 1, 2)
  - [ ] Create `src/OneId.Server/Application/TokenPipeline/ITokenClaimsEnricher.cs`
  - [ ] Create `src/OneId.Server/Application/TokenPipeline/TokenEnrichmentContext.cs`
  - [ ] Create `src/OneId.Server/Application/TokenPipeline/RoleClaimsEnricher.cs` — Epic 2 stub; no DB query yet, adds 0 role claims (roles come from Epic 4a)

- [ ] Task 2: DI registration via `TokenPipelineExtensions.cs` + update `Program.cs` (AC: 1)
  - [ ] Create `src/OneId.Server/Infrastructure/OpenIddict/TokenPipelineExtensions.cs` with `AddTokenPipeline(this IServiceCollection services)` extension
  - [ ] Register `RoleClaimsEnricher` as `ITokenClaimsEnricher` (scoped) in that extension
  - [ ] Call `builder.Services.AddTokenPipeline()` in `Program.cs`

- [ ] Task 3: Update `ConnectController` to call the pipeline before `SignIn()` (AC: 1, 2)
  - [ ] Add `IEnumerable<ITokenClaimsEnricher> enrichers` as constructor parameter
  - [ ] In `HandleMfaGrantAsync`, after building `ClaimsIdentity` and before `SignIn()`, call the pipeline:
    ```csharp
    var ctx = new TokenEnrichmentContext(user.Id, user.TenantId, request.GrantType);
    foreach (var enricher in enrichers)
        await enricher.EnrichAsync(identity, ctx, ct);
    ```
  - [ ] The existing `foreach (var claim in identity.Claims) claim.SetDestinations(Destinations.AccessToken)` sweep MUST remain AFTER the enricher pipeline so enricher-added claims also get AccessToken destination

- [ ] Task 4: Create `tests/OneId.Server.IntegrationTests/OpenIddict/TokenIssuanceTests.cs` (AC: 1, 2, 3)
  - [ ] `EnricherPipelineOrderTest` — validates DI registration ordering is preserved
  - [ ] `JwtClaimsIntegrationTest` — decodes real JWT, asserts all required claims
  - [ ] `TokenIssuance_P95_UnderBudget` — 50-sample Stopwatch test with p95 ≤ 400ms assertion

- [ ] Task 5: Final verification (AC: all)
  - [ ] `dotnet build OneId.slnx` — zero warnings, zero errors
  - [ ] `dotnet test OneId.slnx` — all new tests pass; no regressions

## Dev Notes

### Critical Architecture: Pipeline Position

The `ITokenClaimsEnricher` pipeline sits **inside `HandleMfaGrantAsync`**, immediately before `SignIn()`. This is the ONLY place tokens are issued (Story 2.3 established that the password grant returns `mfa_required`, never a token).

Current `HandleMfaGrantAsync` pattern (end of method, lines ~179–195):
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

**After Story 2.4**, the enrichment call slots between identity construction and the destination sweep:
```csharp
var identity = new ClaimsIdentity(...);
identity.SetClaim(Claims.Subject, user.Id.ToString())
        .SetClaim(Claims.Email, user.Email)
        .SetClaim("tid", user.TenantId.ToString());

var principal = new ClaimsPrincipal(identity);
principal.SetScopes(request.GetScopes());

// NEW: run the enricher pipeline (adds roles[], permissions[], etc. in future epics)
var enrichmentContext = new TokenEnrichmentContext(user.Id, user.TenantId, request.GrantType);
foreach (var enricher in enrichers)
    await enricher.EnrichAsync(identity, enrichmentContext, ct);

// Destination sweep MUST remain here — covers both base claims and enricher-added claims
foreach (var claim in identity.Claims)
    claim.SetDestinations(Destinations.AccessToken);

return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
```

### Interface and Context Patterns

**`ITokenClaimsEnricher.cs`:**
```csharp
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public interface ITokenClaimsEnricher
{
    Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct);
}
```

**`TokenEnrichmentContext.cs`:**
```csharp
namespace OneId.Server.Application.TokenPipeline;

public sealed record TokenEnrichmentContext(Guid UserId, Guid TenantId, string? GrantType);
```

**`RoleClaimsEnricher.cs`:**
```csharp
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

// Epic 2 stub: no role data yet (roles come from Epic 4a DB entities).
// Registers as the first ITokenClaimsEnricher stage; Epic 4b adds the real query.
// The "roles" claim is ABSENT from the JWT in Epic 2 when no roles are assigned.
// JwtClaimsIntegrationTest accounts for this: it asserts roles claim type IF present.
public sealed class RoleClaimsEnricher : ITokenClaimsEnricher
{
    public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        // Epic 4a: query db.UserRoles for context.UserId and add a Claim per role name.
        // For N roles > 0, OpenIddict serializes them as a JSON array in the JWT.
        // For 0 roles (Epic 2), no "roles" claim is added — the claim is absent in the JWT.
        return Task.CompletedTask;
    }
}
```

### DI Registration Pattern

**`TokenPipelineExtensions.cs`:**
```csharp
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.TokenPipeline;

namespace OneId.Server.Infrastructure.OpenIddict;

public static class TokenPipelineExtensions
{
    // Registration order = execution order (IEnumerable<ITokenClaimsEnricher> preserves DI order).
    // Epic 4b adds new enrichers here — no other file changes needed.
    public static IServiceCollection AddTokenPipeline(this IServiceCollection services)
    {
        services.AddScoped<ITokenClaimsEnricher, RoleClaimsEnricher>();
        return services;
    }
}
```

**`Program.cs` addition** (add after `builder.Services.AddSingleton<IPasswordHasher<User>>...`):
```csharp
builder.Services.AddTokenPipeline();
```

### `ConnectController` Constructor Change

Current:
```csharp
public class ConnectController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IDataProtectionProvider dp) : ControllerBase
```

After:
```csharp
public class ConnectController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IDataProtectionProvider dp,
    IEnumerable<ITokenClaimsEnricher> enrichers) : ControllerBase
```

Add required using:
```csharp
using OneId.Server.Application.TokenPipeline;
```

### Test File: `TokenIssuanceTests.cs`

Test helpers — copy the pattern from `TotpMfaIntegrationTests.cs`:
```csharp
private static FormUrlEncodedContent TotpUserPasswordRequest() => new(...); // same as before
private static FormUrlEncodedContent MfaGrantRequest(string mfaToken, string code) => new(...);
private static string CurrentTotpCode()
    => new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret)).ComputeTotp(DateTime.UtcNow);
```

**Test 1 — `EnricherPipelineOrderTest`:**

Uses `Factory.WithWebHostBuilder()` to add two stub enrichers on top of production DI:

```csharp
[Fact]
public async Task EnricherPipelineOrder_StubBSeesStubA_Marker()
{
    // Stubs defined as inner classes in the test class (see below)
    using var customFactory = Factory.WithWebHostBuilder(b =>
        b.ConfigureServices(svc =>
        {
            svc.AddScoped<ITokenClaimsEnricher, StubEnricherA>();
            svc.AddScoped<ITokenClaimsEnricher, StubEnricherB>();
        }));
    using var client = customFactory.CreateClient();

    // Step 1: password grant
    var step1 = await client.PostAsync("/connect/token", TotpUserPasswordRequest());
    var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("mfa_session_token").GetString()!;

    // Step 2: MFA grant with stubs active
    var step2 = await client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, CurrentTotpCode()));
    Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

    var body = await step2.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = body.GetProperty("access_token").GetString()!;

    // Decode JWT payload (no validation needed — testing claim presence, not security)
    var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(accessToken.Split('.')[1]));
    var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

    Assert.True(payload.TryGetProperty("test-marker-a", out _), "StubEnricherA must have run");
    Assert.True(payload.TryGetProperty("test-marker-b", out _), "StubEnricherB must have run (and A ran first)");
}

// Stub enrichers as private inner classes:
private sealed class StubEnricherA : ITokenClaimsEnricher
{
    public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        identity.AddClaim(new Claim("test-marker-a", "added-by-a"));
        return Task.CompletedTask;
    }
}

private sealed class StubEnricherB : ITokenClaimsEnricher
{
    public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        // If A did not run first, this assertion causes a 500 from the controller,
        // and the test fails with an unexpected HTTP 400/500.
        if (!identity.HasClaim(c => c.Type == "test-marker-a"))
            throw new InvalidOperationException("StubEnricherB ran before StubEnricherA — pipeline ordering broken.");
        identity.AddClaim(new Claim("test-marker-b", "added-by-b"));
        return Task.CompletedTask;
    }
}
```

**Test 2 — `JwtClaimsIntegrationTest`:**

```csharp
[Fact]
public async Task IssuedJwt_ContainsRequiredClaims()
{
    // Full two-step flow
    var step1 = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
    var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("mfa_session_token").GetString()!;
    var step2 = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, CurrentTotpCode()));
    Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

    var body = await step2.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = body.GetProperty("access_token").GetString()!;

    // Decode without signature validation — testing claim shape, not security
    var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(accessToken.Split('.')[1]));
    var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

    // OpenIddict-issued standard claims
    Assert.True(payload.TryGetProperty("sub", out _), "sub claim required");
    Assert.True(payload.TryGetProperty("iss", out _), "iss claim required");
    Assert.True(payload.TryGetProperty("aud", out _), "aud claim required");
    Assert.True(payload.TryGetProperty("exp", out _), "exp claim required");
    Assert.True(payload.TryGetProperty("iat", out _), "iat claim required");
    Assert.True(payload.TryGetProperty("jti", out _), "jti claim required — used for revocation in Story 2.5");

    // sub must match the seeded TOTP user's ID
    Assert.Equal(DevSeeder.TotpUserId.ToString(), payload.GetProperty("sub").GetString());

    // roles: present as array when enricher adds roles; absent when 0 roles (Epic 2 acceptable)
    // Epic 4a populates real roles — this assertion softly validates the type when present
    if (payload.TryGetProperty("roles", out var rolesProp))
        Assert.Equal(JsonValueKind.Array, rolesProp.ValueKind);
}
```

**Test 3 — `TokenIssuance_P95_UnderBudget`:**

```csharp
[Fact]
public async Task TokenIssuance_P95_UnderBudget()
{
    const int SampleCount = 50;
    const long BudgetMs = 400L;

    // Acquire session token once (outside the measurement loop)
    var step1 = await Client.PostAsync("/connect/token", TotpUserPasswordRequest());
    var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("mfa_session_token").GetString()!;
    var totpCode = CurrentTotpCode();

    var times = new List<long>(SampleCount);

    for (var i = 0; i < SampleCount; i++)
    {
        // Reset replay prevention between samples so the same TOTP code is valid again
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.Id == DevSeeder.TotpUserId);
            user.TotpLastUsedTimeStep = null;
            await db.SaveChangesAsync();
        }

        var sw = Stopwatch.StartNew();
        var step2 = await Client.PostAsync("/connect/token", MfaGrantRequest(mfaToken, totpCode));
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);
        times.Add(sw.ElapsedMilliseconds);
    }

    times.Sort();
    var p95Index = (int)Math.Ceiling(SampleCount * 0.95) - 1;
    var p95Ms = times[p95Index];
    Assert.True(p95Ms <= BudgetMs,
        $"p95 token issuance time {p95Ms}ms exceeded {BudgetMs}ms budget (NFR-2: ≤500ms, leaving 100ms headroom)");
}
```

### Required Usings for Test File

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;  // Base64UrlEncoder
using OneId.Server.Application.TokenPipeline;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;
```

Test file lives at:
`tests/OneId.Server.IntegrationTests/OpenIddict/TokenIssuanceTests.cs`

It must be in the `[Collection("IntegrationTests")]` collection (same as PasswordAuthTests, TotpMfaIntegrationTests) and extend `IntegrationTestBase`.

### JWT Claim Encoding — OpenIddict Rules

- OpenIddict's claim serialization: **N claims with same type → JSON array**; **1 claim → scalar string**; **0 claims → absent**
- `claims.SetDestinations(Destinations.AccessToken)` MUST cover all claims (both base and enricher-added)
- OpenIddict adds `jti`, `iss`, `aud`, `exp`, `iat` automatically — no manual code needed
- `DisableAccessTokenEncryption()` is already set in `Program.cs` → token is a standard JWT readable without decryption

### What NOT to Change

- AR-15: Deferred-skip cap = 3; currently 2 open (`DevSigningKeyStabilityTest`, `TestTokenFactoryContractTests`). All 3 new tests must NOT use `[Fact(Skip)]`.
- AR-10: No direct `IMemoryCache` injection; use `ICacheService` if caching is ever needed inside an enricher.
- The `HandlePasswordGrantAsync` method: **do not move the MFA gate or change the password flow** — only `HandleMfaGrantAsync` needs updating.
- OpenIddict grant type registration: already complete from Story 2.3 (`AllowCustomFlow("urn:oneid:mfa")`).

### Story 2.3 Learnings That Apply Here

- `(string?)request.GetParameter("x")` is the correct OpenIddict 7.5.0 API (not `.Value?.ToString()`)
- `FormUrlEncodedContent` is single-use — always use factory methods
- `DevSigningKeyStabilityTest` fails due to Docker infra, not code — expected 1 infra failure in test run
- The TOTP user (`DevSeeder.TotpUserId`, `DevSeeder.TotpUserEmail`, `DevSeeder.TotpUserTotpSecret = "JBSWY3DPEHPK3PXP"`) is pre-enrolled and available after every `ResetDatabaseAsync()` call

### Performance Baseline Reference

Story 2.3 test run: `dotnet test` completed all 22 passing tests in ~10 seconds total on developer hardware. The 400ms p95 budget for the MFA grant step is expected to be achievable on testcontainer Postgres (local Docker). If p95 is borderline, check:
1. DB connection pool warm-up (first few iterations may be slow — consider discarding first 5 samples if needed)
2. TOTP verification (pure in-memory — not a bottleneck)
3. DPAPI (in-memory key derivation — not a bottleneck at this scale)

## Dev Agent Record

### Completion Notes
_To be populated by dev agent_

### Debug Log
_To be populated by dev agent_

### Files Modified
_To be populated by dev agent_

### Change Log
_To be populated by dev agent_
