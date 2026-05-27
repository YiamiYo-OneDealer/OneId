using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
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
[Trait("Category", "TenantAdmin")]
public class UserLifecycleIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    // ── AC2: POST creates user ─────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidUser_Returns201WithUserDto()
    {
        var client = await AuthClientAsync();

        var response = await client.PostAsJsonAsync("/api/tenant/users",
            new { email = $"new-{Guid.NewGuid():N}@example.com", displayName = "New User" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.GetProperty("id").GetString()!));
        Assert.Equal("New User", body.GetProperty("displayName").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Post_DuplicateEmailSameTenant_Returns409()
    {
        var client = await AuthClientAsync();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "First" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Second" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("email_conflict", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_AuditEntry_IsWrittenOnCreate()
    {
        var client = await AuthClientAsync();
        var email = $"audit-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "AuditTest" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        var auditResp = await client.GetAsync("/api/tenant/audit?pageSize=50");
        auditResp.EnsureSuccessStatusCode();
        var audit = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = audit.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, e =>
            e.GetProperty("action").GetString() == "user.created" &&
            e.GetProperty("entityId").GetString() == userId);
    }

    // ── AC3: PATCH updates user ────────────────────────────────────────────────

    [Fact]
    public async Task Patch_DisplayName_Returns200WithUpdatedDto()
    {
        var client = await AuthClientAsync();
        var email = $"patch-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Original" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        var patchResp = await client.PatchAsJsonAsync($"/api/tenant/users/{userId}",
            new { displayName = "Updated Name" });
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
        var updated = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", updated.GetProperty("displayName").GetString());
        Assert.Equal(email, updated.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Patch_NonExistentUser_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.PatchAsJsonAsync($"/api/tenant/users/{Guid.NewGuid()}",
            new { displayName = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_AuditEntry_IsWrittenOnUpdate()
    {
        var client = await AuthClientAsync();
        var email = $"patch-audit-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Before" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        await client.PatchAsJsonAsync($"/api/tenant/users/{userId}", new { displayName = "After" });

        var auditResp = await client.GetAsync("/api/tenant/audit?pageSize=50");
        auditResp.EnsureSuccessStatusCode();
        var audit = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = audit.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, e =>
            e.GetProperty("action").GetString() == "user.updated" &&
            e.GetProperty("entityId").GetString() == userId);
    }

    // ── AC4: DELETE deactivates user ──────────────────────────────────────────

    [Fact]
    public async Task Delete_ActiveUser_Returns204AndDeactivates()
    {
        var client = await AuthClientAsync();
        var email = $"del-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "ToDelete" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        var deleteResp = await client.DeleteAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // GET must still return the user with isActive=false
        var getResp = await client.GetAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(dto.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Delete_AlreadyInactiveUser_Returns204Idempotent()
    {
        var client = await AuthClientAsync();
        var email = $"del-idem-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Idem" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        await client.DeleteAsync($"/api/tenant/users/{userId}");
        var second = await client.DeleteAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentUser_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.DeleteAsync($"/api/tenant/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AuditEntry_IsWrittenOnDeactivate()
    {
        var client = await AuthClientAsync();
        var email = $"del-audit-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "AuditDelete" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        await client.DeleteAsync($"/api/tenant/users/{userId}");

        var auditResp = await client.GetAsync("/api/tenant/audit?pageSize=50");
        auditResp.EnsureSuccessStatusCode();
        var audit = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = audit.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, e =>
            e.GetProperty("action").GetString() == "user.deactivated" &&
            e.GetProperty("entityId").GetString() == userId);
    }

    // ── AC5: GET list with pagination ─────────────────────────────────────────

    [Fact]
    public async Task GetList_ExcludesInactiveByDefault()
    {
        var client = await AuthClientAsync();
        var email = $"inactive-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Inactive" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;
        await client.DeleteAsync($"/api/tenant/users/{userId}");

        var listResp = await client.GetAsync("/api/tenant/users?pageSize=100");
        listResp.EnsureSuccessStatusCode();
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToList();
        Assert.DoesNotContain(userId, items);
    }

    [Fact]
    public async Task GetList_IncludesInactiveWhenFlagSet()
    {
        var client = await AuthClientAsync();
        var email = $"inactive2-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Inactive2" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;
        await client.DeleteAsync($"/api/tenant/users/{userId}");

        var listResp = await client.GetAsync("/api/tenant/users?includeInactive=true&pageSize=100");
        listResp.EnsureSuccessStatusCode();
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToList();
        Assert.Contains(userId, items);
    }

    [Fact]
    public async Task GetList_ReturnsPaginatedResponse()
    {
        var client = await AuthClientAsync();

        var listResp = await client.GetAsync("/api/tenant/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalCount", out _));
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
    }

    // ── AC6: GET single user ─────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingUser_Returns200()
    {
        var client = await AuthClientAsync();
        var email = $"get-{Guid.NewGuid():N}@example.com";

        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "GetTest" });
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;

        var getResp = await client.GetAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, dto.GetProperty("id").GetString());
        Assert.Equal("GetTest", dto.GetProperty("displayName").GetString());
        Assert.True(dto.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GetById_NonExistentUser_Returns404()
    {
        var client = await AuthClientAsync();
        var response = await client.GetAsync($"/api/tenant/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── AC7: 403 without TenantAdmin role ─────────────────────────────────────

    [Fact]
    public async Task AllEndpoints_WithoutAuth_Return401()
    {
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/tenant/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync($"/api/tenant/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/tenant/users", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PatchAsJsonAsync($"/api/tenant/users/{id}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.DeleteAsync($"/api/tenant/users/{id}")).StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_WithNonTenantAdminRole_Return403()
    {
        // admin@oneid.dev is InternalAdmin only — JWT contains no TenantAdmin role
        var tokenResp = await Client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = "admin@oneid.dev",
                ["password"] = "Admin123!",
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));
        tokenResp.EnsureSuccessStatusCode();
        var token = (await tokenResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/tenant/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/tenant/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/tenant/users", new { email = "x@x.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/tenant/users/{id}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/tenant/users/{id}")).StatusCode);
    }

    // ── AC8: Full lifecycle sequence ─────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_CreateUpdateDeactivate_AuditTrailComplete()
    {
        var client = await AuthClientAsync();
        var email = $"lifecycle-{Guid.NewGuid():N}@example.com";

        // POST create
        var createResp = await client.PostAsJsonAsync("/api/tenant/users",
            new { email, displayName = "Initial Name" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var user = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var userId = user.GetProperty("id").GetString()!;
        Assert.True(user.GetProperty("isActive").GetBoolean());

        // PATCH update
        var patchResp = await client.PatchAsJsonAsync($"/api/tenant/users/{userId}",
            new { displayName = "Updated Name" });
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
        var updated = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", updated.GetProperty("displayName").GetString());

        // GET verify update
        var getResp = await client.GetAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var fetched = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", fetched.GetProperty("displayName").GetString());

        // DELETE deactivate
        var deleteResp = await client.DeleteAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // GET verify isActive=false (not 404)
        var getAfterDelete = await client.GetAsync($"/api/tenant/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getAfterDelete.StatusCode);
        var deactivated = await getAfterDelete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(deactivated.GetProperty("isActive").GetBoolean());

        // Verify 3 audit entries: user.created, user.updated, user.deactivated in timestamp order
        var auditResp = await client.GetAsync("/api/tenant/audit?pageSize=50");
        auditResp.EnsureSuccessStatusCode();
        var audit = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
        var userAuditEntries = audit.GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("entityId").GetString() == userId)
            .OrderBy(e => e.GetProperty("timestamp").GetString())
            .Select(e => e.GetProperty("action").GetString())
            .ToList();
        Assert.Equal(3, userAuditEntries.Count);
        Assert.Equal("user.created", userAuditEntries[0]);
        Assert.Equal("user.updated", userAuditEntries[1]);
        Assert.Equal("user.deactivated", userAuditEntries[2]);

        // TODO (Phase 6): assert seatsUsed decremented via GET /api/tenant/license once licensing endpoint is implemented
    }
}
