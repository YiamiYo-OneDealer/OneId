using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.OpenIddict;

public static class RevocationExtensions
{
    public static IServiceCollection AddRevocationHandler(this IServiceCollection services)
    {
        services.AddScoped<IUserTokenRevoker, RevocationHandler>();
        return services;
    }
}
