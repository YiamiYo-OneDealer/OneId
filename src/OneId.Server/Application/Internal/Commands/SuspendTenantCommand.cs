using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed class SuspendTenantHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db,
    IUserTokenRevoker revoker)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<TenantDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && !t.DeletedAt.HasValue, ct);

        if (tenant is null)
            return null;

        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        var userIds = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == id && u.DeletedAt == null)
            .Select(u => u.Id)
            .ToListAsync(ct);

        await db.SaveChangesAsync(ct);

        foreach (var userId in userIds)
            await revoker.RevokeAllUserTokensAsync(userId, ct);

        var version = db.Entry(tenant).Property<uint>("xmin").CurrentValue;
        return new TenantDto(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt, tenant.UpdatedAt, version);
    }
}
