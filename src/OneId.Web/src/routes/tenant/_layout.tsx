import { Outlet } from 'react-router'
import { GlobalNav } from '@/components/shared/GlobalNav'
import { Breadcrumbs } from '@/components/shared/Breadcrumbs'

export function TenantAdminLayout() {
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <GlobalNav tier="tenant" />
      <main className="flex flex-1 flex-col">
        <header className="border-b border-border px-6 py-3">
          <Breadcrumbs />
        </header>
        <div className="flex-1 px-6 py-4">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
