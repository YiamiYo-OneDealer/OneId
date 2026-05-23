import { Link } from 'react-router'

export function InternalDashboard() {
  return (
    <div className="p-8 text-foreground">
      <h1 className="text-2xl font-semibold mb-4">Internal Admin</h1>
      <p className="text-muted-foreground mb-4">Select a tenant to manage.</p>
      <p className="text-muted-foreground text-sm">Tenant list coming in Epic 5c.</p>
    </div>
  )
}
