using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Groups.Commands;

public sealed class DeleteGroupHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return false;

        db.Groups.Remove(group);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "group.deleted",
            "Group",
            id), ct);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
