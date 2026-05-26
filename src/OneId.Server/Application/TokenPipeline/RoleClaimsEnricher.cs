using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OneId.Server.Infrastructure.Persistence;
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

// Epic 2 stub extended in Story 3.4: adds TenantAdmin role when User.IsTenantAdmin == true.
// Epic 4a: add fine-grained role claims from UserRoles/Groups here.
public sealed class RoleClaimsEnricher(AppDbContext db) : ITokenClaimsEnricher
{
    public async Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == context.UserId && u.DeletedAt == null, ct);

        if (user?.IsTenantAdmin == true)
        {
            // Use OpenIddict's Claims.Role ("role") — matches the roleType set on the ClaimsIdentity.
            identity.AddClaim(new Claim(
                OpenIddictConstants.Claims.Role,
                "TenantAdmin",
                ClaimValueTypes.String,
                "OpenIddict"));
        }

        if (user?.IsInternalAdmin == true)
        {
            identity.AddClaim(new Claim(
                OpenIddictConstants.Claims.Role,
                "InternalAdmin",
                ClaimValueTypes.String,
                "OpenIddict"));
        }
    }
}
