using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Groups.Queries;

public sealed class GetGroupHandler(AppDbContext db)
{
    public async Task<GroupDto?> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.Groups
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.CreatedAt,
                g.UpdatedAt,
                Version = EF.Property<uint>(g, "xmin"),
                Roles = g.GroupRoles.Select(gr => new { gr.Role.Id, gr.Role.Name }).ToList(),
                RoleSets = g.GroupRoleSets.Select(grs => new { grs.RoleSet.Id, grs.RoleSet.Name }).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (item is null) return null;

        return new GroupDto(
            item.Id,
            item.Name,
            item.Roles.Select(r => new RoleSummaryDto(r.Id, r.Name)).ToList(),
            item.RoleSets.Select(rs => new RoleSetSummaryDto(rs.Id, rs.Name)).ToList(),
            item.CreatedAt,
            item.UpdatedAt,
            item.Version);
    }
}
