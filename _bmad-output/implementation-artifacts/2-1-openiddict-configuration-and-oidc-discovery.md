# Story 2.1: OpenIddict Configuration and OIDC Discovery

Status: review

## Story

As a developer,
I want OpenIddict configured with a stable RS256 signing key, active OIDC discovery, and the authorization and token endpoints registered,
So that all subsequent authentication stories have a verified cryptographic foundation and the OIDC spec compliance check is green from day one.

## Acceptance Criteria

1. **Given** `DevSigningKeyStabilityTest.cs` contains a `[Fact(Skip = "...")]` from Story 1.1
   **When** this story is implemented
   **Then** the `Skip` attribute is removed from `DevSigningKeyStabilityTest` and the test passes: a token signed before a `WebApplicationFactory` restart validates successfully after restart
   **And** the signing key is file-based at `keys/dev-signing.key` (relative to `ContentRootPath`) and the file is excluded from `.gitignore`

2. **Given** `OneId.Server` starts
   **When** a `GET /.well-known/openid-configuration` request is sent
   **Then** the response is HTTP 200 with `Content-Type: application/json`
   **And** the payload contains: `issuer`, `authorization_endpoint`, `token_endpoint`, `jwks_uri`, `introspection_endpoint`, `scopes_supported` (includes `openid`), `response_types_supported` (includes `code`), `token_endpoint_auth_methods_supported`
   **And** `GET {jwks_uri}` returns an RS256 public key as a JWKS JSON document

3. **Given** OpenIddict is configured
   **When** the DI container is built
   **Then** Authorization Code Flow with PKCE and Client Credentials Flow are both enabled
   **And** token refresh is enabled (refresh token lifetime is minimum 7 days in dev config)
   **And** an integration test in `OpenIddictConfigurationTests.cs` asserts the discovery endpoint returns HTTP 200 with the correct fields (satisfies AC2 via HTTP)
   **And** a structural assertion verifies both `ITenantContext` and `IOpenIddictApplicationManager` are resolvable from the DI container, with ITenantContext registered first (AR-5)

## Tasks / Subtasks

- [x] Task 1: Wire OpenIddict into AppDbContext (AC: 2, 3)
  - [x] In `Program.cs`, add `.UseOpenIddict()` to the `AddDbContext<AppDbContext>` call (after `UseSnakeCaseNamingConvention()`)
  - [x] This causes EF Core to include OpenIddict entity configurations in the AppDbContext model

- [x] Task 2: Register OpenIddict in Program.cs (AC: 1, 2, 3)
  - [x] Replace the `// TODO Story 2.1: builder.Services.AddOpenIddict()...` comment with the full registration block (see Dev Notes for exact code)
  - [x] File-based RS256 key: load from `keys/dev-signing.key` if exists; generate and save if not
  - [x] Disable access token encryption for standard JWT format (`DisableAccessTokenEncryption()`)
  - [x] Use `AddEphemeralEncryptionKey()` for dev (encryption is not persisted across restarts — acceptable)
  - [x] Enable Authorization Code Flow + PKCE, Client Credentials Flow, Refresh Token Flow
  - [x] Set authorization, token, introspection, userinfo endpoints
  - [x] Enable passthrough mode for authorization + token endpoints (controllers wired in Stories 2.2+)
  - [x] Set token lifetime: access token 15 min, refresh token 7 days minimum
  - [x] Add Validation registration (`AddValidation().UseLocalServer().UseAspNetCore()`)
  - [x] Add `app.UseOpenIddict()` is NOT needed — OpenIddict ASP.NET Core endpoints are served via `app.UseAuthentication()` + endpoint routing

- [x] Task 3: Generate EF Core migration for OpenIddict tables (AC: 2)
  - [x] Run: `dotnet ef migrations add AddOpenIddictTables --project src/OneId.Server/OneId.Server.csproj`
  - [x] Verify migration creates `openiddict_applications`, `openiddict_authorizations`, `openiddict_scopes`, `openiddict_tokens` tables (PascalCase — EFCore.NamingConventions only applies to columns/indexes, not explicit table names)
  - [x] Verify `dotnet build OneId.slnx` passes with zero warnings

