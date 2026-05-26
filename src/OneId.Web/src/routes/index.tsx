import { createBrowserRouter, Navigate } from 'react-router'
import { AuthenticatedLayout } from './_authenticated'
import { ErrorPage } from './error'
import { LoginPage } from './login'
import { SuspendedPage } from './suspended'
import { InternalLayout } from './internal/_layout'
import { InternalDashboard } from './internal/index'
import { TenantContextLayout } from './internal/tenants/_layout'
import { TenantDetailPage } from './internal/tenants/TenantDetailPage'
import { TenantListPage } from './internal/tenants/TenantListPage'
import { TenantProvisioningPage } from './internal/tenants/TenantProvisioningPage'
import { TenantUsersPage } from './internal/tenants/TenantUsersPage'
import { TenantGroupsPage } from './internal/tenants/TenantGroupsPage'
import { TenantRolesPage } from './internal/tenants/TenantRolesPage'
import { TenantRoleSetsPage } from './internal/tenants/TenantRoleSetsPage'
import { PermissionsPage } from './internal/permissions'
import { TenantAdminLayout } from './tenant/_layout'
import { TenantAdminDashboard } from './tenant/index'
import { StubPage } from './_stub-page'
import { ForgotPasswordPage } from './forgot-password'
import { ResetPasswordPage } from './reset-password'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AuthenticatedLayout />,
    errorElement: <ErrorPage />,
    children: [
      { index: true, element: <Navigate to="/internal/tenants" replace /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'suspended', element: <SuspendedPage /> },
      { path: 'forgot-password', element: <ForgotPasswordPage /> },
      { path: 'reset-password', element: <ResetPasswordPage /> },
      {
        path: 'internal',
        element: <InternalLayout />,
        children: [
          { index: true, element: <InternalDashboard /> },
          { path: 'tenants', element: <TenantListPage /> },
          { path: 'tenants/new', element: <TenantProvisioningPage /> },
          { path: 'permissions', element: <PermissionsPage /> },
          {
            path: 'tenants/:tenantId',
            element: <TenantContextLayout />,
            children: [
              { index: true, element: <TenantDetailPage /> },
              { path: 'users', element: <TenantUsersPage /> },
              { path: 'groups', element: <TenantGroupsPage /> },
              { path: 'roles', element: <TenantRolesPage /> },
              { path: 'role-sets', element: <TenantRoleSetsPage /> },
            ],
          },
        ],
      },
      {
        path: 'tenant',
        element: <TenantAdminLayout />,
        children: [
          { index: true, element: <TenantAdminDashboard /> },
          { path: 'users', element: <StubPage title="Users" /> },
          { path: 'groups', element: <StubPage title="Groups" /> },
          { path: 'roles', element: <StubPage title="Roles" /> },
          { path: 'role-sets', element: <StubPage title="Role Sets" /> },
          { path: 'audit-log', element: <StubPage title="Audit Log" /> },
        ],
      },
    ],
  },
])
