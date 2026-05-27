using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.UserOverrides.Commands;

public sealed class DeleteUserOverrideHandler(
    AppDbContext db,
    ITenantContext tenantContext,
    IAuditService audit)
{
    public async Task<bool> HandleAsync(Guid userId, Guid overrideId, CancellationToken ct = default)
    {
        var entity = await db.UserPermissionOverrides
            .FirstOrDefaultAsync(o => o.Id == overrideId && o.UserId == userId, ct);
        if (entity is null) return false;

        db.UserPermissionOverrides.Remove(entity);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId, "user_override.deleted", "UserPermissionOverride", entity.Id), ct);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
