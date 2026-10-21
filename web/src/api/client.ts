import createClient, { type Middleware } from 'openapi-fetch'
import { authFetch } from '../auth/session'
import { ApiError } from '../auth/session'
import type { paths } from './schema'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'

// One client. All requests go through authFetch, so 401s trigger the single-flight refresh.
export const api = createClient<paths>({
  baseUrl: BASE_URL,
  fetch: authFetch,
  credentials: 'include',
})

// A small middleware that converts non-2xx responses into a thrown ApiError
// so callers only deal with data or an exception. The unwrap() helper below does the same
// shape check for code that prefers try/catch over the middleware.
const toApiError: Middleware = {
  async onResponse({ response }) {
    if (!response.ok) {
      const body = await response.clone().json().catch(() => null)
      throw new ApiError(response.status, body, response.headers.get('Retry-After'))
    }
    return undefined
  },
}

// Optional: attach the middleware. Comment out if you want explicit unwrap() everywhere instead.
// api.use(toApiError)

/**
 * Unwrap an openapi-fetch response: return the parsed data on 2xx,
 * throw ApiError with the problem+json body and the Retry-After header on non-2xx.
 */
export async function unwrap<T>(
  promise: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await promise
  if (!response.ok) {
    throw new ApiError(response.status, error ?? null, response.headers.get('Retry-After'))
  }
  return data as T
}