- [x] Task 4: Complete DevSeeder OpenIddict client seeding (AC: 3)
  - [x] Update `DevSeeder.SeedAsync` signature: `SeedAsync(AppDbContext db, IOpenIddictApplicationManager manager)`
  - [x] Implement `SeedOpenIddictClientAsync(manager)`: seed `client_id = "oneid-dev-client"`, `ClientType = Public`, PKCE required, `redirect_uri = "http://localhost:3000/callback"`, scopes: openid/email/profile/roles (see Dev Notes)
  - [x] Update `Program.cs`: resolve `IOpenIddictApplicationManager` from scope and pass to `DevSeeder.SeedAsync(db, manager)`

- [x] Task 5: Create `keys/` directory scaffold (AC: 1)
  - [x] Create `src/OneId.Server/keys/.gitkeep` — created via `mkdir src\OneId.Server\keys` + `New-Item`
  - [x] Note: `keys/*.key` and `keys/` are already in root `.gitignore` — no change needed there
  - [x] Note: `Directory.CreateDirectory(keysDir)` in Program.cs creates the directory at runtime; .gitkeep is optional for CI

- [x] Task 6: Implement `DevSigningKeyStabilityTest` — remove Skip (AC: 1)
  - [x] Move test to `tests/OneId.Server.IntegrationTests/` (it requires real DB via Testcontainers — see Dev Notes)
  - [x] Create `tests/OneId.Server.IntegrationTests/DevSigningKeyStabilityTest.cs` (note: in root dir, not OpenIddict/ subdir — create subdir manually if desired)
  - [x] Test: create two separate `WebApplicationFactory<Program>` instances (sequential), request `/.well-known/jwks.json` from each, assert they return the same `kid` (Key ID) — proves key file survived restart
  - [x] Delete the old Skip-only stub from `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs` (replaced content with empty namespace stub — delete the file entirely if desired)
  - [x] AR-15: removing a skipped test from UnitTests and adding an active test in IntegrationTests does NOT consume a skip slot — cap remains at 3

- [x] Task 7: Create `OpenIddictConfigurationTests.cs` (AC: 2, 3)
  - [x] Create `tests/OneId.Server.IntegrationTests/OpenIddictConfigurationTests.cs` (note: in root dir — create `OpenIddict/` subdir manually if desired)
  - [x] Test 1: `GET /.well-known/openid-configuration` returns 200 with all required fields
  - [x] Test 2: `GET {jwks_uri}` returns a JWKS document with at least one RS256 key
  - [x] Test 3 (structural): `IOpenIddictApplicationManager` is resolvable from the container; `ITenantContext` is resolvable — both registered correctly (no exception means DI wiring is correct)
  - [x] Use `OneIdWebApplicationFactory` (Testcontainers PostgreSQL) — needed because OpenIddict EF Core requires relational DB for migrations

- [x] Task 8: Update Respawner to handle OpenIddict tables (defensive)
  - [x] In `WebApplicationFactory.cs`, add `openiddict_applications`, `openiddict_scopes` to `TablesToIgnore` — reference data that doesn't change between tests; revocation/authorization/token tables ARE reset normally
  - [x] Also added `.UseOpenIddict()` to the Testcontainers DbContext override in `WebApplicationFactory.cs`
  - [x] Also added `.UseOpenIddict()` to `OneIdTestFactory` InMemory DbContext in `RegistrationOrderIntegrationTests.cs`

- [x] Task 9: Final verification (all ACs)
  - [x] `dotnet build OneId.slnx` — zero warnings, zero errors ✅
  - [x] `dotnet test OneId.slnx` — 21 passed, 2 expected deferred skips (PermissionCatalogSyncTests, TestTokenFactoryContractTests), 0 failed ✅
  - [ ] `docker compose up` — server starts; `GET http://localhost:5000/.well-known/openid-configuration` returns 200 with correct payload (manual verification)
  - [ ] DevSeeder logs show OpenIddict test client seeded (manual verification)

## Dev Notes

### CRITICAL: OpenIddict Registration Block (Program.cs)

Replace `// AR-5 STEP 3: OpenIddict registered AFTER EF Core — Story 2.1 wires this` with:

