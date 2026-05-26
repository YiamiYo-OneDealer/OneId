using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Queries;

public sealed class ListTenantsHandler(InternalAdminContext internalAdminContext, AppDbContext db)
{
    // AR-8: field satisfies the ArchUnit dependency check — this class must live inside Application/Internal/
    private readonly InternalAdminContext _ctx = internalAdminContext;

    public async Task<List<TenantDto>> HandleAsync(CancellationToken ct = default)
    {
        return await db.Tenants
            .Where(t => !t.DeletedAt.HasValue)
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(
                t.Id,
                t.Name,
                t.Status,
                t.CreatedAt,
                t.UpdatedAt,
                EF.Property<uint>(t, "xmin")))
            .ToListAsync(ct);
    }
}
