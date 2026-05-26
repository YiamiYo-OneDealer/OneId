import { useState, useEffect } from 'react'
import { Outlet } from 'react-router'
import { GlobalNav } from '@/components/shared/GlobalNav'
import { AdminTierBanner } from '@/components/shared/AdminTierBanner'
import { Breadcrumbs } from '@/components/shared/Breadcrumbs'
import { CommandPalette } from '@/components/shared/CommandPalette'

export function InternalLayout() {
  const [paletteOpen, setPaletteOpen] = useState(false)

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
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <AdminTierBanner />
      <div className="flex flex-1">
        <GlobalNav tier="internal" />
        <main className="flex flex-1 flex-col">
          <header className="border-b border-border px-6 py-3">
            <Breadcrumbs />
          </header>
          <div className="flex-1 px-6 py-4">
            <Outlet />
          </div>
        </main>
      </div>
      <CommandPalette
        open={paletteOpen}
        onOpenChange={setPaletteOpen}
        tier="internal"
        tenantId={null}
      />
    </div>
  )
}
