using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.RoleSets.Commands;

public sealed record CreateRoleSetRequest(string Name, IReadOnlyList<Guid> RoleIds);

public sealed class CreateRoleSetHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<RoleSetDto> HandleAsync(CreateRoleSetRequest request, CancellationToken ct = default)
    {
        var validRoles = await ValidateRoleIdsAsync(request.RoleIds, db, ct);

        var roleSet = new RoleSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RoleSetRoles = validRoles.Select(r => new RoleSetRole { RoleId = r.Id }).ToList(),
        };

        db.RoleSets.Add(roleSet);
        await db.SaveChangesAsync(ct);

        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role_set.created",
            "RoleSet",
            roleSet.Id,
            JsonSerializer.Serialize(new { roleSet.Name, RoleIds = validRoles.Select(r => r.Id) })), ct);

        var version = db.Entry(roleSet).Property<uint>("xmin").CurrentValue;
        return new RoleSetDto(
            roleSet.Id,
            roleSet.Name,
            validRoles.Select(r => new RoleSummaryDto(r.Id, r.Name)).ToList(),
            roleSet.CreatedAt,
            roleSet.UpdatedAt,
            version);
    }

    internal static async Task<List<Domain.Entities.Role>> ValidateRoleIdsAsync(
        IReadOnlyList<Guid> roleIds,
        AppDbContext db,
        CancellationToken ct)
    {
        var requested = roleIds.Distinct().ToList();
        // Global query filter on Roles already scopes to current tenant.
        // Any cross-tenant role ID will not appear in results and is caught as invalid.
        var valid = await db.Roles
            .Where(r => requested.Contains(r.Id))
            .ToListAsync(ct);

        var invalid = requested.Except(valid.Select(r => r.Id)).ToList();
        if (invalid.Count > 0)
            throw new InvalidRoleIdsException(invalid);

        return valid;
    }
}

public sealed class InvalidRoleIdsException(IReadOnlyList<Guid> invalidIds)
    : Exception($"Invalid or cross-tenant role IDs: {string.Join(", ", invalidIds)}")
{
    public IReadOnlyList<Guid> InvalidIds { get; } = invalidIds;
}
