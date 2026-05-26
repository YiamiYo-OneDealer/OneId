using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Groups.Queries;

public sealed record ListGroupsRequest(int Page, int PageSize);

public sealed class ListGroupsHandler(AppDbContext db)
{
    public async Task<PagedResponse<GroupDto>> HandleAsync(ListGroupsRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.Groups.AsQueryable();

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync(ct);

        var dtos = items.Select(g => new GroupDto(
            g.Id,
            g.Name,
            g.Roles.Select(r => new RoleSummaryDto(r.Id, r.Name)).ToList(),
            g.RoleSets.Select(rs => new RoleSetSummaryDto(rs.Id, rs.Name)).ToList(),
            g.CreatedAt,
            g.UpdatedAt,
            g.Version))
            .ToList();

        return new PagedResponse<GroupDto>(dtos, page, pageSize, totalCount);
    }
}
