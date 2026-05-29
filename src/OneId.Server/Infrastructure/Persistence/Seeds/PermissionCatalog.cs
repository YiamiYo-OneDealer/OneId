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
    ];

    // Seeded by DevSeeder in development only — demo OneDealer business permissions.
    public static readonly IReadOnlyList<PermissionSeedEntry> SeedEntries =
    [
        new(Permissions.OdBpView,   "Business Partners — View List"),
        new(Permissions.OdBpCreate, "Business Partners — Create"),
        new(Permissions.OdBpEdit,   "Business Partners — Edit"),
        new(Permissions.OdCpView,   "Contact Persons — View List"),

        new(Permissions.OdLeadsView,           "Leads — View List"),
        new(Permissions.OdLeadsCreate,         "Leads — Create / Edit"),
        new(Permissions.OdOpportunitiesView,   "Opportunities — View List"),
        new(Permissions.OdOpportunitiesCreate, "Opportunities — Create / Edit"),

        new(Permissions.OdVehiclesView,   "Vehicles — View List"),
        new(Permissions.OdVehiclesCreate, "Vehicles — Add / Create"),

        new(Permissions.OdAfterSalesJobCardView,   "After Sales — View Job Card List"),
        new(Permissions.OdAfterSalesJobCardCreate, "After Sales — Add Job Card"),

        new(Permissions.OdCalendarView, "Calendar — View"),
    ];
}

internal sealed record PermissionSeedEntry(string PermissionId, string Label);
