import type { NextRequest } from 'next/server'
import { NextResponse } from 'next/server'
import { COOKIE_NAME, cookieOptions, sealSession, unsealSession } from '@/lib/sessionCrypto'
import { refreshOnce } from '@/lib/refreshOnce'
import { safeNextPath } from '@/lib/safeNext'

function blocked(request: NextRequest, expired: boolean) {
  if (request.nextUrl.pathname.startsWith('/bff/'))
    return NextResponse.json({ title: expired ? 'Session expired' : 'Please sign in' }, { status: 401 })
  const url = new URL('/login', request.url)
  if (expired) url.searchParams.set('expired', '1')
  else url.searchParams.set('next', safeNextPath(`${request.nextUrl.pathname}${request.nextUrl.search}`))
  return NextResponse.redirect(url)
}
export async function proxy(request: NextRequest) {
  const encrypted = request.cookies.get(COOKIE_NAME)?.value
  let session = await unsealSession(encrypted)
  if (!session) {
    const response = blocked(request, false)
    response.cookies.set(COOKIE_NAME, '', { ...cookieOptions, maxAge: 0 })
    return response
  }
  const margin = Number(process.env.REFRESH_MARGIN_SECONDS ?? '60') * 1000
  if (session.accessExpiresAt > Date.now() + margin) return NextResponse.next()
  const rotated = await refreshOnce(session.refreshToken).catch(() => null)
  if (!rotated) {
    const response = blocked(request, true)
    response.cookies.set(COOKIE_NAME, '', { ...cookieOptions, maxAge: 0 })
    return response
  }
  session = { ...session, ...rotated }
  const value = await sealSession(session)
  // BOTH directions matter: this incoming render and every future browser request.
  const headers = new Headers(request.headers)
  headers.set('cookie', `${request.headers.get('cookie')?.split(';').filter(v => !v.trim().startsWith(`${COOKIE_NAME}=`)).join('; ') ?? ''}; ${COOKIE_NAME}=${value}`)
  const response = NextResponse.next({ request: { headers } })
  response.cookies.set(COOKIE_NAME, value, cookieOptions)
  return response
}
export const config = { matcher: ['/dashboard/:path*', '/stokvels/:path*', '/activity/:path*', '/bff/:path*'] }
