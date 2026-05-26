using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Email;

public static class EmailExtensions
{
    public static IServiceCollection AddEmailSender(this IServiceCollection services)
    {
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        return services;
    }
}
