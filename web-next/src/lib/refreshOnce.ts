import type { SiteSession } from './sessionCrypto'
function apiBase() { const base = process.env.API_BASE_URL; if (!base) throw new Error('API_BASE_URL missing'); return base.replace(/\/$/, '') }

type Rotation = Pick<SiteSession, 'accessToken' | 'accessExpiresAt' | 'refreshToken'>
const pending = new Map<string, Promise<Rotation | null>>()
const recent = new Map<string, { value: Rotation; until: number }>()
const STALE_REQUEST_GRACE_MS = 15_000

/** Single instance only. Never log or expose these raw tokens. */
export function refreshOnce(oldToken: string): Promise<Rotation | null> {
  const cached = recent.get(oldToken)
  if (cached && cached.until > Date.now()) return Promise.resolve(cached.value)
  if (cached) recent.delete(oldToken)
  const active = pending.get(oldToken)
  if (active) return active
  const flight = fetch(`${apiBase()}/api/v1/auth/refresh`, {
    method: 'POST', headers: { Cookie: `refresh_token=${encodeURIComponent(oldToken)}` },
    cache: 'no-store',
  }).then(async response => {
    if (!response.ok) return null
    const data = await response.json() as { accessToken?: string; expiresAt?: string }
    const cookie = response.headers.getSetCookie().find(c => /^refresh_token=/.test(c))
    const rotated = cookie?.match(/^refresh_token=([^;]*)/)?.[1]
    const expires = Date.parse(data.expiresAt ?? '')
    if (!data.accessToken || !rotated || !Number.isFinite(expires)) return null
    const result = {
      accessToken: data.accessToken, accessExpiresAt: expires,
      refreshToken: decodeURIComponent(rotated),
    }
    recent.set(oldToken, { value: result, until: Date.now() + STALE_REQUEST_GRACE_MS })
    return result
  }).finally(() => { pending.delete(oldToken) })
  pending.set(oldToken, flight)
  return flight
}
