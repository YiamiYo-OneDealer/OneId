using OneId.Server.Domain.Services;
using System.Security.Claims;

namespace OneId.Server.Application.TokenPipeline;

public sealed class PermissionEvaluationEnricher(IPermissionEvaluator evaluator) : ITokenClaimsEnricher
{
    public async Task EnrichAsync(ClaimsIdentity identity, TokenEnrichmentContext context, CancellationToken ct)
    {
        var permissions = await evaluator.EvaluateAsync(context.UserId, context.TenantId, ct);

        foreach (var permissionId in permissions)
        {
            identity.AddClaim(new Claim("permissions", permissionId, ClaimValueTypes.String, "OpenIddict"));
        }
    }
}
