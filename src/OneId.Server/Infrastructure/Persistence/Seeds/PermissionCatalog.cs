using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

// AR-9: Version-controlled source of truth for Permission definitions.
// Every entry here must have a matching const in Application/Common/Permissions.cs.
// PermissionCatalogSyncTests.cs asserts the two are in sync.
internal static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionSeedEntry> SeedEntries =
    [
        new(Permissions.AdminTenantsView,    "View Tenants"),
        new(Permissions.AdminTenantsCreate,  "Create Tenants"),
        new(Permissions.AdminTenantsUpdate,  "Update Tenants"),
        new(Permissions.AdminTenantsSuspend, "Suspend Tenants"),

        new(Permissions.AdminPermissionsView,       "View Permission Catalog"),
        new(Permissions.AdminPermissionsCreate,     "Create Permissions"),
        new(Permissions.AdminPermissionsUpdate,     "Update Permissions"),
        new(Permissions.AdminPermissionsDeactivate, "Deactivate Permissions"),

        new(Permissions.AdminLicensesView,   "View Licenses"),
        new(Permissions.AdminLicensesCreate, "Create Licenses"),
        new(Permissions.AdminLicensesUpdate, "Update Licenses"),

        new(Permissions.AdminIdpView,      "View IDP Configuration"),
        new(Permissions.AdminIdpConfigure, "Configure IDP Federation"),

        new(Permissions.AdminUsersView,       "View Users"),
        new(Permissions.AdminUsersCreate,     "Create Users"),
        new(Permissions.AdminUsersUpdate,     "Update Users"),
        new(Permissions.AdminUsersDeactivate, "Deactivate Users"),
        new(Permissions.AdminUsersRevoke,     "Revoke User Tokens"),

        new(Permissions.AdminRolesView,   "View Roles"),
        new(Permissions.AdminRolesCreate, "Create Roles"),
        new(Permissions.AdminRolesUpdate, "Update Roles"),
        new(Permissions.AdminRolesDelete, "Delete Roles"),

        new(Permissions.AdminRoleSetsView,   "View Role Sets"),
        new(Permissions.AdminRoleSetsCreate, "Create Role Sets"),
        new(Permissions.AdminRoleSetsUpdate, "Update Role Sets"),
        new(Permissions.AdminRoleSetsDelete, "Delete Role Sets"),

        new(Permissions.AdminGroupsView,          "View Groups"),
        new(Permissions.AdminGroupsCreate,        "Create Groups"),
        new(Permissions.AdminGroupsUpdate,        "Update Groups"),
        new(Permissions.AdminGroupsDelete,        "Delete Groups"),
        new(Permissions.AdminGroupsMembersManage, "Manage Group Members"),

        new(Permissions.AdminDimensionsView,   "View Dimension Assignments"),
        new(Permissions.AdminDimensionsAssign, "Assign Dimensions to Users"),

        new(Permissions.AdminAuditView, "View Audit Log"),

        new(Permissions.CrmRead,           "CRM — Read"),
        new(Permissions.CrmWrite,          "CRM — Write"),
        new(Permissions.CrmInvoiceCreate,  "CRM — Create Invoice"),
        new(Permissions.CrmInvoiceApprove, "CRM — Approve Invoice"),

        new(Permissions.FinanceRead,    "Finance — Read"),
        new(Permissions.FinanceWrite,   "Finance — Write"),
        new(Permissions.FinanceApprove, "Finance — Approve"),
    ];
}

internal sealed record PermissionSeedEntry(string PermissionId, string Label);
