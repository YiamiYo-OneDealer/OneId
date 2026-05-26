using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.Internal.Permissions.Commands;

public sealed class DeactivatePermissionHandler(InternalAdminContext internalAdminContext, AppDbContext db, IAuditService audit)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<bool> HandleAsync(string permissionId, CancellationToken ct = default)
    {
        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.PermissionId == permissionId, ct);

        if (permission is null)
            return false;

        permission.Status = PermissionStatus.Inactive;
        permission.UpdatedAt = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            Guid.Empty,
            "permission.deactivated",
            "Permission",
            permission.Id,
            JsonSerializer.Serialize(new { permission.PermissionId })), ct);

        await db.SaveChangesAsync(ct);
        return true;
    }
}
