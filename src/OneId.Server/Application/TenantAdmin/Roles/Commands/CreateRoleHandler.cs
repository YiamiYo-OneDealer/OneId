using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Roles.Commands;

public sealed record CreateRoleRequest(string Name, IReadOnlyList<string> PermissionIds);

public sealed class CreateRoleHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<RoleDto> HandleAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var validPermissions = await ValidatePermissionIdsAsync(request.PermissionIds, db, ct);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RolePermissions = validPermissions
                .Select(p => new RolePermission { PermissionId = p.Id })
                .ToList(),
        };

        db.Roles.Add(role);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role.created",
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

    internal static async Task<List<Permission>> ValidatePermissionIdsAsync(
        IReadOnlyList<string> permissionIds,
        AppDbContext db,
        CancellationToken ct)
    {
        var requested = permissionIds.Distinct().ToList();
        var valid = await db.Permissions
            .Where(p => requested.Contains(p.PermissionId) && p.Status == PermissionStatus.Active)
            .ToListAsync(ct);

        var invalid = requested.Except(valid.Select(p => p.PermissionId)).ToList();
        if (invalid.Count > 0)
            throw new InvalidPermissionIdsException(invalid);

        return valid;
    }
}

public sealed class InvalidPermissionIdsException(IReadOnlyList<string> invalidIds)
    : Exception($"Invalid or inactive permission IDs: {string.Join(", ", invalidIds)}")
{
    public IReadOnlyList<string> InvalidIds { get; } = invalidIds;
}
