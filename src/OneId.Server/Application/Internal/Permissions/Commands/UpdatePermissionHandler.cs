using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.Internal.Permissions.Commands;

public sealed record UpdatePermissionRequest(string PermissionId, string Label, uint Version);

public sealed class UpdatePermissionHandler(InternalAdminContext internalAdminContext, AppDbContext db, IAuditService audit)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<PermissionDto?> HandleAsync(UpdatePermissionRequest request, CancellationToken ct = default)
    {
        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.PermissionId == request.PermissionId, ct);

        if (permission is null)
            return null;

        db.Entry(permission).Property<uint>("xmin").OriginalValue = request.Version;
        permission.Label = request.Label;
        permission.UpdatedAt = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            Guid.Empty,
            "permission.updated",
            "Permission",
            permission.Id,
            JsonSerializer.Serialize(new { permission.PermissionId, label = request.Label })), ct);

        await db.SaveChangesAsync(ct);

        var version = db.Entry(permission).Property<uint>("xmin").CurrentValue;
        return new PermissionDto(
            permission.Id, permission.PermissionId, permission.Label,
            permission.Status.ToString(), permission.CreatedAt, permission.UpdatedAt, version);
    }
}
