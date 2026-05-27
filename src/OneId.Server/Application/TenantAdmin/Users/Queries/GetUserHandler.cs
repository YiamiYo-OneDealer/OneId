using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Users.Queries;

public sealed class GetUserHandler(AppDbContext db, ITenantContext tenantContext)
{
    public async Task<UserDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: must return deactivated users (isActive=false), not 404
        return await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == id && u.TenantId == tenantContext.TenantId)
            .Select(u => new UserDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.TenantId,
                !u.DeletedAt.HasValue,
                u.IsTenantAdmin,
                u.CreatedAt,
                u.UpdatedAt))
            .FirstOrDefaultAsync(ct);
    }
}
