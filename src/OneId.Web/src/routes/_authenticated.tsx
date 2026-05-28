import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuthStore } from '@/store/auth-store'

const PUBLIC_PATHS = ['/login', '/forgot-password', '/reset-password', '/suspended']

export function AuthenticatedLayout() {
  const { accessToken } = useAuthStore()
  const hasHydrated = useAuthStore.persist.hasHydrated()
  const location = useLocation()

  // Don't redirect from public paths — avoids infinite redirect loop since /login
  // is a sibling route under the same root layout that wraps AuthenticatedLayout.
  const isPublic = PUBLIC_PATHS.some((p) => location.pathname.startsWith(p))

  // Wait for persist hydration before making auth decisions — prevents spurious
  // login redirect on page refresh when the token is in localStorage.
  if (!hasHydrated && !isPublic) return null

  if (!accessToken && !isPublic) {
    const returnTo = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />
  }

  return <Outlet />
}
