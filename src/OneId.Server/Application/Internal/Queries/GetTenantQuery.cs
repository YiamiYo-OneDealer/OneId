using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Queries;

public sealed class GetTenantHandler(InternalAdminContext internalAdminContext, AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<TenantDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Tenants
            .Where(t => t.Id == id && !t.DeletedAt.HasValue)
            .Select(t => new TenantDto(
                t.Id,
                t.Name,
                t.Status,
                t.CreatedAt,
                t.UpdatedAt,
                EF.Property<uint>(t, "xmin")))
            .FirstOrDefaultAsync(ct);
    }
}
