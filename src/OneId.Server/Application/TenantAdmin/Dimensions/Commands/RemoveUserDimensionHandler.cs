using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Commands;

public sealed class RemoveUserDimensionHandler(AppDbContext db)
{
    public async Task<bool> HandleAsync(Guid userId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await db.UserDimensionAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.UserId == userId, ct);
        if (assignment is null) return false;
        db.UserDimensionAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
