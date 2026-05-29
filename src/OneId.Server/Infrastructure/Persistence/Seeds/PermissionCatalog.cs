using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

// AR-9: Version-controlled source of truth for Permission definitions.
// Every entry here must have a matching const in Application/Common/Permissions.cs.
// PermissionCatalogSyncTests.cs asserts the two are in sync.
internal static class PermissionCatalog
{
    // Seeded by SystemSeeder in all environments (including production).
    public static readonly IReadOnlyList<PermissionSeedEntry> OneIdEntries =
    [
        new(Permissions.OneIdTenantsView,    "View Tenants"),
        new(Permissions.OneIdTenantsCreate,  "Create Tenants"),
        new(Permissions.OneIdTenantsUpdate,  "Update Tenants"),
        new(Permissions.OneIdTenantsSuspend, "Suspend Tenants"),

        new(Permissions.OneIdPermissionsView,       "View Permission Catalog"),
        new(Permissions.OneIdPermissionsCreate,     "Create Permissions"),
        new(Permissions.OneIdPermissionsUpdate,     "Update Permissions"),
        new(Permissions.OneIdPermissionsDeactivate, "Deactivate Permissions"),

        new(Permissions.OneIdLicensesView,   "View Licenses"),
        new(Permissions.OneIdLicensesCreate, "Create Licenses"),
        new(Permissions.OneIdLicensesUpdate, "Update Licenses"),

        new(Permissions.OneIdIdpView,      "View IDP Configuration"),
        new(Permissions.OneIdIdpConfigure, "Configure IDP Federation"),

        new(Permissions.OneIdAuditView, "View Platform Audit Log"),
    ];

    // Seeded by DevSeeder in development only — tenant admin and demo business permissions.
    public static readonly IReadOnlyList<PermissionSeedEntry> SeedEntries =
    [
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