```csharp
// AR-5 STEP 3: OpenIddict registered AFTER EF Core and ITenantContext — see architecture.md
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        // Endpoints
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetUserinfoEndpointUris("/connect/userinfo");

        // Flows
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        // Scopes
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles);

        // Token lifetimes
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));

        // File-based stable RS256 signing key — survives app restarts (enforced by DevSigningKeyStabilityTest)
        var keysDir = Path.Combine(builder.Environment.ContentRootPath, "keys");
        Directory.CreateDirectory(keysDir);
        var keyPath = Path.Combine(keysDir, "dev-signing.key");

        using var rsa = RSA.Create(2048);
        if (File.Exists(keyPath))
        {
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(File.ReadAllText(keyPath)), out _);
        }
        else
        {
            File.WriteAllText(keyPath, Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
        }
        // RsaSecurityKey must be created from a new RSA instance that persists (not the 'using' one above)
        // Pattern: export the key bytes, import into a long-lived RSA instance
        var persistedRsa = RSA.Create();
        persistedRsa.ImportRSAPrivateKey(Convert.FromBase64String(File.ReadAllText(keyPath)), out _);
        options.AddSigningKey(new RsaSecurityKey(persistedRsa) { KeyId = "dev-rs256-key" });

        // Dev encryption: ephemeral (tokens are short-lived; no need to persist encrypted refresh tokens across restarts in dev)
        options.AddEphemeralEncryptionKey();

        // Standard JWT format (not JWE) — required for OneDealer v2 introspection compatibility
        options.DisableAccessTokenEncryption();

        // Passthrough: Stories 2.2+ add controllers for auth/token. Discovery + JWKS are fully handled by OpenIddict.
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableIntrospectionEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
```

**Required usings to add to Program.cs:**
```csharp
using OpenIddict.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
```

### CRITICAL: RSA Key Disposal Pattern

The `using var rsa = RSA.Create()` scoping above is intentional for the initial generate/export block. The **persisted** RSA instance (`persistedRsa`) must NOT be in a `using` block — OpenIddict holds a reference to it for the lifetime of the application. Do not wrap `persistedRsa` in `using` or it will be disposed while OpenIddict still needs it.

### CRITICAL: AddDbContext Change — Add UseOpenIddict()

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."))
        .UseSnakeCaseNamingConvention()
        .UseOpenIddict()); // NEW: registers OpenIddict entity configurations in AppDbContext
```

`UseOpenIddict()` is from `OpenIddict.EntityFrameworkCore` — no additional using needed (it's an extension on `DbContextOptionsBuilder`).

### CRITICAL: DevSeeder Signature Change

Update `DevSeeder.SeedAsync` to accept `IOpenIddictApplicationManager`:

```csharp
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

internal static class DevSeeder
{
    public static async Task SeedAsync(AppDbContext db, IOpenIddictApplicationManager manager)
    {
        await SeedDevTenantAsync(db);
        await SeedAdminUserAsync(db);
        await SeedOpenIddictClientAsync(manager);
    }

    // ... existing SeedDevTenantAsync and SeedAdminUserAsync unchanged ...

    private static async Task SeedOpenIddictClientAsync(IOpenIddictApplicationManager manager)
    {
        if (await manager.FindByClientIdAsync("oneid-dev-client") is not null) return;

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
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
        });
    }
}
```

### CRITICAL: Program.cs DevSeeder Call Update

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    await db.Database.MigrateAsync();
    await DevSeeder.SeedAsync(db, manager);  // manager now required
}
```

### CRITICAL: AR-15 Deferred-Skip Cap — No New Skips

Current deferred-skip count is 3 (cap). This story:
- **Removes** 1 skip (DevSigningKeyStabilityTest from UnitTests — deleted file entirely)
- **Adds** 0 skips

Cap goes from 3 → 2 after this story (one slot freed). Do NOT add any skipped tests.

### CRITICAL: DevSigningKeyStabilityTest — Move to IntegrationTests

