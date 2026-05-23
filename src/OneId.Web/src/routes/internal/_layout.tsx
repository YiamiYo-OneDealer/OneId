import { Outlet } from 'react-router'

export function InternalLayout() {
  // GlobalNav and AdminTierBanner added in Story 5a.3
  return (
    <div className="min-h-screen bg-background text-foreground">
      <Outlet />
    </div>
  )
}
