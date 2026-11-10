import 'server-only'
import { cookies } from 'next/headers'
import { redirect } from 'next/navigation'
import { COOKIE_NAME, cookieOptions, sealSession, unsealSession, type SiteSession } from './sessionCrypto'
export async function getSession(): Promise<SiteSession | null> {
  const jar = await cookies()
  return unsealSession(jar.get(COOKIE_NAME)?.value)
}
export async function requireSession(): Promise<SiteSession> {
  const s = await getSession()
  if (!s) { redirect('/login'); throw new Error('No session') }
  return s
}
export async function setSession(session: SiteSession): Promise<void> {
  (await cookies()).set(COOKIE_NAME, await sealSession(session), cookieOptions)
}
export async function deleteSession(): Promise<void> {
  (await cookies()).delete(COOKIE_NAME)
}
