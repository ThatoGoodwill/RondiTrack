import 'server-only'
import createClient from 'openapi-fetch'
import { notFound, redirect } from 'next/navigation'
import type { paths } from '@/api/schema'
import { getSession } from './session'
import { errorFromResponse, ApiError, type Problem } from './errorMessages'
export function apiBase(): string {
  const base = process.env.API_BASE_URL
  if (!base || !/^https?:\/\//.test(base)) throw new Error('Configure API_BASE_URL')
  return base.replace(/\/$/, '')
}
export async function apiForSession() {
  const session = await getSession()
  if (!session) { redirect('/login'); throw new Error('No session') }
  return createClient<paths>({
    baseUrl: apiBase(), headers: { Authorization: `Bearer ${session.accessToken}` },
    fetch: (input, init) => fetch(input, { ...init, cache: 'no-store' }),
  })
}
/** For newly added endpoints not present in the downloaded 2026-10-22 OpenAPI snapshot. */
export async function apiRequest(path: `/api/v1/${string}`, init: RequestInit = {}): Promise<Response> {
  const session = await getSession()
  if (!session) { redirect('/login'); throw new Error('No session') }
  return fetch(`${apiBase()}${path}`, {
    ...init, cache: 'no-store',
    headers: { ...Object.fromEntries(new Headers(init.headers).entries()), Authorization: `Bearer ${session.accessToken}` },
  })
}
export async function readOrFail<T>(response: Response): Promise<T> {
  if (response.status === 401) redirect('/login?expired=1')
  if (response.status === 404) notFound()
  if (!response.ok) throw await errorFromResponse(response)
  return await response.json() as T
}
export { ApiError }

export function unwrapApi<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.response.status === 401) redirect('/login?expired=1')
  if (result.response.status === 404) notFound()
  if (!result.response.ok) throw new ApiError(result.response.status, result.error as Problem,
    result.response.headers.get('Retry-After'))
  return result.data as T
}
