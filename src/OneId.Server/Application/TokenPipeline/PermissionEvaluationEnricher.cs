using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

// Permissions are intentionally NOT embedded in the JWT access token.
// They are returned exclusively via the /connect/introspect endpoint (IntrospectionEnricher),
// so the token stays small and the authoritative permission check requires a live server round-trip.
public sealed class PermissionEvaluationEnricher : ITokenClaimsEnricher
{
    public Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
        => Task.CompletedTask;
}
