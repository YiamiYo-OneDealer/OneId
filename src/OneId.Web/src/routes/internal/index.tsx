import { Link } from 'react-router'

export function InternalDashboard() {
  return (
    <div className="p-8 text-foreground">
      <h1 className="text-2xl font-semibold mb-4">Internal Admin</h1>
      <p className="text-muted-foreground mb-4">Select a tenant to manage.</p>
      <Link to="/internal/tenants" className="text-primary underline">
        View all tenants →
      </Link>
    </div>
  )
}
