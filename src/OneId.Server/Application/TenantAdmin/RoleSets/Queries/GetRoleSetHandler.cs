using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.RoleSets.Queries;

public sealed class GetRoleSetHandler(AppDbContext db)
{
    public async Task<RoleSetDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var roleSet = await db.RoleSets
            .Include(rs => rs.RoleSetRoles)
            .ThenInclude(rsr => rsr.Role)
            .FirstOrDefaultAsync(rs => rs.Id == id, ct);

        if (roleSet is null) return null;

        var version = db.Entry(roleSet).Property<uint>("xmin").CurrentValue;
        return new RoleSetDto(
            roleSet.Id,
            roleSet.Name,
            roleSet.RoleSetRoles.Select(rsr => new RoleSummaryDto(rsr.Role.Id, rsr.Role.Name)).ToList(),
            roleSet.CreatedAt,
            roleSet.UpdatedAt,
            version);
    }
}
