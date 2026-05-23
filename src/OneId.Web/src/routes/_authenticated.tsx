import { Outlet } from 'react-router'

export function AuthenticatedLayout() {
  // Auth check is Epic 2. For now, always render outlet.
  // TODO Epic 2: Replace with real token check from auth store
  return <Outlet />
}
