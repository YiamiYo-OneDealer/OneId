import ky from 'ky'
import { refreshGrant } from './auth'
import { useAuthStore } from '@/store/auth-store'

// Singleton in-flight refresh promise — prevents concurrent 401s from racing to refresh simultaneously.
// Multiple simultaneous 401 responses share one refresh attempt; the second would fail (rotation).
let refreshPromise: Promise<void> | null = null

async function doRefresh(): Promise<void> {
  const { refreshToken, setTokens, clearTokens } = useAuthStore.getState()
  if (!refreshToken) {
    clearTokens()
    const returnTo = encodeURIComponent(window.location.pathname + window.location.search)
    window.location.href = `/login?returnTo=${returnTo}&session_expired=1`
    return
  }
  try {
    const tokens = await refreshGrant(refreshToken)
    if (!tokens.access_token || !tokens.refresh_token) {
      throw new Error('incomplete_token_response')
    }
    setTokens(tokens.access_token, tokens.refresh_token)
  } catch (err) {
    clearTokens()
    const returnTo = encodeURIComponent(window.location.pathname + window.location.search)
    window.location.href = `/login?returnTo=${returnTo}&session_expired=1`
    throw err
  }
}

export const apiClient = ky.create({
  baseUrl: import.meta.env.VITE_API_BASE_URL || window.location.origin,
  hooks: {
    beforeRequest: [
      ({ request }) => {
        const { accessToken } = useAuthStore.getState()
        if (accessToken) {
          request.headers.set('Authorization', `Bearer ${accessToken}`)
        }
      },
    ],
    afterResponse: [
      async ({ request, response, retryCount }) => {
        // Only attempt refresh on first 401 — retryCount guard prevents infinite loops.
        if (response.status !== 401 || retryCount > 0) {
          return response
        }

        // Coalesce concurrent 401s: all share one refresh call.
        if (!refreshPromise) {
          refreshPromise = doRefresh().finally(() => {
            refreshPromise = null
          })
        }

        try {
          await refreshPromise
        } catch {
          // doRefresh already cleared tokens and redirected; return the 401 to stop ky processing.
          return response
        }

        // Retry the original request with the new access token.
        const { accessToken } = useAuthStore.getState()
        const headers = new Headers(request.headers)
        if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
        return ky.retry({ request: new Request(request, { headers }) })
      },
    ],
  },
})
