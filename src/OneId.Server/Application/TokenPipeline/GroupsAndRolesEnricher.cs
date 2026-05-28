using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public sealed class GroupsAndRolesEnricher(AppDbContext db) : ITokenClaimsEnricher
{
    public async Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        var groupNames = await db.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == context.UserId && ug.Group.TenantId == context.TenantId)
            .Select(ug => ug.Group.Name)
            .ToListAsync(ct);

        foreach (var name in groupNames)
            identity.AddClaim(new Claim("groups", name, ClaimValueTypes.String, "OpenIddict"));

        var roleNames = await db.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == context.UserId && ug.Group.TenantId == context.TenantId)
            .SelectMany(ug => ug.Group.GroupRoles.Select(gr => gr.Role.Name))
            .Distinct()
            .ToListAsync(ct);

        foreach (var name in roleNames)
            identity.AddClaim(new Claim("od_roles", name, ClaimValueTypes.String, "OpenIddict"));
    }
}
