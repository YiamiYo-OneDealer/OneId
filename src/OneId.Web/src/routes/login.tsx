import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { passwordGrant, mfaGrant } from '@/lib/auth'
import { useAuthStore } from '@/store/auth-store'

type LoginStep = 'credentials' | 'totp'

export function LoginPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { setTokens } = useAuthStore()

  const [step, setStep] = useState<LoginStep>('credentials')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mfaSessionToken, setMfaSessionToken] = useState('')
  const [totpCode, setTotpCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const returnTo = searchParams.get('returnTo')
  const sessionExpired = searchParams.get('session_expired') === '1'
  const safeReturnTo =
    returnTo &&
    returnTo.startsWith('/') &&
    !returnTo.startsWith('//') &&
    !returnTo.startsWith('/\\')
      ? returnTo
      : '/internal/tenants'

  const handleCredentials = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const res = await passwordGrant(email, password)
      setMfaSessionToken(res.mfa_session_token)
      setStep('totp')
    } catch {
      setError('Invalid credentials or MFA code')
    } finally {
      setLoading(false)
    }
  }

  const handleMfa = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const tokens = await mfaGrant(mfaSessionToken, totpCode)
      setTokens(tokens.access_token, tokens.refresh_token)
      navigate(safeReturnTo, { replace: true })
    } catch {
      setError('Invalid credentials or MFA code')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-background text-foreground flex items-center justify-center p-8">
      {sessionExpired && (
        <div
          role="status"
          className="fixed top-4 left-1/2 -translate-x-1/2 bg-blue-50 border border-blue-200 text-blue-800 rounded px-4 py-2 text-sm"
        >
          Your session has expired. Please sign in again.
        </div>
      )}
      {step === 'credentials' ? (
        <form onSubmit={handleCredentials} className="flex flex-col gap-4 w-80">
          <h1 className="text-2xl font-semibold">Sign in</h1>
          {error && <p className="text-red-600 text-sm">{error}</p>}
          <input
            type="email"
            placeholder="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            disabled={loading}
            className="border rounded px-3 py-2"
          />
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            disabled={loading}
            className="border rounded px-3 py-2"
          />
          <button
            type="submit"
            disabled={loading}
            className="bg-primary text-primary-foreground rounded px-4 py-2"
          >
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      ) : (
        <form onSubmit={handleMfa} className="flex flex-col gap-4 w-80">
          <h1 className="text-2xl font-semibold">Two-factor authentication</h1>
          <p className="text-muted-foreground text-sm">
            Enter the code from your authenticator app.
          </p>
          {error && <p className="text-red-600 text-sm">{error}</p>}
          <input
            type="text"
            placeholder="6-digit code"
            value={totpCode}
            onChange={(e) => setTotpCode(e.target.value)}
            required
            maxLength={6}
            autoComplete="one-time-code"
            disabled={loading}
            className="border rounded px-3 py-2 tracking-widest text-center"
          />
          <button
            type="submit"
            disabled={loading}
            className="bg-primary text-primary-foreground rounded px-4 py-2"
          >
            {loading ? 'Verifying…' : 'Verify'}
          </button>
        </form>
      )}
    </div>
  )
}
