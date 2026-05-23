import { createBrowserRouter } from 'react-router'
import { AuthenticatedLayout } from './_authenticated'
import { ErrorPage } from './error'
import { LoginPage } from './login'
import { SuspendedPage } from './suspended'
import { InternalLayout } from './internal/_layout'
import { InternalDashboard } from './internal/index'
import { TenantContextLayout } from './internal/tenants/_layout'
import { TenantDashboardStub } from './internal/tenants/index'
import { TenantAdminLayout } from './tenant/_layout'
import { TenantAdminDashboard } from './tenant/index'
import { StubPage } from './_stub-page'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AuthenticatedLayout />,
    errorElement: <ErrorPage />,
    children: [
      { path: 'login', element: <LoginPage /> },
      { path: 'suspended', element: <SuspendedPage /> },
      {
        path: 'internal',
        element: <InternalLayout />,
        children: [
          { index: true, element: <InternalDashboard /> },
          {
            path: 'tenants/:tenantId',
            element: <TenantContextLayout />,
            children: [
              { index: true, element: <TenantDashboardStub /> },
              { path: 'users', element: <StubPage title="Users" /> },
              { path: 'groups', element: <StubPage title="Groups" /> },
              { path: 'roles', element: <StubPage title="Roles" /> },
              { path: 'role-sets', element: <StubPage title="Role Sets" /> },
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
