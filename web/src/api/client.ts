import createClient from 'openapi-fetch'
import { authFetch, ApiError } from '../auth/session'
import type { paths } from './schema'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'

export const api = createClient<paths>({
  baseUrl: BASE_URL,
  fetch: authFetch,
  credentials: 'include',
})

export async function unwrap<T>(
  promise: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await promise
  if (!response.ok) {
    throw new ApiError(response.status, error ?? null, response.headers.get('Retry-After'))
  }
  return data as T
}