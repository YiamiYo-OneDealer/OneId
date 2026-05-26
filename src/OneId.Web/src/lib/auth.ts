interface PasswordGrantResponse {
  mfa_required: boolean
  mfa_session_token: string
  totp_enrollment_uri?: string
}

interface TokenResponse {
  access_token: string
  refresh_token: string
  token_type: string
  expires_in: number
}

// These helpers use plain fetch intentionally — using the ky apiClient here would
// cause infinite loops because the apiClient's 401 interceptor calls refreshGrant.
export async function passwordGrant(
  email: string,
  password: string,
): Promise<PasswordGrantResponse> {
  const res = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      username: email,
      password,
      client_id: 'oneid-dev-client',
      scope: 'openid email profile offline_access',
    }),
  })
  if (!res.ok) throw new Error('invalid_credentials')
  return res.json()
}

export async function mfaGrant(
  mfaSessionToken: string,
  totpCode: string,
): Promise<TokenResponse> {
  const res = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'urn:oneid:mfa',
      mfa_session_token: mfaSessionToken,
      totp_code: totpCode,
      client_id: 'oneid-dev-client',
      scope: 'openid email profile offline_access',
    }),
  })
  if (!res.ok) throw new Error('invalid_mfa')
  return res.json()
}

export async function refreshGrant(refreshToken: string): Promise<TokenResponse> {
  const res = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: 'oneid-dev-client',
    }),
  })
  if (!res.ok) throw new Error('refresh_failed')
  const data: TokenResponse = await res.json()
  if (!data.access_token || !data.refresh_token) throw new Error('incomplete_token_response')
  return data
}
