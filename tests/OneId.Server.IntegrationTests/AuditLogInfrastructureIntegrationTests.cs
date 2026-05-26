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
[Trait("Category", "AuditLog")]
public class AuditLogInfrastructureIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
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

    // AC6: UpdateTenant writes audit entry visible to TenantAdmin
    [Fact]
    public async Task UpdateTenant_WritesAuditEntry_VisibleToTenantAdmin()
    {
        var client = await AuthClientAsync();

        // Get DevTenant to obtain current version
        var getResp = await client.GetAsync($"/api/internal/tenants/{DevSeeder.DevTenantId}");
        getResp.EnsureSuccessStatusCode();
        var tenantBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var version = tenantBody.GetProperty("version").GetUInt32();

        // Update the tenant name
        var patchResp = await client.PatchAsJsonAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}",
            new { name = "Updated Dev Tenant", version });
        patchResp.EnsureSuccessStatusCode();

        // Restore original name
        var updatedBody = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        var newVersion = updatedBody.GetProperty("version").GetUInt32();
        await client.PatchAsJsonAsync(
            $"/api/internal/tenants/{DevSeeder.DevTenantId}",
            new { name = "Dev Tenant", version = newVersion });

        // TenantAdmin reads audit log
        var auditResp = await client.GetAsync("/api/tenant/audit");
        Assert.Equal(HttpStatusCode.OK, auditResp.StatusCode);

        var auditBody = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = auditBody.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, item =>
            item.GetProperty("action").GetString() == "tenant.updated" &&
            Guid.Parse(item.GetProperty("entityId").GetString()!) == DevSeeder.DevTenantId &&
            Guid.Parse(item.GetProperty("tenantId").GetString()!) == DevSeeder.DevTenantId);
    }

    // AC6: Pagination — correct subset returned
    [Fact]
    public async Task AuditLog_Pagination_ReturnsCorrectSubset()
    {
        var client = await AuthClientAsync();

        // Create a new tenant to generate a tenant.created audit entry
        await client.PostAsJsonAsync("/api/internal/tenants", new { name = "PaginationTest Corp" });

        // Request page 1 with pageSize=1
        var resp = await client.GetAsync("/api/tenant/audit?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
    }

    // AC1/6: TenantId isolation — second tenant's entries not visible
    [Fact]
    public async Task AuditLog_TenantIsolation_SeparateTenantEntriesNotVisible()
    {
        var client = await AuthClientAsync();

        // Create a second tenant
        var createResp = await client.PostAsJsonAsync("/api/internal/tenants",
            new { name = "Isolated Tenant" });
        createResp.EnsureSuccessStatusCode();
        var newTenantBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var newTenantId = Guid.Parse(newTenantBody.GetProperty("id").GetString()!);

        // TotpUser belongs to DevTenant — their audit log should NOT contain the new tenant's entries
        var auditResp = await client.GetAsync("/api/tenant/audit?pageSize=100");
        auditResp.EnsureSuccessStatusCode();

        var auditBody = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = auditBody.GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(items, item =>
            Guid.Parse(item.GetProperty("tenantId").GetString()!) == newTenantId);
    }

    // AC4: Unauthenticated request returns 401
    [Fact]
    public async Task AuditLog_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/tenant/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
