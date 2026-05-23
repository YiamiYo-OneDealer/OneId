using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using Serilog.Core;
using Serilog.Events;

namespace OneId.Server.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public static IServiceCollection AddSerilogEnrichers(this IServiceCollection services)
    {
        services.AddSingleton<ILogEventEnricher, EventTypeEnricher>();
        services.AddSingleton<ILogEventEnricher, TraceIdEnricher>();
        services.AddSingleton<ILogEventEnricher, TenantIdEnricher>();
        services.AddSingleton<ILogEventEnricher, UserIdEnricher>();
        services.AddSingleton<ILogEventEnricher, SensitiveDataRedactionEnricher>();
        return services;
    }
}

public sealed class EventTypeEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var eventType = Fnv1a(logEvent.MessageTemplate.Text);
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("EventType", $"{eventType:X8}"));
    }

    // FNV-1a 32-bit — deterministic across runtimes and deployments, unlike GetHashCode()
    private static uint Fnv1a(string text)
    {
        const uint fnvPrime = 16777619;
        const uint fnvOffsetBasis = 2166136261;
        var hash = fnvOffsetBasis;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= fnvPrime;
        }
        return hash;
    }
}

public sealed class TraceIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
    }
}

public sealed class TenantIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.RequestServices is null) return;

        try
        {
            var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
            if (tenantContext?.IsInitialized == true)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("TenantId", tenantContext.TenantId));
            }
        }
        catch
        {
            // Enrichment must never throw — silently skip on any error
        }
    }
}

public sealed class UserIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        try
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return;

            var sub = user.FindFirst("sub")?.Value;
            if (sub is not null)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("UserId", sub));
            }
        }
        catch
        {
            // Enrichment must never throw — silently skip on any error
        }
    }
}

public sealed class SensitiveDataRedactionEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Pwd", "ClientSecret", "client_secret", "Secret", "Token",
        "access_token", "refresh_token", "id_token"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var key in logEvent.Properties.Keys.ToList())
        {
            var value = logEvent.Properties[key];

            if (SensitiveNames.Contains(key))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "[Redacted]"));
                continue;
            }

            // Redact any string value that starts with "Bearer " (Authorization header)
            if (value is ScalarValue { Value: string str } && str.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "[Redacted]"));
            }
        }
    }
}
