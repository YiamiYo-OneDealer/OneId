using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Roles.Queries;

public sealed record ListRolesRequest(int Page, int PageSize);

public sealed class ListRolesHandler(AppDbContext db)
{
    public async Task<PagedResponse<RoleDto>> HandleAsync(ListRolesRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.Roles.AsQueryable();

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.CreatedAt,
                r.UpdatedAt,
                Version = EF.Property<uint>(r, "xmin"),
                PermissionIds = r.RolePermissions.Select(rp => rp.Permission.PermissionId).ToList(),
            })
            .ToListAsync(ct);

        var dtos = items.Select(r => new RoleDto(
            r.Id, r.Name,
            r.PermissionIds,
            r.CreatedAt, r.UpdatedAt, r.Version))
            .ToList();

        return new PagedResponse<RoleDto>(dtos, page, pageSize, totalCount);
    }
}
