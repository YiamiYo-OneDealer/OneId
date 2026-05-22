// AR-5: ITenantContext MUST precede OpenIddict and EF Core — see architecture.md
namespace OneId.Server.Application.Common;

public interface ITenantContext
{
    /// <exception cref="InvalidOperationException">Thrown when accessed before TenantContextMiddleware has executed for this request.</exception>
    Guid TenantId { get; }
    bool IsInitialized { get; }
}
