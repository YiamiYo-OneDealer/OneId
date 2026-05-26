using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.RoleSets.Commands;

public sealed record UpdateRoleSetRequest(Guid Id, string Name, IReadOnlyList<Guid> RoleIds, uint Version);

public sealed class UpdateRoleSetHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<RoleSetDto?> HandleAsync(UpdateRoleSetRequest request, CancellationToken ct = default)
    {
        var roleSet = await db.RoleSets
            .Include(rs => rs.RoleSetRoles)
            .FirstOrDefaultAsync(rs => rs.Id == request.Id, ct);

        if (roleSet is null) return null;

        // Set xmin original value for optimistic concurrency check (AR-14)
        db.Entry(roleSet).Property<uint>("xmin").OriginalValue = request.Version;

        var validRoles = await CreateRoleSetHandler.ValidateRoleIdsAsync(request.RoleIds, db, ct);

        // Atomic replace: remove old role references, add new ones
        db.RoleSetRoles.RemoveRange(roleSet.RoleSetRoles);
        roleSet.RoleSetRoles = validRoles
            .Select(r => new RoleSetRole { RoleSetId = roleSet.Id, RoleId = r.Id })
            .ToList();
        roleSet.Name = request.Name;
        roleSet.UpdatedAt = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role_set.updated",
            "RoleSet",
            roleSet.Id,
            JsonSerializer.Serialize(new { roleSet.Name, RoleIds = request.RoleIds })), ct);
        await db.SaveChangesAsync(ct);  // throws DbUpdateConcurrencyException if xmin mismatch

        var version = db.Entry(roleSet).Property<uint>("xmin").CurrentValue;
        return new RoleSetDto(
            roleSet.Id,
            roleSet.Name,
            validRoles.Select(r => new RoleSummaryDto(r.Id, r.Name)).ToList(),
            roleSet.CreatedAt,
            roleSet.UpdatedAt,
            version);
    }
}
