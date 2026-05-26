import { useState, useEffect } from 'react'
import { Outlet } from 'react-router'
import { GlobalNav } from '@/components/shared/GlobalNav'
import { Breadcrumbs } from '@/components/shared/Breadcrumbs'
import { CommandPalette } from '@/components/shared/CommandPalette'
import { useTenantStore } from '@/store/tenant-store'

export function TenantAdminLayout() {
  const [paletteOpen, setPaletteOpen] = useState(false)
  const tenantId = useTenantStore((s) => s.activeTenantId)

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setPaletteOpen((prev) => !prev)
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

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
      <CommandPalette
        open={paletteOpen}
        onOpenChange={setPaletteOpen}
        tier="tenant"
        tenantId={tenantId}
      />
    </div>
  )
}
