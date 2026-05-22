// Wired in Story 1.7a — see architecture.md (AR-10)
// All cache access must go through this interface. Direct IMemoryCache injection is forbidden outside Infrastructure/Caching/.
namespace OneId.Server.Application.Common;

public interface ICacheService
{
    // TODO Story 1.7a: Add Get, Set, Remove methods
    // Cache key format: {entity}:{userId}:{tenantId}
}
