using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Commands;

public sealed class DeactivateDimensionValueHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<bool> HandleAsync(Guid id, DimensionAxis axis, CancellationToken ct = default)
    {
        var entity = await db.DimensionValues.FirstOrDefaultAsync(d => d.Id == id && d.Axis == axis, ct);
        if (entity is null) return false;

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "dimension_value.deactivated",
            "DimensionValue",
            entity.Id,
            JsonSerializer.Serialize(new { entity.Axis, entity.Value })), ct);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
