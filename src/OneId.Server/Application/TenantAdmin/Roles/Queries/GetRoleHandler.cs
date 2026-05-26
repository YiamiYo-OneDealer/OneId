using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Roles.Queries;

public sealed class GetRoleHandler(AppDbContext db)
{
    public async Task<RoleDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        var version = db.Entry(role).Property<uint>("xmin").CurrentValue;
        return new RoleDto(
            role.Id,
            role.Name,
            role.RolePermissions.Select(rp => rp.Permission.PermissionId).ToList(),
            role.CreatedAt,
            role.UpdatedAt,
            version);
    }
}