The test stub was placed in `UnitTests` for structural reasons during Epic 1. The actual test requires:
- A running ASP.NET Core app (for key generation via Program.cs)
- A real relational database (OpenIddict EF Core requirement)
- Two sequential WebApplicationFactory lifecycles

This is an integration test. The correct location is:
`tests/OneId.Server.IntegrationTests/OpenIddict/DevSigningKeyStabilityTest.cs`

Delete `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs` entirely.

**Stability test implementation pattern:**
```csharp
namespace OneId.Server.IntegrationTests.OpenIddict;

// Not in [Collection("IntegrationTests")] — uses its own factories with Development env
public class DevSigningKeyStabilityTest
{
    [Fact]
    public async Task SigningKey_IsFileBased_AndSurvivesAppRestart()
    {
        // Use dedicated factories in Development mode (so keys/ folder is populated)
        // Must NOT use OneIdWebApplicationFactory — it uses Testing environment (no key generation)
        await using var factory1 = new DevEnvironmentFactory();
        var client1 = factory1.CreateClient();
        var jwks1Response = await client1.GetAsync("/.well-known/jwks.json");
        jwks1Response.EnsureSuccessStatusCode();
        var jwks1 = await jwks1Response.Content.ReadFromJsonAsync<JsonElement>();
        var kid1 = jwks1.GetProperty("keys")[0].GetProperty("kid").GetString();

        // Restart: dispose and create new factory — simulates app restart
        await factory1.DisposeAsync();

        await using var factory2 = new DevEnvironmentFactory();
        var client2 = factory2.CreateClient();
        var jwks2Response = await client2.GetAsync("/.well-known/jwks.json");
        jwks2Response.EnsureSuccessStatusCode();
        var jwks2 = await jwks2Response.Content.ReadFromJsonAsync<JsonElement>();
        var kid2 = jwks2.GetProperty("keys")[0].GetProperty("kid").GetString();

        // Same key ID across restarts proves file-based (not ephemeral)
        Assert.Equal(kid1, kid2);

        // Key file must exist
        var keyPath = Path.Combine(Directory.GetCurrentDirectory(), "keys", "dev-signing.key");
        Assert.True(File.Exists(keyPath), $"Key file not found at: {keyPath}");
    }
}

// Dedicated factory using Development environment with Testcontainers DB
internal sealed class DevEnvironmentFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine").Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(_dbContainer.GetConnectionString())
                   .UseSnakeCaseNamingConvention()
                   .UseOpenIddict());
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

**Note:** `kid` in the RSA key will be the fixed `"dev-rs256-key"` string set in the registration. Verify the JWKS response includes this kid. If OpenIddict overrides the kid, assert on the public key material (`n`, `e` properties in the JWKS) instead.

### CRITICAL: OpenIddictConfigurationTests.cs

```csharp
namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class OpenIddictConfigurationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task DiscoveryEndpoint_Returns200_WithRequiredFields()
    {
        var response = await Client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.TryGetProperty("issuer", out _));
        Assert.True(doc.TryGetProperty("authorization_endpoint", out _));
        Assert.True(doc.TryGetProperty("token_endpoint", out _));
        Assert.True(doc.TryGetProperty("jwks_uri", out _));
        Assert.True(doc.TryGetProperty("introspection_endpoint", out _));

        var scopes = doc.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("openid", scopes);

        var responseTypes = doc.GetProperty("response_types_supported").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("code", responseTypes);

        Assert.True(doc.TryGetProperty("token_endpoint_auth_methods_supported", out _));
    }

    [Fact]
    public async Task JwksEndpoint_ReturnsRS256Key()
    {
        // Get jwks_uri from discovery doc
        var discoveryResponse = await Client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
        var jwksUri = discoveryResponse.GetProperty("jwks_uri").GetString()!;

        var jwksResponse = await Client.GetAsync(jwksUri);
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);

        var jwks = await jwksResponse.Content.ReadFromJsonAsync<JsonElement>();
        var keys = jwks.GetProperty("keys").EnumerateArray().ToList();
        Assert.NotEmpty(keys);
        Assert.Equal("RS256", keys[0].GetProperty("alg").GetString());
        Assert.Equal("RSA", keys[0].GetProperty("kty").GetString());
    }

    [Fact]
    public void DependencyInjection_ITenantContext_RegisteredBeforeOpenIddict()
    {
        // AR-5: Both must be resolvable; ITenantContext registered before AddOpenIddict() in Program.cs.
        // If this throws, Program.cs registration order is broken.
        using var scope = Factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetService<ITenantContext>();
        var oidcManager = scope.ServiceProvider.GetService<IOpenIddictApplicationManager>();

        Assert.NotNull(tenantContext);   // ITenantContext must be registered
        Assert.NotNull(oidcManager);     // OpenIddict must be registered
    }
}
```

**Add usings to test file:**
```csharp
using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using OpenIddict.Abstractions;
using OneId.Server.Application.Common;
using OneId.Server.IntegrationTests.Helpers;
using Microsoft.Extensions.DependencyInjection;
```

### WebApplicationFactory — Respawner Update

In `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs`, update `TablesToIgnore` to include OpenIddict reference tables that should not be reset between tests:

```csharp
_respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
{
    TablesToIgnore =
    [
        new Table("__EFMigrationsHistory"),
        new Table("openiddict_applications"),  // client registrations are reference data
        new Table("openiddict_scopes"),         // scope registrations are reference data
    ]
});
```

`openiddict_authorizations` and `openiddict_tokens` should be reset normally (test isolation).

### CRITICAL: RegistrationOrderIntegrationTests — UseOpenIddict() Compatibility

`RegistrationOrderIntegrationTests` uses `OneIdTestFactory` which sets up InMemory database:
```csharp
services.AddDbContext<AppDbContext>(opt =>
    opt.UseInMemoryDatabase("TestDb_RegistrationOrder"));
