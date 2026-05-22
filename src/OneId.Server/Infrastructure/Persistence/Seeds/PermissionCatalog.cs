// AR-9: Version-controlled source of truth for Permission definitions.
// Populated in Story 4a.1 when the Permission catalog is built.
// PermissionCatalogSyncTests.cs enforces that every entry here has a corresponding
// od.* constant in Application/Common/Permissions.cs.
namespace OneId.Server.Infrastructure.Persistence.Seeds;

internal static class PermissionCatalog
{
    // TODO Story 4a.1: Add all od.* permission seed records here.
    // Each entry maps a dot-notation permission ID (e.g., "crm.invoice.create") to a
    // display name and description, and is applied via HasData() in the Permission entity configuration.
}
