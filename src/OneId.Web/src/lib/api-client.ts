import ky from 'ky'
import { refreshGrant } from './auth'
import { useAuthStore } from '@/store/auth-store'

export const apiClient = ky.create({
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

        const { refreshToken, setTokens, clearTokens } = useAuthStore.getState()
        if (!refreshToken) {
          clearTokens()
          window.location.href = '/login'
          return response
        }

        try {
          const tokens = await refreshGrant(refreshToken)
          setTokens(tokens.access_token, tokens.refresh_token)

          // Retry the original request with the new access token.
          const headers = new Headers(request.headers)
          headers.set('Authorization', `Bearer ${tokens.access_token}`)
          return ky.retry({ request: new Request(request, { headers }) })
        } catch {
          clearTokens()
          window.location.href = '/login'
          return response
        }
      },
    ],
  },
})
