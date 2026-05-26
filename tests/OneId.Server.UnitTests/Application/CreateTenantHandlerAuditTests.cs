using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Application.Internal.Commands;
using OneId.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OneId.Server.UnitTests.Application;

public class CreateTenantHandlerAuditTests
{
    private sealed class FakeAuditService : IAuditService
    {
        public List<AuditLogEntry> Appended { get; } = [];

        public Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            Appended.Add(entry);
            return Task.CompletedTask;
        }

        public Task<PagedResponse<AuditLogDto>> QueryAsync(int page, int pageSize, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private static AppDbContext BuildInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantCtx = new TenantContext();
        return new AppDbContext(opts, tenantCtx);
    }

    [Fact]
    public async Task HandleAsync_CallsAppendAsync_WithTenantCreatedAction()
    {
        var db = BuildInMemoryDb();
        var audit = new FakeAuditService();
        var handler = new CreateTenantHandler(new InternalAdminContext(), db, audit);

        await handler.HandleAsync(new CreateTenantRequest("Acme Corp"));

        Assert.Single(audit.Appended);
        Assert.Equal("tenant.created", audit.Appended[0].Action);
        Assert.Equal("Tenant", audit.Appended[0].EntityType);
    }

    [Fact]
    public async Task HandleAsync_AuditEntry_HasCorrectEntityId()
    {
        var db = BuildInMemoryDb();
        var audit = new FakeAuditService();
        var handler = new CreateTenantHandler(new InternalAdminContext(), db, audit);

        await handler.HandleAsync(new CreateTenantRequest("Beta Corp"));

        var tenant = await db.Tenants.IgnoreQueryFilters().FirstAsync();
        Assert.Equal(tenant.Id, audit.Appended[0].EntityId);
        Assert.Equal(tenant.Id, audit.Appended[0].TenantId);
    }
}
