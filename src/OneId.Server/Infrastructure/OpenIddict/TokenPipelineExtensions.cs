using OneId.Server.Application.TokenPipeline;

namespace OneId.Server.Infrastructure.OpenIddict;

public static class TokenPipelineExtensions
{
    // Registration order = execution order: IEnumerable<ITokenClaimsEnricher> preserves DI insertion order.
    // Epic 4b adds new enrichers here — no other file changes required at the call site.
    public static IServiceCollection AddTokenPipeline(this IServiceCollection services)
    {
        services.AddScoped<ITokenClaimsEnricher, RoleClaimsEnricher>();
        return services;
    }
}
