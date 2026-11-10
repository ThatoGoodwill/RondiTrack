 'use server'
import { redirect } from 'next/navigation'
import { apiBase } from '@/lib/serverApi'
import { deleteSession, getSession } from '@/lib/session'
export async function signOut() {
  const session = await getSession()
  try {
    if (session) await fetch(`${apiBase()}/api/v1/auth/logout`, {
      method: 'POST', headers: { Cookie: `refresh_token=${encodeURIComponent(session.refreshToken)}` },
      cache: 'no-store',
    })
  } catch { /* Always clear the local session even if API is down. */ }
  await deleteSession()
  redirect('/login')
}
