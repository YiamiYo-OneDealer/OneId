namespace OneId.Server.Application.Common;

public static class Permissions
{
    // OneId platform — Tenant management (FR-12, FR-14)
    public const string OneIdTenantsView    = "oneid.tenants.view";
    public const string OneIdTenantsCreate  = "oneid.tenants.create";
    public const string OneIdTenantsUpdate  = "oneid.tenants.update";
    public const string OneIdTenantsSuspend = "oneid.tenants.suspend";

    // OneId platform — Permission catalog management (FR-6)
    public const string OneIdPermissionsView       = "oneid.permissions.view";
    public const string OneIdPermissionsCreate     = "oneid.permissions.create";
    public const string OneIdPermissionsUpdate     = "oneid.permissions.update";
    public const string OneIdPermissionsDeactivate = "oneid.permissions.deactivate";

    // OneId platform — License management (FR-15, FR-19)
    public const string OneIdLicensesView   = "oneid.licenses.view";
    public const string OneIdLicensesCreate = "oneid.licenses.create";
    public const string OneIdLicensesUpdate = "oneid.licenses.update";

    // OneId platform — IDP federation configuration (FR-16, FR-17)
    public const string OneIdIdpView      = "oneid.idp.view";
    public const string OneIdIdpConfigure = "oneid.idp.configure";

    // OneId platform — Audit log
    public const string OneIdAuditView = "oneid.audit.view";

    // Tenant Admin — User lifecycle management (FR-14, FR-21)
    public const string AdminUsersView       = "oneid.admin.users.view";
    public const string AdminUsersCreate     = "oneid.admin.users.create";
    public const string AdminUsersUpdate     = "oneid.admin.users.update";
    public const string AdminUsersDeactivate = "oneid.admin.users.deactivate";
    public const string AdminUsersRevoke     = "oneid.admin.users.revoke";

    // Tenant Admin — Role management (FR-7)
    public const string AdminRolesView   = "oneid.admin.roles.view";
    public const string AdminRolesCreate = "oneid.admin.roles.create";
    public const string AdminRolesUpdate = "oneid.admin.roles.update";
    public const string AdminRolesDelete = "oneid.admin.roles.delete";

    // Tenant Admin — Role Set management (FR-8)
    public const string AdminRoleSetsView   = "oneid.admin.rolesets.view";
    public const string AdminRoleSetsCreate = "oneid.admin.rolesets.create";
    public const string AdminRoleSetsUpdate = "oneid.admin.rolesets.update";
    public const string AdminRoleSetsDelete = "oneid.admin.rolesets.delete";

    // Tenant Admin — Group management (FR-9)
    public const string AdminGroupsView          = "oneid.admin.groups.view";
    public const string AdminGroupsCreate        = "oneid.admin.groups.create";
    public const string AdminGroupsUpdate        = "oneid.admin.groups.update";
    public const string AdminGroupsDelete        = "oneid.admin.groups.delete";
    public const string AdminGroupsMembersManage = "oneid.admin.groups.members.manage";

    // Tenant Admin — Dimensional Attribute management (FR-10)
    public const string AdminDimensionsView   = "oneid.admin.dimensions.view";
    public const string AdminDimensionsAssign = "oneid.admin.dimensions.assign";

    // Tenant Admin — Audit log read (FR-22)
    public const string AdminAuditView = "oneid.admin.audit.view";

    // OneDealer sample permissions — Business Partners / Contact Persons
    public const string OdBpView   = "onedealer.bp.view";
    public const string OdBpCreate = "onedealer.bp.create";
    public const string OdBpEdit   = "onedealer.bp.edit";
    public const string OdCpView   = "onedealer.cp.view";

    // OneDealer sample permissions — Leads & Opportunities
    public const string OdLeadsView          = "onedealer.leads.view";
    public const string OdLeadsCreate        = "onedealer.leads.create";
    public const string OdOpportunitiesView  = "onedealer.opportunities.view";
    public const string OdOpportunitiesCreate = "onedealer.opportunities.create";

    // OneDealer sample permissions — Vehicles
    public const string OdVehiclesView   = "onedealer.vehicles.view";
    public const string OdVehiclesCreate = "onedealer.vehicles.create";

    // OneDealer sample permissions — After Sales
    public const string OdAfterSalesJobCardView   = "onedealer.aftersales.jobcard.view";
    public const string OdAfterSalesJobCardCreate = "onedealer.aftersales.jobcard.create";

    // OneDealer sample permissions — Calendar
    public const string OdCalendarView = "onedealer.calendar.view";

    // OneDealer sample permissions — CRM (used in permission evaluation tests)
    public const string CrmRead  = "od.crm.read";
    public const string CrmWrite = "od.crm.write";

    // OneDealer sample permissions — Finance (used in permission evaluation tests)
    public const string FinanceRead  = "od.finance.read";
    public const string FinanceWrite = "od.finance.write";
}
