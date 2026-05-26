using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Groups.Commands;

public enum AddMemberResult { Ok, GroupNotFound, UserNotFound }

public sealed record AddGroupMemberRequest(Guid GroupId, Guid UserId);

public sealed class AddGroupMemberHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<AddMemberResult> HandleAsync(AddGroupMemberRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == request.GroupId, ct);
        if (group is null) return AddMemberResult.GroupNotFound;

        // Global query filter on Users scopes to current tenant; cross-tenant userId won't be found
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null) return AddMemberResult.UserNotFound;

        // Idempotency: if already a member, return Ok without inserting a duplicate
        var existing = await db.UserGroups
            .FirstOrDefaultAsync(ug => ug.GroupId == request.GroupId && ug.UserId == request.UserId, ct);
        if (existing is not null) return AddMemberResult.Ok;

        db.UserGroups.Add(new UserGroup { GroupId = request.GroupId, UserId = request.UserId });
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "group.member_added",
            "Group",
            request.GroupId), ct);
        await db.SaveChangesAsync(ct);
        return AddMemberResult.Ok;
    }
}
