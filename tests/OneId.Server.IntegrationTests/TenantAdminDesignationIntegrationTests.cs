using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
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
public class TenantAdminDesignationIntegrationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
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

    private async Task<string> IssueTwoStepTokenAsync(string email, string password, string totpBase32Secret)
    {
        var anonClient = Factory.CreateClient();
        var step1 = await anonClient.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = email,
                ["password"] = password,
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));
        step1.EnsureSuccessStatusCode();
        var mfaToken = (await step1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mfa_session_token").GetString()!;

        var step2 = await anonClient.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:oneid:mfa",
                ["mfa_session_token"] = mfaToken,
                ["totp_code"] = new Totp(Base32Encoding.ToBytes(totpBase32Secret))
                                    .ComputeTotp(DateTime.UtcNow),
                ["client_id"] = "oneid-dev-client",
                ["scope"] = "openid",
            }));
        step2.EnsureSuccessStatusCode();
        return (await step2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }

    // Seeds a TOTP-enrolled user in the given tenant and returns (userId, base32Secret, password).
    private async Task<(Guid UserId, string TotpBase32Secret, string Password)> SeedTotpEnrolledUserAsync(
        Guid tenantId, string email)
    {
        const string password = "Test123!";
        const string totpSecret = "JBSWY3DPEHPK3PXP"; // stable test vector (same as DevSeeder)

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dp = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsTotpEnrolled = true,
            TotpSecret = dp.CreateProtector("totp.secret.v1").Protect(totpSecret),
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (userId, totpSecret, password);
    }

    private static List<string?> GetRolesFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var doc = JsonSerializer.Deserialize<JsonElement>(bytes);

        if (!doc.TryGetProperty("role", out var roleElement))
            return new List<string?>();

        return roleElement.ValueKind == JsonValueKind.Array
            ? roleElement.EnumerateArray().Select(r => r.GetString()).ToList()
            : new List<string?> { roleElement.GetString() };
    }

    private async Task<(Guid TenantId, Guid UserId)> SeedTenantWithUserAsync(string tenantName, string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = tenantName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (tenantId, userId);
    }

    // ── AC1: POST designates user as Tenant Admin ─────────────────────────────

    [Fact]
    public async Task Post_DesignateAdmin_Returns200WithIsTenantAdminTrue()
    {
        var (tenantId, userId) = await SeedTenantWithUserAsync("Tenant-Designate-1", "user-d1@test.com");
        var client = await AuthClientAsync();

        var response = await client.PostAsync(
            $"/api/internal/tenants/{tenantId}/admins/{userId}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isTenantAdmin").GetBoolean());
        Assert.Equal(userId.ToString(), body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Post_NonExistentUser_Returns404()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("Tenant-Designate-2", "user-d2@test.com");
        var client = await AuthClientAsync();

        var response = await client.PostAsync(
            $"/api/internal/tenants/{tenantId}/admins/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CrossTenantUser_Returns404_NotForbidden()
    {
        var (tenantA, _) = await SeedTenantWithUserAsync("Tenant-A-Isolation", "user-a@test.com");
        var (_, userBId) = await SeedTenantWithUserAsync("Tenant-B-Isolation", "user-b@test.com");
        var client = await AuthClientAsync();

        // Try to designate Tenant B's user under Tenant A — must return 404, not 403
        var response = await client.PostAsync(
            $"/api/internal/tenants/{tenantA}/admins/{userBId}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Idempotent_DoubleDesignationBothSucceed()
    {
        var (tenantId, userId) = await SeedTenantWithUserAsync("Tenant-Idempotent", "user-idem@test.com");
        var client = await AuthClientAsync();

        var first = await client.PostAsync(
            $"/api/internal/tenants/{tenantId}/admins/{userId}", null);
        var second = await client.PostAsync(
            $"/api/internal/tenants/{tenantId}/admins/{userId}", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // ── AC2: DELETE removes Tenant Admin role ────────────────────────────────

    [Fact]
    public async Task Delete_RemoveAdmin_Returns204()
    {
        var (tenantId, user1Id) = await SeedTenantWithUserAsync("Tenant-Remove-1", "user-r1@test.com");
        // Seed a second user so we don't hit the last-admin guard
        var user2Id = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = user2Id,
                TenantId = tenantId,
                Email = "user-r2@test.com",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = await AuthClientAsync();
        // Designate both, then remove one
        await client.PostAsync($"/api/internal/tenants/{tenantId}/admins/{user1Id}", null);
        await client.PostAsync($"/api/internal/tenants/{tenantId}/admins/{user2Id}", null);

        var response = await client.DeleteAsync(
            $"/api/internal/tenants/{tenantId}/admins/{user1Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentUser_Returns404()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("Tenant-Remove-404", "user-r404@test.com");
        var client = await AuthClientAsync();

        var response = await client.DeleteAsync(
            $"/api/internal/tenants/{tenantId}/admins/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_LastTenantAdmin_Returns409WithLastTenantAdminError()
    {
        var (tenantId, userId) = await SeedTenantWithUserAsync("Tenant-LastAdmin", "user-last@test.com");
        var client = await AuthClientAsync();

        await client.PostAsync($"/api/internal/tenants/{tenantId}/admins/{userId}", null);

        var response = await client.DeleteAsync(
            $"/api/internal/tenants/{tenantId}/admins/{userId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("last_tenant_admin", body.GetProperty("error").GetString());
    }

    // ── AC3: TenantAdmin role appears in JWT ──────────────────────────────────

    // Each test seeds a fresh TOTP-enrolled user to avoid TOTP time-step replay conflicts.
    // The TOTP replay guard prevents issuing two tokens for the same user within 30 seconds,
    // so each assertion (before/after) uses a separate freshly-seeded user.

    [Fact]
    public async Task NonTenantAdmin_JwtHasNoTenantAdminRole()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("Tenant-JwtRole-Before", "admin-jb@test.com");
        var (_, totpSecret, password) = await SeedTotpEnrolledUserAsync(tenantId, "subject-jb@test.com");

        var token = await IssueTwoStepTokenAsync("subject-jb@test.com", password, totpSecret);
        var roles = GetRolesFromJwt(token);

        Assert.DoesNotContain("TenantAdmin", roles);
    }

    [Fact]
    public async Task DesignatedTenantAdmin_JwtContainsTenantAdminRole()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("Tenant-JwtRole-After", "admin-ja@test.com");
        var (userId, totpSecret, password) = await SeedTotpEnrolledUserAsync(tenantId, "subject-ja@test.com");

        var adminClient = await AuthClientAsync();
        var designateResp = await adminClient.PostAsync(
            $"/api/internal/tenants/{tenantId}/admins/{userId}", null);
        Assert.Equal(HttpStatusCode.OK, designateResp.StatusCode);

        var token = await IssueTwoStepTokenAsync("subject-ja@test.com", password, totpSecret);
        var roles = GetRolesFromJwt(token);

        Assert.Contains("TenantAdmin", roles);
    }

    [Fact]
    public async Task RemovedTenantAdmin_JwtHasNoTenantAdminRole()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("Tenant-JwtRole-Removed", "admin-jr@test.com");
        var (userId, totpSecret, password) = await SeedTotpEnrolledUserAsync(tenantId, "subject-jr@test.com");

        // Seed a second admin to allow removal without hitting last-admin guard
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = "guard-jr@test.com",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var adminClient = await AuthClientAsync();
        // Designate both; then remove the subject
        await adminClient.PostAsync($"/api/internal/tenants/{tenantId}/admins/{userId}", null);
        // Designate guard user — need guard userId
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var guard = await db.Users.IgnoreQueryFilters()
                .FirstAsync(u => u.TenantId == tenantId && u.Email == "guard-jr@test.com");
            await adminClient.PostAsync(
                $"/api/internal/tenants/{tenantId}/admins/{guard.Id}", null);
        }

        var removeResp = await adminClient.DeleteAsync(
            $"/api/internal/tenants/{tenantId}/admins/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResp.StatusCode);

        // Subject is no longer admin — fresh JWT should have no TenantAdmin role
        var token = await IssueTwoStepTokenAsync("subject-jr@test.com", password, totpSecret);
        var roles = GetRolesFromJwt(token);

        Assert.DoesNotContain("TenantAdmin", roles);
    }
}
