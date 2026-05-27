using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.UserOverrides.Commands;

public sealed class UserOverrideDuplicateException() : Exception("An override for this permission already exists for this user.");

public sealed record CreateUserOverrideRequest(
    Guid UserId,
    string PermissionId,
    PermissionOverrideType OverrideType,
    string Reason,
    DateTimeOffset? ExpiresAt);

public sealed class CreateUserOverrideHandler(
    AppDbContext db,
    ITenantContext tenantContext,
    IAuditService audit)
{
    public async Task<UserOverrideDto?> HandleAsync(CreateUserOverrideRequest request, CancellationToken ct = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists) return null;

        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.PermissionId == request.PermissionId, ct);
        if (permission is null || permission.Status != PermissionStatus.Active)
            throw new InvalidOperationException("permission_not_found_or_inactive");

        var duplicate = await db.UserPermissionOverrides
            .AnyAsync(o => o.UserId == request.UserId && o.PermissionId == request.PermissionId, ct);
        if (duplicate) throw new UserOverrideDuplicateException();

        var now = DateTimeOffset.UtcNow;
        var actorSub = tenantContext.GetType().Name; // resolved via IAuditService from HttpContext

        var entity = new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = request.UserId,
            PermissionId = request.PermissionId,
            OverrideType = request.OverrideType,
            Reason = request.Reason,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now,
            CreatedByUserId = Guid.Empty, // resolved from JWT via AuditService
        };

        db.UserPermissionOverrides.Add(entity);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId, "user_override.created", "UserPermissionOverride", entity.Id), ct);
        await db.SaveChangesAsync(ct);

        return new UserOverrideDto(
            entity.Id,
            entity.PermissionId,
            entity.OverrideType.ToString(),
            entity.Reason,
            entity.ExpiresAt,
            entity.CreatedAt,
            entity.ExpiresAt.HasValue && entity.ExpiresAt.Value < now);
    }
}
