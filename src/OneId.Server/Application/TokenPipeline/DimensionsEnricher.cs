using OneId.Server.Domain.Services;
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public sealed class DimensionsEnricher(IDimensionEvaluator evaluator) : ITokenClaimsEnricher
{
    public async Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        // Remove stale dim_* claims — identity is seeded from the old token on refresh.
        foreach (var c in identity.Claims.Where(c => c.Type.StartsWith("dim_")).ToList())
            identity.RemoveClaim(c);

        var dimensions = await evaluator.EvaluateAsync(context.UserId, context.TenantId, ct);

        foreach (var (axis, values) in dimensions)
        {
            foreach (var value in values)
                identity.AddClaim(new Claim($"dim_{axis}", value, ClaimValueTypes.String, "OpenIddict"));
        }
    }
}
