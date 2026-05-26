using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Permissions.Queries;

public sealed record ListPermissionsRequest(string Status = "Active", int Page = 1, int PageSize = 25);

public sealed class ListPermissionsHandler(InternalAdminContext internalAdminContext, AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<PagedResponse<PermissionDto>> HandleAsync(ListPermissionsRequest request, CancellationToken ct = default)
    {
        var query = db.Permissions.AsQueryable();

        query = request.Status switch
        {
            "Active"   => query.Where(p => p.Status == PermissionStatus.Active),
            "Inactive" => query.Where(p => p.Status == PermissionStatus.Inactive),
            _          => query,
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.PermissionId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PermissionDto(
                p.Id, p.PermissionId, p.Label, p.Status.ToString(),
                p.CreatedAt, p.UpdatedAt,
                EF.Property<uint>(p, "xmin")))
            .ToListAsync(ct);

        return new PagedResponse<PermissionDto>(items, request.Page, request.PageSize, totalCount);
    }
}
