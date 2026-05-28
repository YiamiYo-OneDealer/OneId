using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
public class InternalPermissionsIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    // ── AC4: POST creates permission ──────────────────────────────────────────

    [Fact]
    public async Task Post_ValidPermission_Returns201WithVersionField()
    {
        var client = await AuthClientAsync();
        var response = await client.PostAsJsonAsync("/api/internal/permissions",
            new { permissionId = "od.test.create", label = "Test Create" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("od.test.create", body.GetProperty("permissionId").GetString());
        Assert.Equal("Test Create", body.GetProperty("label").GetString());
        Assert.Equal("Active", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("version").GetUInt32() > 0);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Post_DuplicatePermissionId_Returns409()
    {
        var client = await AuthClientAsync();
        await client.PostAsJsonAsync("/api/internal/permissions",
            new { permissionId = "od.dup.test", label = "First" });
        var response = await client.PostAsJsonAsync("/api/internal/permissions",
            new { permissionId = "od.dup.test", label = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("permission_id_taken", body.GetProperty("error").GetString());
    }

    // ── AC5: GET list ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_List_ReturnsPaginatedResult()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync("/api/internal/permissions?pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() > 0);
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("items").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Get_ListWithStatusAll_ReturnsAllPermissions()
    {
        var client = await AuthClientAsync();
        // Deactivate one seeded permission first
        await client.DeleteAsync("/api/internal/permissions/od.crm.read");

        var activeResponse = await client.GetAsync("/api/internal/permissions?status=Active&pageSize=100");
        var allResponse    = await client.GetAsync("/api/internal/permissions?status=All&pageSize=100");

        var activeCount = (await activeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("totalCount").GetInt32();
        var allCount = (await allResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("totalCount").GetInt32();

        Assert.True(allCount > activeCount);
    }

    // ── AC6: GET single ───────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ById_ExistingPermission_Returns200()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync("/api/internal/permissions/od.crm.read");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("od.crm.read", body.GetProperty("permissionId").GetString());
    }

    [Fact]
    public async Task Get_ById_NonExistentPermission_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync("/api/internal/permissions/od.does.not.exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC7: PATCH update ─────────────────────────────────────────────────────

    [Fact]
    public async Task Patch_ValidVersion_Returns200UpdatedLabel()
    {
        var client = await AuthClientAsync();

        // Get current version
        var getResp = await client.GetAsync("/api/internal/permissions/od.crm.read");
        var current = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var version = current.GetProperty("version").GetUInt32();

        var patchResp = await client.PatchAsJsonAsync(
            "/api/internal/permissions/od.crm.read",
            new { label = "CRM — Read (Updated)", version });

        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
        var body = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CRM — Read (Updated)", body.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Patch_StaleVersion_Returns409()
    {
        var client = await AuthClientAsync();
        var response = await client.PatchAsJsonAsync(
            "/api/internal/permissions/od.crm.read",
            new { label = "Stale attempt", version = 0u });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── AC8: DELETE deactivate ────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingPermission_Returns204AndSetsInactive()
    {
        var client = await AuthClientAsync();
        var deleteResp = await client.DeleteAsync("/api/internal/permissions/od.crm.write");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify soft-delete: active list no longer contains it
        var activeResp = await client.GetAsync("/api/internal/permissions/od.crm.write");
        var body = await activeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inactive", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Delete_NonExistentPermission_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.DeleteAsync("/api/internal/permissions/od.nonexistent.perm");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC5: POST activate (reactivate) ──────────────────────────────────────

    [Fact]
    public async Task Post_Activate_DeactivateThenReactivate_StatusReturnsToActive()
    {
        var client = await AuthClientAsync();

        // Deactivate first
        var deleteResp = await client.DeleteAsync("/api/internal/permissions/od.crm.read");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify it is now Inactive
        var afterDeactivate = await client.GetAsync("/api/internal/permissions/od.crm.read");
        var deactivatedBody = await afterDeactivate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inactive", deactivatedBody.GetProperty("status").GetString());

        // Reactivate
        var activateResp = await client.PostAsync("/api/internal/permissions/od.crm.read/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activateResp.StatusCode);

        // Verify status is Active again
        var afterActivate = await client.GetAsync("/api/internal/permissions/od.crm.read");
        var reactivatedBody = await afterActivate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", reactivatedBody.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Post_Activate_NonExistentPermission_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.PostAsync("/api/internal/permissions/od.does.not.exist/activate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC10: Auth enforcement ────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Request_Returns401()
    {
        // Client without auth header
        var response = await Client.GetAsync("/api/internal/permissions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── AC4,7,8: Audit entries created ───────────────────────────────────────

    [Fact]
    public async Task Create_WritesAuditEntry()
    {
        var client = await AuthClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/internal/permissions",
            new { permissionId = "od.audit.test", label = "Audit Test" });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var permissionId = created.GetProperty("id").GetGuid();

        // Verify audit entry in DB
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditLogs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Action == "permission.created" && a.TenantId == Guid.Empty);

        Assert.NotNull(entry);
        Assert.Equal("Permission", entry.EntityType);
        Assert.Equal(permissionId, entry.EntityId);
    }

    // ── AC7: PATCH 404 for non-existent permissionId ──────────────────────────

    [Fact]
    public async Task Patch_NonExistentPermission_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.PatchAsJsonAsync(
            "/api/internal/permissions/od.does.not.exist",
            new { label = "Ghost", version = 1u });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
