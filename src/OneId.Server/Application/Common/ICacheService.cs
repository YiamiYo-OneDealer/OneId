// AR-10: All cache access must go through this interface.
// Direct IMemoryCache injection is forbidden outside Infrastructure/Caching/ — enforced by InternalBoundaryTests.cs.
// Cache key format: {entity}:{userId}:{tenantId} (e.g., "user:abc123:tenant456")
namespace OneId.Server.Application.Common;

public interface ICacheService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    void Remove(string key);
}
