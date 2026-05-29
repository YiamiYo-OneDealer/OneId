using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Groups.Queries;

public sealed class GetUserGroupsHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<GroupDto>> HandleAsync(Guid userId, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) throw new GetUserGroupsUserNotFoundException();

        return await db.Groups
            .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
            .OrderBy(g => g.Name)
            .Select(g => new GroupDto(
                g.Id,
                g.Name,
                g.GroupRoles.Select(gr => new RoleSummaryDto(gr.Role.Id, gr.Role.Name)).ToList(),
                g.GroupRoleSets.Select(grs => new RoleSetSummaryDto(grs.RoleSet.Id, grs.RoleSet.Name)).ToList(),
                g.CreatedAt,
                g.UpdatedAt,
                EF.Property<uint>(g, "xmin")))
            .ToListAsync(ct);
    }
}

public sealed class GetUserGroupsUserNotFoundException()
    : Exception("User not found in this tenant.");
