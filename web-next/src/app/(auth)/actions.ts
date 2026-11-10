 'use server'
import { redirect } from 'next/navigation'
import { apiBase } from '@/lib/serverApi'
import { setSession } from '@/lib/session'
import { safeNextPath } from '@/lib/safeNext'
import { describeError, errorFromResponse, type ErrorDescription } from '@/lib/errorMessages'

export type AuthState = { message: string; reference: string | null }
export const initialAuthState: AuthState = { message: '', reference: null }
async function failure(response: Response, context: 'login' | 'general' = 'general'): Promise<AuthState> {
  const error = await errorFromResponse(response)
  const info: ErrorDescription = describeError(error, context)
  return { message: `${info.message} ${info.advice}`.trim(), reference: info.reference }
}
export async function signIn(_: AuthState, data: FormData): Promise<AuthState> {
  const email = String(data.get('email') ?? '').trim()
  const password = String(data.get('password') ?? '')
  const next = safeNextPath(String(data.get('next') ?? ''))
  if (!email || !password) return { message: 'Enter your email and password.', reference: null }
  let res: Response
  try { res = await fetch(`${apiBase()}/api/v1/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, cache: 'no-store',
    body: JSON.stringify({ email, password }),
  }) } catch { return { message: "Can't reach RondiTrack. Check your connection.", reference: null } }
  if (!res.ok) return failure(res, 'login')
  const tokens = await res.json() as { accessToken: string; expiresAt: string }
  const refreshCookie = res.headers.getSetCookie().find(c => /^refresh_token=/.test(c))
  const raw = refreshCookie?.match(/^refresh_token=([^;]*)/)?.[1]
  const expiresAt = Date.parse(tokens.expiresAt)
  if (!raw || !tokens.accessToken || !Number.isFinite(expiresAt))
    return { message: 'Sign-in response was incomplete. Please try again.', reference: null }
  const profile = await fetch(`${apiBase()}/api/v1/me`, {
    headers: { Authorization: `Bearer ${tokens.accessToken}` }, cache: 'no-store',
  }).catch(() => null)
  if (!profile?.ok) return { message: 'Could not load your account. Please retry.', reference: null }
  const user = await profile.json() as { id: string; email: string; firstName: string; lastName: string }
  await setSession({
    accessToken: tokens.accessToken, refreshToken: decodeURIComponent(raw), accessExpiresAt: expiresAt,
    user: { id: user.id, email: user.email, displayName: `${user.firstName} ${user.lastName}`.trim() },
  })
  return redirect(next)
}
export async function register(_: AuthState, data: FormData): Promise<AuthState> {
  const email = String(data.get('email') ?? '').trim()
  const password = String(data.get('password') ?? '')
  const firstName = String(data.get('firstName') ?? '').trim()
  const lastName = String(data.get('lastName') ?? '').trim()
  const dateOfBirth = String(data.get('dateOfBirth') ?? '')
  if (!email || !password || !firstName || !lastName || !/^\d{4}-\d{2}-\d{2}$/.test(dateOfBirth))
    return { message: 'Complete every field and enter a valid birth date.', reference: null }
  if (password.length < 12) return { message: 'Password must have at least 12 characters.', reference: null }
  let res: Response
  try { res = await fetch(`${apiBase()}/api/v1/auth/register`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, cache: 'no-store',
    body: JSON.stringify({ firstName, lastName, email, password, dateOfBirth }),
  }) } catch { return { message: "Can't reach RondiTrack. Try again later.", reference: null } }
  if (!res.ok) return failure(res)
  // The API intentionally registers an account without signing in. Follow with sign-in.
  return redirect('/login?registered=1')
}
