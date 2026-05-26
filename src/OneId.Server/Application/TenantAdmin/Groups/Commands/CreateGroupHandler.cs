using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Groups.Commands;

public sealed record CreateGroupRequest(string Name, IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> RoleSetIds);

public sealed class CreateGroupHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<GroupDto> HandleAsync(CreateGroupRequest request, CancellationToken ct = default)
    {
        var validRoles = await ValidateRoleIdsAsync(request.RoleIds, db, ct);
        var validRoleSets = await ValidateRoleSetIdsAsync(request.RoleSetIds, db, ct);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            GroupRoles = validRoles.Select(r => new GroupRole { RoleId = r.Id }).ToList(),
            GroupRoleSets = validRoleSets.Select(rs => new GroupRoleSet { RoleSetId = rs.Id }).ToList(),
        };

        db.Groups.Add(group);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "group.created",
            "Group",
            group.Id,
            JsonSerializer.Serialize(new { group.Name, RoleIds = request.RoleIds, RoleSetIds = request.RoleSetIds })), ct);
        await db.SaveChangesAsync(ct);

        var version = db.Entry(group).Property<uint>("xmin").CurrentValue;
        return new GroupDto(
            group.Id,
            group.Name,
            validRoles.Select(r => new RoleSummaryDto(r.Id, r.Name)).ToList(),
            validRoleSets.Select(rs => new RoleSetSummaryDto(rs.Id, rs.Name)).ToList(),
            group.CreatedAt,
            group.UpdatedAt,
            version);
    }

    internal static async Task<List<Domain.Entities.Role>> ValidateRoleIdsAsync(
        IReadOnlyList<Guid> roleIds, AppDbContext db, CancellationToken ct)
    {
        var requested = roleIds.Distinct().ToList();
        var valid = await db.Roles.Where(r => requested.Contains(r.Id)).ToListAsync(ct);
        var invalid = requested.Except(valid.Select(r => r.Id)).ToList();
        if (invalid.Count > 0) throw new InvalidRoleIdsException(invalid);
        return valid;
    }

    internal static async Task<List<Domain.Entities.RoleSet>> ValidateRoleSetIdsAsync(
        IReadOnlyList<Guid> roleSetIds, AppDbContext db, CancellationToken ct)
    {
        var requested = roleSetIds.Distinct().ToList();
        var valid = await db.RoleSets.Where(rs => requested.Contains(rs.Id)).ToListAsync(ct);
        var invalid = requested.Except(valid.Select(rs => rs.Id)).ToList();
        if (invalid.Count > 0) throw new InvalidRoleSetIdsException(invalid);
        return valid;
    }
}

public sealed class InvalidRoleIdsException(IReadOnlyList<Guid> invalidIds)
    : Exception($"Invalid or cross-tenant role IDs: {string.Join(", ", invalidIds)}")
{
    public IReadOnlyList<Guid> InvalidIds { get; } = invalidIds;
}

public sealed class InvalidRoleSetIdsException(IReadOnlyList<Guid> invalidIds)
    : Exception($"Invalid or cross-tenant role set IDs: {string.Join(", ", invalidIds)}")
{
    public IReadOnlyList<Guid> InvalidIds { get; } = invalidIds;
}
