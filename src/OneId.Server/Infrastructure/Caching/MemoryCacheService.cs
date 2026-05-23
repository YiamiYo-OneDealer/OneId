using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Caching;

internal sealed class MemoryCacheService(IMemoryCache cache, IHttpContextAccessor httpContextAccessor) : ICacheService
{
    public T? Get<T>(string key)
    {
        cache.TryGetValue(TenantKey(key), out T? value);
        return value;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = expiry.HasValue
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }
            : new MemoryCacheEntryOptions();
        cache.Set(TenantKey(key), value, options);
    }

    public void Remove(string key) => cache.Remove(TenantKey(key));

    // Auto-prefix with current tenant when inside an HTTP request context.
    // Background services without an active HTTP context receive un-prefixed (global) keys.
    private string TenantKey(string key)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.RequestServices is null) return key;

        try
        {
            var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
            if (tenantContext?.IsInitialized == true)
                return $"{tenantContext.TenantId}:{key}";
        }
        catch
        {
            // Disposed scope or DI failure — fall back to un-prefixed key
        }

        return key;
    }
}
