namespace OneId.Server.Application.Common;

public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public Guid TenantId =>
        _tenantId ?? throw new InvalidOperationException(
            "Tenant context not initialized — check middleware registration order in Program.cs");

    public bool IsInitialized => _tenantId.HasValue;

    internal void Initialize(Guid tenantId)
    {
        if (_tenantId.HasValue)
            throw new InvalidOperationException("TenantContext has already been initialized for this scope.");
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be Guid.Empty.", nameof(tenantId));
        _tenantId = tenantId;
    }
}
