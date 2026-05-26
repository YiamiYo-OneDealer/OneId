using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;
using OneId.Server.IntegrationTests.Helpers;
using System.Reflection;
using Xunit;

namespace OneId.Server.IntegrationTests;

[Collection("IntegrationTests")]
public class PermissionCatalogSyncTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task AllPermissionConstants_HaveCorrespondingSeedRow()
    {
        // Reflect on Permissions static class to get all const string values
        var constants = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        Assert.NotEmpty(constants); // sanity: class must have at least one constant

        // Query DB — Permissions are global, no tenant context needed
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seededIds = await db.Permissions
            .Select(p => p.PermissionId)
            .ToHashSetAsync();

        var missing = constants.Except(seededIds).OrderBy(x => x).ToList();
        Assert.Empty(missing);
    }
}
