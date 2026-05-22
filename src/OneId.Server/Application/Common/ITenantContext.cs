// Wired in Story 1.3a — see architecture.md
// AR-5: ITenantContext MUST be registered before EF Core and OpenIddict
namespace OneId.Server.Application.Common;

public interface ITenantContext
{
    // TODO Story 1.3a: Add TenantId property and guard logic
}
