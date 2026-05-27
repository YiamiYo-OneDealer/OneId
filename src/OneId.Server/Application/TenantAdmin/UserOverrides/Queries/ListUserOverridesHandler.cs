using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.UserOverrides.Queries;

public sealed class ListUserOverridesHandler(AppDbContext db)
{
    public async Task<IEnumerable<UserOverrideDto>> HandleAsync(Guid userId, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) return null!;

        var now = DateTimeOffset.UtcNow;

        return await db.UserPermissionOverrides
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new UserOverrideDto(
                o.Id,
                o.PermissionId,
                o.OverrideType.ToString(),
                o.Reason,
                o.ExpiresAt,
                o.CreatedAt,
                o.ExpiresAt.HasValue && o.ExpiresAt.Value < now))
            .ToListAsync(ct);
    }
}
