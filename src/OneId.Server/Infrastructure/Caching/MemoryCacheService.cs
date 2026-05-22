using Microsoft.Extensions.Caching.Memory;
using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Caching;

internal sealed class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public T? Get<T>(string key)
    {
        cache.TryGetValue(key, out T? value);
        return value;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = expiry.HasValue
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }
            : new MemoryCacheEntryOptions();
        cache.Set(key, value, options);
    }

    public void Remove(string key) => cache.Remove(key);
}
