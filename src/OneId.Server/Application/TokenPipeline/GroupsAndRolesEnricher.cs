using Microsoft.EntityFrameworkCore;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public sealed class GroupsAndRolesEnricher(AppDbContext db) : ITokenClaimsEnricher
{
    public async Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        // Remove stale claims first — on refresh the identity is seeded from the old token's claims,
        // so AddClaim would otherwise duplicate these on every rotation.
        foreach (var c in identity.Claims.Where(c => c.Type is "groups" or "od_roles").ToList())
            identity.RemoveClaim(c);

        var groupNames = await db.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == context.UserId && ug.Group.TenantId == context.TenantId)
            .Select(ug => ug.Group.Name)
            .ToListAsync(ct);

        foreach (var name in groupNames)
            identity.AddClaim(new Claim("groups", name, ClaimValueTypes.String, "OpenIddict"));

        // Include roles from both direct GroupRoles and GroupRoleSet → RoleSet → Roles paths.
        var roleNames = await db.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == context.UserId && ug.Group.TenantId == context.TenantId)
            .SelectMany(ug =>
                ug.Group.GroupRoles.Select(gr => gr.Role.Name)
                .Concat(ug.Group.GroupRoleSets.SelectMany(grs => grs.RoleSet.RoleSetRoles.Select(rsr => rsr.Role.Name))))
            .Distinct()
            .ToListAsync(ct);

        foreach (var name in roleNames)
            identity.AddClaim(new Claim("od_roles", name, ClaimValueTypes.String, "OpenIddict"));
    }
}
