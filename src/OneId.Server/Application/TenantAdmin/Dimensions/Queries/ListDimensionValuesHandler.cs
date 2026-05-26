using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Queries;

public sealed class ListDimensionValuesHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<DimensionValueDto>> HandleAsync(DimensionAxis axis, CancellationToken ct = default)
    {
        return await db.DimensionValues
            .Where(d => d.Axis == axis && d.IsActive)
            .OrderBy(d => d.Value)
            .Select(d => new DimensionValueDto(
                d.Id,
                d.Axis.ToString(),
                d.Value,
                EF.Property<uint>(d, "xmin")))
            .ToListAsync(ct);
    }
}
