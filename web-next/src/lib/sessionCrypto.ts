import 'server-only'
import { EncryptJWT, jwtDecrypt } from 'jose'

export type SiteSession = {
  accessToken: string
  refreshToken: string
  accessExpiresAt: number
  user: { id: string; displayName: string; email: string }
}
export const COOKIE_NAME = 'session'
export const cookieOptions = {
  httpOnly: true as const,
  secure: process.env.NODE_ENV === 'production',
  sameSite: 'lax' as const,
  path: '/',
  maxAge: 7 * 24 * 60 * 60, // Matches API Jwt:RefreshTokenDays = 7 in this repo.
}
function key(): Uint8Array {
  const secret = process.env.SESSION_SECRET
  if (!secret) throw new Error('SESSION_SECRET is required')
  // base64 32-byte key, works in Node AND Next proxy runtimes.
  const decoded = atob(secret.replace(/-/g, '+').replace(/_/g, '/'))
  if (decoded.length !== 32) throw new Error('SESSION_SECRET must be base64-encoded 32 bytes')
  return Uint8Array.from(decoded, c => c.charCodeAt(0))
}
function isSession(value: unknown): value is SiteSession {
  if (!value || typeof value !== 'object') return false
  const record = value as Record<string, unknown>
  const user = record.user
  return typeof record.accessToken === 'string' && record.accessToken.length > 0 &&
    typeof record.refreshToken === 'string' && record.refreshToken.length > 0 &&
    typeof record.accessExpiresAt === 'number' && Number.isFinite(record.accessExpiresAt) &&
    !!user && typeof user === 'object' &&
    ['id', 'displayName', 'email'].every(k => typeof (user as Record<string, unknown>)[k] === 'string')
}
export async function sealSession(value: SiteSession): Promise<string> {
  if (!isSession(value)) throw new Error('Invalid session')
  return new EncryptJWT({ value }).setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt().setExpirationTime(`${cookieOptions.maxAge}s`).encrypt(key())
}
export async function unsealSession(token: string | null | undefined): Promise<SiteSession | null> {
  if (!token) return null
  try {
    const { payload } = await jwtDecrypt(token, key(), {
      keyManagementAlgorithms: ['dir'], contentEncryptionAlgorithms: ['A256GCM'],
    })
    return isSession(payload.value) ? payload.value : null
  } catch { return null }
}
