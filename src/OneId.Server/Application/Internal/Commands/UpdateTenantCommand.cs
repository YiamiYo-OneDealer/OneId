using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Internal.Commands;

public sealed record UpdateTenantRequest(string Name, uint Version);

public sealed class UpdateTenantHandler(InternalAdminContext internalAdminContext, AppDbContext db)
{
    private readonly InternalAdminContext _ctx = internalAdminContext; // AR-8 boundary marker

    public async Task<TenantDto?> HandleAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && !t.DeletedAt.HasValue, ct);

        if (tenant is null)
            return null;

        // AR-14: Set the expected xmin so EF Core uses it in the UPDATE WHERE clause.
        db.Entry(tenant).Property<uint>("xmin").OriginalValue = request.Version;

        tenant.Name = request.Name;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new NameTakenException();
        }
        // DbUpdateConcurrencyException propagates to the controller (stale version → 409)

        var version = db.Entry(tenant).Property<uint>("xmin").CurrentValue;
        return new TenantDto(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt, tenant.UpdatedAt, version);
    }
}
