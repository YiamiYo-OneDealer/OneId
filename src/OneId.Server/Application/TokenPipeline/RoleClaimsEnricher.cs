using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

// Epic 2 stub: no role data exists yet (Role entity is created in Epic 4a).
// Registered as the first ITokenClaimsEnricher stage; Epic 4b adds the real DB query.
// With 0 roles added, OpenIddict omits the "roles" claim from the JWT entirely.
public sealed class RoleClaimsEnricher : ITokenClaimsEnricher
{
    public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        // Epic 4a: query UserRoles for context.UserId and call identity.AddClaim() per role name.
        return Task.CompletedTask;
    }
}
