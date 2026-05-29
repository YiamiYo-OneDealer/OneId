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
    public const string AdminUsersView       = "od.admin.users.view";
    public const string AdminUsersCreate     = "od.admin.users.create";
    public const string AdminUsersUpdate     = "od.admin.users.update";
    public const string AdminUsersDeactivate = "od.admin.users.deactivate";
    public const string AdminUsersRevoke     = "od.admin.users.revoke";

    // Tenant Admin — Role management (FR-7)
    public const string AdminRolesView   = "od.admin.roles.view";
    public const string AdminRolesCreate = "od.admin.roles.create";
    public const string AdminRolesUpdate = "od.admin.roles.update";
    public const string AdminRolesDelete = "od.admin.roles.delete";

    // Tenant Admin — Role Set management (FR-8)
    public const string AdminRoleSetsView   = "od.admin.rolesets.view";
    public const string AdminRoleSetsCreate = "od.admin.rolesets.create";
    public const string AdminRoleSetsUpdate = "od.admin.rolesets.update";
    public const string AdminRoleSetsDelete = "od.admin.rolesets.delete";

    // Tenant Admin — Group management (FR-9)
    public const string AdminGroupsView          = "od.admin.groups.view";
    public const string AdminGroupsCreate        = "od.admin.groups.create";
    public const string AdminGroupsUpdate        = "od.admin.groups.update";
    public const string AdminGroupsDelete        = "od.admin.groups.delete";
    public const string AdminGroupsMembersManage = "od.admin.groups.members.manage";

    // Tenant Admin — Dimensional Attribute management (FR-10)
    public const string AdminDimensionsView   = "od.admin.dimensions.view";
    public const string AdminDimensionsAssign = "od.admin.dimensions.assign";

    // Tenant Admin — Audit log read (FR-22)
    public const string AdminAuditView = "od.admin.audit.view";

    // Business permissions — CRM module
    public const string CrmRead           = "od.crm.read";
    public const string CrmWrite          = "od.crm.write";
    public const string CrmInvoiceCreate  = "od.crm.invoice.create";
    public const string CrmInvoiceApprove = "od.crm.invoice.approve";

    // Business permissions — Finance module
    public const string FinanceRead    = "od.finance.read";
    public const string FinanceWrite   = "od.finance.write";
    public const string FinanceApprove = "od.finance.approve";
}
