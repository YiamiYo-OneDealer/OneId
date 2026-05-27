using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Users.Commands;

public sealed class DeleteUserHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: must handle already-deactivated users (idempotent)
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantContext.TenantId, ct);

        if (user is null) return false;

        if (user.DeletedAt.HasValue) return true;  // already inactive — idempotent 204

        user.DeletedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId, "user.deactivated", "User", id), ct);
        await db.SaveChangesAsync(ct);

        return true;
    }
}
