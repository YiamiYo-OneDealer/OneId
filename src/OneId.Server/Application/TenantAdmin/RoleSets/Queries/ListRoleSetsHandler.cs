using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.RoleSets.Queries;

public sealed record ListRoleSetsRequest(int Page, int PageSize);

public sealed class ListRoleSetsHandler(AppDbContext db)
{
    public async Task<PagedResponse<RoleSetDto>> HandleAsync(ListRoleSetsRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.RoleSets.AsQueryable();

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(rs => rs.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(rs => new
            {
                rs.Id,
                rs.Name,
                rs.CreatedAt,
                rs.UpdatedAt,
                Version = EF.Property<uint>(rs, "xmin"),
                Roles = rs.RoleSetRoles.Select(rsr => new { rsr.Role.Id, rsr.Role.Name }).ToList(),
            })
            .ToListAsync(ct);

        var dtos = items.Select(rs => new RoleSetDto(
            rs.Id,
            rs.Name,
            rs.Roles.Select(r => new RoleSummaryDto(r.Id, r.Name)).ToList(),
            rs.CreatedAt,
            rs.UpdatedAt,
            rs.Version))
            .ToList();

        return new PagedResponse<RoleSetDto>(dtos, page, pageSize, totalCount);
    }
}
