using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Domain.Services;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Dimensions;

// Called during introspection where ITenantContext is not initialized.
// Uses explicit tenantId parameter and IgnoreQueryFilters() for isolation.
public sealed class DimensionEvaluator(AppDbContext db, ICacheService cache) : IDimensionEvaluator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> EvaluateAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"dimensions:{userId}:{tenantId}";
        var cached = cache.Get<Dictionary<string, IReadOnlyList<string>>>(cacheKey);
        if (cached is not null)
            return cached;

        var assignments = await db.UserDimensionAssignments
            .IgnoreQueryFilters()
            .Include(a => a.DimensionValue)
            .Where(a => a.UserId == userId && a.DimensionValue.TenantId == tenantId)
            .Select(a => new { a.DimensionValue.Axis, a.DimensionValue.Value })
            .ToListAsync(ct);

        var result = Enum.GetValues<DimensionAxis>()
            .ToDictionary(
                axis => axis.ToString(),
                axis => (IReadOnlyList<string>)assignments
                    .Where(a => a.Axis == axis)
                    .Select(a => a.Value)
                    .ToList());

        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }
}
