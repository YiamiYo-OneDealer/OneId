using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.Infrastructure.Persistence.Seeds;
using OneId.Server.IntegrationTests.Helpers;
using OtpNet;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
[Trait("Category", "InternalAdmin")]
public class InternalTenantsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // TotpUser is the only fully-enrolled user — requires password step + MFA step.
    private async Task<HttpClient> AuthClientAsync()
    {
        var step1 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = DevSeeder.TotpUserEmail,
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));
        step1.EnsureSuccessStatusCode();
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:oneid:mfa",
                ["mfa_session_token"] = mfaToken,
                ["totp_code"] = new Totp(Base32Encoding.ToBytes(DevSeeder.TotpUserTotpSecret))
                                    .ComputeTotp(DateTime.UtcNow),
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));
        step2.EnsureSuccessStatusCode();
        var token = (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── AC1: POST creates tenant ──────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidName_Returns201WithVersionField()
    {
        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync("/api/internal/tenants",
            new { name = "Acme Corp" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Acme Corp", body.GetProperty("name").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Post_DuplicateName_Returns400NameTaken()
    {
        var client = await AuthClientAsync();
        await client.PostAsJsonAsync("/api/internal/tenants", new { name = "Dup Tenant" });
        var response = await client.PostAsJsonAsync("/api/internal/tenants", new { name = "Dup Tenant" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("name_taken", body.GetProperty("error").GetString());
    }

    // ── AC2: GET list ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_TwoActiveTenants_BothAppear()
    {
        // Seed a second active tenant
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Second Tenant",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = await AuthClientAsync();
        var response = await client.GetAsync("/api/internal/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tenants = body.EnumerateArray().ToList();
        Assert.True(tenants.Count >= 2, $"Expected at least 2 tenants, got {tenants.Count}");
        var names = tenants.Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("Dev Tenant", names);
        Assert.Contains("Second Tenant", names);
    }

    [Fact]
    public async Task GetList_ExcludesSoftDeletedTenants()
    {
        // Seed and deactivate a tenant
        Guid deactivatedId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Deactivated Tenant",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                DeletedAt = DateTimeOffset.UtcNow,
            };
            db.Tenants.Add(t);
            await db.SaveChangesAsync();
            deactivatedId = t.Id;
        }

        var client = await AuthClientAsync();
        var response = await client.GetAsync("/api/internal/tenants");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.EnumerateArray().Select(t => t.GetProperty("id").GetString()).ToList();
        Assert.DoesNotContain(deactivatedId.ToString(), ids);
    }

    // ── AC3: GET single tenant ────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingTenant_Returns200WithVersion()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(DevSeeder.DevTenantId.ToString(), body.GetProperty("id").GetString());
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/internal/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_SoftDeletedTenant_Returns404()
    {
        Guid id;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "To Delete",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                DeletedAt = DateTimeOffset.UtcNow,
            };
            db.Tenants.Add(t);
            await db.SaveChangesAsync();
            id = t.Id;
        }

        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/internal/tenants/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC4: PATCH update tenant ──────────────────────────────────────────────

    [Fact]
    public async Task Patch_ValidVersion_Returns200WithUpdatedName()
    {
        var client = await AuthClientAsync();

        // Get current version
        var getResp = await client.GetAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}");
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var version = getBody.GetProperty("version").GetUInt32();

        var patchResp = await client.PatchAsJsonAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}",
            new { name = "Dev Tenant Updated", version });

        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
        var body = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Dev Tenant Updated", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
    }

    [Fact]
    public async Task Patch_StaleVersion_Returns409Conflict()
    {
        var client = await AuthClientAsync();
        const uint staleVersion = 1u;

        var response = await client.PatchAsJsonAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}",
            new { name = "Any Name", version = staleVersion });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Patch_DuplicateName_Returns400NameTaken()
    {
        var client = await AuthClientAsync();

        // Create a second tenant
        var createResp = await client.PostAsJsonAsync("/api/internal/tenants",
            new { name = "Other Tenant" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // Get Dev Tenant version
        var getResp = await client.GetAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}");
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var version = getBody.GetProperty("version").GetUInt32();

        // Try to rename Dev Tenant to existing name
        var patchResp = await client.PatchAsJsonAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}",
            new { name = "Other Tenant", version });

        Assert.Equal(HttpStatusCode.BadRequest, patchResp.StatusCode);
        var body = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("name_taken", body.GetProperty("error").GetString());
    }

    // ── AC5: DELETE deactivates tenant ────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingTenant_Returns204AndSubsequentGetReturns404()
    {
        // Create a fresh tenant to deactivate (don't deactivate DevTenant — other tests use it)
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/internal/tenants",
            new { name = "Tenant To Deactivate" });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetString();

        var deleteResp = await client.DeleteAsync($"/api/internal/tenants/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/internal/tenants/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentId_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.DeleteAsync($"/api/internal/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Unauthenticated ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_Unauthenticated_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/internal/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
