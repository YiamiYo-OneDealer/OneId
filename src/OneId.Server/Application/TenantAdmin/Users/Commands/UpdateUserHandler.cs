using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Users.Commands;

public sealed record UpdateUserRequest(Guid Id, string? DisplayName, string? Email);

public sealed class UpdateUserHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<UserDto?> HandleAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, ct);
        if (user is null) return null;

        var changes = new Dictionary<string, object?>();

        if (request.DisplayName is not null && request.DisplayName != user.DisplayName)
        {
            changes["displayName"] = request.DisplayName;
            user.DisplayName = request.DisplayName;
        }
        if (request.Email is not null && request.Email != user.Email)
        {
            changes["email"] = request.Email;
            user.Email = request.Email;
        }

        if (changes.Count > 0)
        {
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await audit.AppendAsync(new AuditLogEntry(
                tenantContext.TenantId, "user.updated", "User", user.Id,
                JsonSerializer.Serialize(changes)), ct);
            await db.SaveChangesAsync(ct);
        }

        return new UserDto(user.Id, user.Email, user.DisplayName, user.TenantId,
            true, user.IsTenantAdmin, user.CreatedAt, user.UpdatedAt);
    }
}
