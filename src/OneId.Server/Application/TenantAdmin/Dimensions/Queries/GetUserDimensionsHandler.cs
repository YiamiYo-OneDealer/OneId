using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Queries;

public sealed class GetUserDimensionsHandler(AppDbContext db)
{
    public async Task<UserDimensionsGroupedDto> HandleAsync(Guid userId, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) throw new UserDimensionUserNotFoundException();

        var assignments = await db.UserDimensionAssignments
            .Include(a => a.DimensionValue)
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

        var grouped = assignments
            .GroupBy(a => a.DimensionValue.Axis)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<UserDimensionValueDto>)g
                    .Select(a => new UserDimensionValueDto(a.DimensionValueId, a.DimensionValue.Value))
                    .ToList());

        return new UserDimensionsGroupedDto(
            Company:       grouped.GetValueOrDefault(DimensionAxis.Company, []),
            Location:      grouped.GetValueOrDefault(DimensionAxis.Location, []),
            Branch:        grouped.GetValueOrDefault(DimensionAxis.Branch, []),
            Make:          grouped.GetValueOrDefault(DimensionAxis.Make, []),
            MarketSegment: grouped.GetValueOrDefault(DimensionAxis.MarketSegment, []));
    }
}

public sealed class UserDimensionUserNotFoundException()
    : Exception("User not found in this tenant.");
