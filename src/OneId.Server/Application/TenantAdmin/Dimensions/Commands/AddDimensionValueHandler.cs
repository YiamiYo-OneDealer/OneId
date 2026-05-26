using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.TenantAdmin.Dimensions.Commands;

public sealed class AddDimensionValueHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<DimensionValueDto> HandleAsync(DimensionAxis axis, string value, CancellationToken ct = default)
    {
        var exists = await db.DimensionValues
            .IgnoreQueryFilters()
            .AnyAsync(d => d.TenantId == tenantContext.TenantId && d.Axis == axis && d.Value == value, ct);
        if (exists) throw new DuplicateDimensionValueException();

        var entity = new DimensionValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Axis = axis,
            Value = value,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.DimensionValues.Add(entity);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "dimension_value.created",
            "DimensionValue",
            entity.Id,
            JsonSerializer.Serialize(new { entity.Axis, entity.Value })), ct);
        await db.SaveChangesAsync(ct);

        var version = db.Entry(entity).Property<uint>("xmin").CurrentValue;
        return new DimensionValueDto(entity.Id, entity.Axis.ToString(), entity.Value, version);
    }
}

public sealed class DuplicateDimensionValueException()
    : Exception("Dimension value already exists for this axis in this tenant.");
