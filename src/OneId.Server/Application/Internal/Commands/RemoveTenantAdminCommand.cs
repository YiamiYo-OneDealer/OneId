using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed class RemoveTenantAdminHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<UserDto?> HandleAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.TenantId == tenantId &&
                u.DeletedAt == null, ct);

        if (user is null)
            return null;

        if (user.IsTenantAdmin)
        {
            var otherAdmins = await db.Users
                .IgnoreQueryFilters()
                .CountAsync(u =>
                    u.TenantId == tenantId &&
                    u.IsTenantAdmin &&
                    u.Id != userId &&
                    u.DeletedAt == null, ct);

            if (otherAdmins == 0)
                throw new LastTenantAdminException(tenantId);
        }

        user.IsTenantAdmin = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var version = db.Entry(user).Property<uint>("xmin").CurrentValue;
        return new UserDto(user.Id, user.Email, user.IsTenantAdmin, user.CreatedAt, user.UpdatedAt, version);
    }
}

public sealed class LastTenantAdminException(Guid tenantId)
    : Exception($"Cannot remove the last Tenant Admin from tenant {tenantId}.")
{
    public Guid TenantId { get; } = tenantId;
}
