using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Groups.Commands;

public enum RemoveMemberResult { Ok, NotFound }

public sealed class RemoveGroupMemberHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<RemoveMemberResult> HandleAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        var membership = await db.UserGroups
            .FirstOrDefaultAsync(ug => ug.GroupId == groupId && ug.UserId == userId, ct);

        if (membership is null) return RemoveMemberResult.NotFound;

        db.UserGroups.Remove(membership);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "group.member_removed",
            "Group",
            groupId), ct);
        await db.SaveChangesAsync(ct);
        return RemoveMemberResult.Ok;
    }
}
