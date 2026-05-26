using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Audit;

public sealed class AuditService(
    AppDbContext db,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        if (tenantContext.IsInitialized && entry.TenantId != tenantContext.TenantId)
            throw new InvalidOperationException(
                $"Audit entry TenantId {entry.TenantId} does not match current tenant context {tenantContext.TenantId}.");

        var actorSub = httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
        Guid? actorUserId = Guid.TryParse(actorSub, out var parsed) ? parsed : null;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = entry.TenantId,
            ActorUserId = actorUserId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Payload = entry.Payload,
            Timestamp = DateTimeOffset.UtcNow,
        });

        return Task.CompletedTask;
    }

    public async Task<PagedResponse<AuditLogDto>> QueryAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.AuditLogs.OrderByDescending(a => a.Timestamp);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.TenantId, a.ActorUserId,
                a.Action, a.EntityType, a.EntityId,
                a.Payload, a.Timestamp))
            .ToListAsync(ct);

        return new PagedResponse<AuditLogDto>(items, page, pageSize, totalCount);
    }
}
