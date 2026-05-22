// AR-8: Injectable ONLY within Application/Internal/ — enforced by InternalBoundaryTests.cs.
// Services in Application/Internal/ inject this to signal they need cross-tenant data access.
// All other code (Tenant Admin services, controllers) must NOT take this as a constructor dependency.
namespace OneId.Server.Application.Common;

public sealed class InternalAdminContext
{
}
