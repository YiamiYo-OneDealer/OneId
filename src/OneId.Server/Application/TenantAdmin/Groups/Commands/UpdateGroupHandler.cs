using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Groups.Commands;

public sealed record UpdateGroupRequest(Guid Id, string Name, IReadOnlyList<Guid> RoleIds, IReadOnlyList<Guid> RoleSetIds, uint Version);

public sealed class UpdateGroupHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<GroupDto?> HandleAsync(UpdateGroupRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.GroupRoles)
            .Include(g => g.GroupRoleSets)
            .FirstOrDefaultAsync(g => g.Id == request.Id, ct);

        if (group is null) return null;

        db.Entry(group).Property<uint>("xmin").OriginalValue = request.Version;

        var validRoles = await CreateGroupHandler.ValidateRoleIdsAsync(request.RoleIds, db, ct);
        var validRoleSets = await CreateGroupHandler.ValidateRoleSetIdsAsync(request.RoleSetIds, db, ct);

        db.GroupRoles.RemoveRange(group.GroupRoles);
        db.GroupRoleSets.RemoveRange(group.GroupRoleSets);
        group.GroupRoles = validRoles.Select(r => new GroupRole { GroupId = group.Id, RoleId = r.Id }).ToList();
        group.GroupRoleSets = validRoleSets.Select(rs => new GroupRoleSet { GroupId = group.Id, RoleSetId = rs.Id }).ToList();
        group.Name = request.Name;
        group.UpdatedAt = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "group.updated",
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
}
