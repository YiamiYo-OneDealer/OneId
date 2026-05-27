using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Users.Queries;

public sealed record ListUsersRequest(int Page, int PageSize, bool IncludeInactive);

public sealed class ListUsersHandler(AppDbContext db, ITenantContext tenantContext)
{
    public async Task<PagedResponse<UserDto>> HandleAsync(ListUsersRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Must IgnoreQueryFilters: global filter excludes DeletedAt users; we need IncludeInactive support
        var query = db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantContext.TenantId);

        if (!request.IncludeInactive)
            query = query.Where(u => !u.DeletedAt.HasValue);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.TenantId,
                !u.DeletedAt.HasValue,
                u.IsTenantAdmin,
                u.CreatedAt,
                u.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResponse<UserDto>(items, page, pageSize, totalCount);
    }
}
