using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed record DesignateTenantAdminRequest(Guid TenantId, Guid UserId);

public sealed class DesignateTenantAdminHandler(
    InternalAdminContext internalAdminContext,
    AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8

    public async Task<UserDto?> HandleAsync(DesignateTenantAdminRequest request, CancellationToken ct = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.Id == request.UserId &&
                u.TenantId == request.TenantId &&
                u.DeletedAt == null, ct);

        if (user is null)
            return null;

        user.IsTenantAdmin = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var version = db.Entry(user).Property<uint>("xmin").CurrentValue;
        return new UserDto(user.Id, user.Email, user.IsTenantAdmin, user.CreatedAt, user.UpdatedAt, version);
    }
}
