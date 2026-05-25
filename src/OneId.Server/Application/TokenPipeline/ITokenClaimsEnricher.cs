using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public interface ITokenClaimsEnricher
{
    Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct);
}
