using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace OneId.Server.Application.Internal.Commands;

public sealed record CreateTenantRequest(string Name);

public sealed class CreateTenantHandler(InternalAdminContext internalAdminContext, AppDbContext db, IAuditService auditService)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<TenantDto> HandleAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.Tenants.Add(tenant);

        await auditService.AppendAsync(new AuditLogEntry(
            tenant.Id,
            "tenant.created",
            "Tenant",
            tenant.Id,
            JsonSerializer.Serialize(new { name = tenant.Name })), ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new NameTakenException();
        }

        var version = db.Entry(tenant).Property<uint>("xmin").CurrentValue;
        return new TenantDto(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt, tenant.UpdatedAt, version);
    }
}
