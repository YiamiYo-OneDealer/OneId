using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed class DeactivateTenantHandler(InternalAdminContext internalAdminContext, AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    /// <returns>true if deactivated; false if not found or already deactivated.</returns>
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && !t.DeletedAt.HasValue, ct);

        if (tenant is null)
            return false;

        tenant.DeletedAt = DateTimeOffset.UtcNow;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