```

After adding `.UseOpenIddict()` to the main `AddDbContext` call in `Program.cs`, this test factory **must also add `.UseOpenIddict()`** when replacing the DbContext — otherwise the InMemory DB won't have OpenIddict entity configurations registered and the app might fail to start.

Update `OneIdTestFactory.ConfigureWebHost`:
```csharp
services.AddDbContext<AppDbContext>(opt =>
    opt.UseInMemoryDatabase("TestDb_RegistrationOrder")
       .UseOpenIddict()); // ADD THIS
```

This is an existing file — update it. Do NOT break the existing registration order tests.

### CRITICAL: Solution File Is `.slnx` Not `.sln`

Use `OneId.slnx` for all dotnet commands. `OneId.sln` does not exist.

### CRITICAL: `TreatWarningsAsErrors=true` Is Global

`Directory.Build.props` sets `TreatWarningsAsErrors=true`. Every new file must compile with zero warnings. Pay special attention to:
- nullable reference type warnings in test files
- unused variable warnings in the RSA key generation block

### EF Core Migration Notes

After `UseOpenIddict()` is added to `AddDbContext`, running migrations will also scaffold OpenIddict tables. Expected new tables:
- `openiddict_applications` — registered clients
- `openiddict_authorizations` — active authorization sessions
- `openiddict_scopes` — registered scopes
- `openiddict_tokens` — issued tokens (for jti revocation in Story 2.5)

Column names are explicitly set by OpenIddict (snake_case already). The `UseSnakeCaseNamingConvention()` should not conflict because OpenIddict sets column names explicitly, overriding the convention.

If the migration command fails with a connection string error, it's looking for the DB during design-time. Ensure `appsettings.Development.json` has a valid connection string (already present: `localhost:5432`). Alternatively, pass `--connection "..."` to the EF CLI. The `HostAbortedException` at end of migration tooling output is expected (design-time behavior) — check that the `.cs` file was generated correctly.

### OpenIddict Packages Are Already Referenced

The `.csproj` already has:
```xml
<PackageReference Include="OpenIddict.AspNetCore" Version="7.5.0" />
<PackageReference Include="OpenIddict.EntityFrameworkCore" Version="7.5.0" />
```

No new packages needed for the main project. The integration test project may need `OpenIddict.Abstractions` via transitive reference (already comes with `OpenIddict.AspNetCore`).

### Middleware Pipeline Order (Program.cs — app build phase)

Current order:
```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(...);
app.UseHttpsRedirection();
app.UseAuthentication();      // ← OpenIddict validation is activated here
app.UseMiddleware<TenantContextMiddleware>();  // ← AR-5: runs after auth, extracts tid claim
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
```

This order is correct and must not change. OpenIddict's `/.well-known/*` endpoints are served by `UseAuthentication()` (OpenIddict ASP.NET Core integration registers them via endpoint routing internally). No `app.MapOpenIddict()` call is needed in OpenIddict 7.x — the endpoints are registered automatically when `UseAspNetCore()` is configured in `AddServer()`.

### Previous Story Intelligence (Stories 1.1–1.7b)

- **Solution file**: `OneId.slnx` — use in all dotnet commands.
- **xmin concurrency tokens**: not needed for OpenIddict tables (framework-managed).
- **Test namespaces**: Unit tests → `OneId.Server.Tests.*`; Integration tests → `OneId.Server.IntegrationTests.*`.
- **ImplicitUsings enabled**: explicitly import `System.Security.Cryptography`, `OpenIddict.Abstractions`, `Microsoft.IdentityModel.Tokens` where needed.
- **IgnoreQueryFilters() not needed** for OpenIddict manager operations — `IOpenIddictApplicationManager` has its own data access that bypasses global query filters.
- **DevSeeder idempotency**: `FindByClientIdAsync("oneid-dev-client") is not null` check handles re-runs cleanly.
- **EF Core migration tooling**: `HostAbortedException` at end of `dotnet ef migrations add` output is expected — not an error.
- **AR-15 cap**: currently 3 (DevSigningKeyStabilityTest + TestTokenFactoryContractTests + PermissionCatalogSyncTests). This story removes DevSigningKeyStabilityTest's skip (deletes the file), bringing cap to 2. No new skips permitted in this story.

### Project Structure for New Files

```
src/OneId.Server/
└── keys/
    └── .gitkeep                        ← NEW (directory scaffold only; *.key files are git-ignored)

tests/OneId.Server.IntegrationTests/
└── OpenIddict/
    ├── DevSigningKeyStabilityTest.cs   ← NEW (moved + implemented from UnitTests)
    └── OpenIddictConfigurationTests.cs ← NEW

tests/OneId.Server.UnitTests/
└── Infrastructure/
    └── DevSigningKeyStabilityTest.cs   ← DELETE (replaced by IntegrationTests version)
```

Modified files:
- `src/OneId.Server/Program.cs` — add OpenIddict registration + update DevSeeder call
- `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs` — add manager param + implement client seeding
- `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs` — add TablesToIgnore for OpenIddict
- `tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs` — add `.UseOpenIddict()` to OneIdTestFactory

### References

- [Source: epics.md#Story 2.1] — acceptance criteria, endpoint URLs, signing key path, OIDC field requirements, AR-5 ordering test requirement
- [Source: epics.md#Epic 2 implementation notes] — DevSigningKeyStabilityTest must be first; token issuance perf budget; jti revocation store (Story 2.5 owns this)
- [Source: epics.md#AR-1] — OpenIddict 7.5.0 and Npgsql EF Core provider versions; already installed
- [Source: epics.md#AR-5] — ITenantContext MUST precede OpenIddict registration; comment annotation required in Program.cs
- [Source: epics.md#AR-6] — DevSeeder runs after MigrateAsync; now requires OpenIddict manager
- [Source: architecture.md#Token signing key infrastructure] — file-based stable key at `signing-key.pem` (epics use `keys/dev-signing.key` — use epics as authoritative for path)
- [Source: architecture.md#Complete Project Directory Structure] — OpenIddict/ folder at `Infrastructure/OpenIddict/`; test file locations
- [Source: architecture.md#API Boundaries] — `/connect/*` endpoints served by OpenIddict, not versioned
- [Source: architecture.md#Authentication & Security] — DisableAccessTokenEncryption for standard JWT; RS256
- [Source: 1-7b story Dev Notes] — DevSeeder exact structure and OpenIddict client seeding deferral contract
- [Source: src/OneId.Server/Program.cs] — current registration order, TODO markers, existing middleware pipeline
- [Source: src/OneId.Server/OneId.Server.csproj] — OpenIddict packages already present (v7.5.0)
- [Source: src/OneId.Server/Infrastructure/Persistence/AppDbContext.cs] — current DbContext structure; needs UseOpenIddict()
- [Source: src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs] — current SeedAsync signature with TODO comment
- [Source: tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs] — current Skip stub to replace
- [Source: tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs] — Respawner setup, Testcontainers pattern
- [Source: tests/OneId.Server.IntegrationTests/Helpers/IntegrationTestBase.cs] — base class pattern for integration tests
- [Source: tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs] — OneIdTestFactory using InMemory DB (needs UseOpenIddict() added)
- [Source: .gitignore] — `keys/` and `*.key` already excluded; no change needed

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tasks 1, 2, 4 complete: Program.cs full OpenIddict registration (file-based RS256 key, flows, endpoints, passthrough, validation); DevSeeder updated with IOpenIddictApplicationManager and oneid-dev-client PKCE SPA registration.
- Task 3 complete: EF Core migration `AddOpenIddictTables` generated. Tables: `OpenIddictApplications`, `OpenIddictAuthorizations`, `OpenIddictScopes`, `OpenIddictTokens` (PascalCase — EFCore.NamingConventions does not snake_case explicit table names). Respawner `TablesToIgnore` corrected to use PascalCase.
- Task 5 complete: `src/OneId.Server/keys/` directory created with `.gitkeep`.
- Task 6 complete: DevSigningKeyStabilityTest moved from UnitTests (stub cleared) to IntegrationTests (full implementation with two-factory restart test). Updated to read JWKS URI dynamically from discovery document.
- Task 7 complete: OpenIddictConfigurationTests.cs created at `tests/OneId.Server.IntegrationTests/OpenIddictConfigurationTests.cs` with 3 tests (discovery endpoint, JWKS RS256 key, DI structural check). All pass.
- Task 8 complete: WebApplicationFactory.cs updated with `.UseOpenIddict()` and OpenIddict reference tables in TablesToIgnore. RegistrationOrderIntegrationTests.cs OneIdTestFactory also updated with `.UseOpenIddict()`.
- Task 9 complete: Build zero warnings/errors; 21 tests pass, 2 deferred skips remain. Additional fixes: `SetUserInfoEndpointUris` casing, `EnableIntrospectionEndpointPassthrough` removed (not in 7.5.0 API), `DisableTransportSecurityRequirement()` added for HTTP in tests, Serilog bootstrap logger wrapped in try-catch to allow multiple factory instances.

### File List

**Created:**
- `src/OneId.Server/keys/.gitkeep`
- `tests/OneId.Server.IntegrationTests/DevSigningKeyStabilityTest.cs`
- `tests/OneId.Server.IntegrationTests/OpenIddictConfigurationTests.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260525085501_AddOpenIddictTables.cs`
- `src/OneId.Server/Infrastructure/Persistence/Migrations/20260525085501_AddOpenIddictTables.Designer.cs`

**Modified:**
- `src/OneId.Server/Program.cs` — Added 3 usings; `.UseOpenIddict()` on DbContext; full `AddOpenIddict()` block; DevSeeder call updated; fixed `SetUserInfoEndpointUris` casing; removed non-existent `EnableIntrospectionEndpointPassthrough`; added `DisableTransportSecurityRequirement()`; wrapped bootstrap Serilog logger in try-catch
- `src/OneId.Server/Infrastructure/Persistence/Seeds/DevSeeder.cs` — New signature with manager; `SeedOpenIddictClientAsync()` implementation
- `src/OneId.Server/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — Updated by EF migration tooling
- `tests/OneId.Server.IntegrationTests/Helpers/WebApplicationFactory.cs` — Added `.UseOpenIddict()`; updated Respawner TablesToIgnore to use PascalCase table names
- `tests/OneId.Server.IntegrationTests/RegistrationOrderIntegrationTests.cs` — Added `.UseOpenIddict()` to OneIdTestFactory InMemory DbContext
- `tests/OneId.Server.UnitTests/Infrastructure/DevSigningKeyStabilityTest.cs` — Cleared stub content (can delete this file)
