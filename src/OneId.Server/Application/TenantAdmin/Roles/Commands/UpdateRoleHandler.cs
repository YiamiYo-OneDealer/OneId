using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Roles.Commands;

public sealed record UpdateRoleRequest(Guid Id, string Name, IReadOnlyList<string> PermissionIds, uint Version);

public sealed class UpdateRoleHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<RoleDto?> HandleAsync(UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);

        if (role is null) return null;

        db.Entry(role).Property<uint>("xmin").OriginalValue = request.Version;

        var validPermissions = await CreateRoleHandler.ValidatePermissionIdsAsync(request.PermissionIds, db, ct);

        db.RolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions = validPermissions
            .Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id })
            .ToList();
        role.Name = request.Name;
        role.UpdatedAt = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role.updated",
            "Role",
            role.Id,
            JsonSerializer.Serialize(new { role.Name, PermissionIds = request.PermissionIds })), ct);
        await db.SaveChangesAsync(ct);

        var version = db.Entry(role).Property<uint>("xmin").CurrentValue;
        return new RoleDto(
            role.Id,
            role.Name,
            validPermissions.Select(p => p.PermissionId).ToList(),
            role.CreatedAt,
            role.UpdatedAt,
            version);
    }
}
