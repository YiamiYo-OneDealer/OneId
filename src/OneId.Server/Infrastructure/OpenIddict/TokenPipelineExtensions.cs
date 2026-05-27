using OneId.Server.Application.Permissions;
using OneId.Server.Application.TokenPipeline;
using OneId.Server.Domain.Services;

namespace OneId.Server.Infrastructure.OpenIddict;

public static class TokenPipelineExtensions
{
    // Registration order = execution order: IEnumerable<ITokenClaimsEnricher> preserves DI insertion order.
    public static IServiceCollection AddTokenPipeline(this IServiceCollection services)
    {
        services.AddScoped<ITokenClaimsEnricher, RoleClaimsEnricher>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<ITokenClaimsEnricher, PermissionEvaluationEnricher>();
        return services;
    }
}
