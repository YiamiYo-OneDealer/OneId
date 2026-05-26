using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Permissions.Queries;

public sealed class GetPermissionHandler(InternalAdminContext internalAdminContext, AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<PermissionDto?> HandleAsync(string permissionId, CancellationToken ct = default)
    {
        return await db.Permissions
            .Where(p => p.PermissionId == permissionId)
            .Select(p => new PermissionDto(
                p.Id, p.PermissionId, p.Label, p.Status.ToString(),
                p.CreatedAt, p.UpdatedAt,
                EF.Property<uint>(p, "xmin")))
            .FirstOrDefaultAsync(ct);
    }
}
