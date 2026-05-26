using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.Internal.Permissions.Commands;

public sealed record CreatePermissionRequest(string PermissionId, string Label);

public sealed class CreatePermissionHandler(InternalAdminContext internalAdminContext, AppDbContext db, IAuditService audit)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<PermissionDto> HandleAsync(CreatePermissionRequest request, CancellationToken ct = default)
    {
        if (await db.Permissions.AnyAsync(p => p.PermissionId == request.PermissionId, ct))
            throw new PermissionIdTakenException(request.PermissionId);

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            PermissionId = request.PermissionId,
            Label = request.Label,
            Status = PermissionStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.Permissions.Add(permission);

        await audit.AppendAsync(new AuditLogEntry(
            Guid.Empty,
            "permission.created",
            "Permission",
            permission.Id,
            JsonSerializer.Serialize(new { permission.PermissionId, permission.Label })), ct);

        await db.SaveChangesAsync(ct);

        var version = db.Entry(permission).Property<uint>("xmin").CurrentValue;
        return new PermissionDto(
            permission.Id, permission.PermissionId, permission.Label,
            permission.Status.ToString(), permission.CreatedAt, permission.UpdatedAt, version);
    }
}

public sealed class PermissionIdTakenException(string permissionId)
    : Exception($"Permission ID '{permissionId}' is already taken.");
