// Single-flight is mandatory for RondiTrack because the API treats two simultaneous
// refresh requests with the same cookie as token theft: the first rotates the token,
// the second gets 401 auth.refresh_reused. Without single-flight, a page with several
// parallel queries that all get 401 would send N refresh requests, revoke the family,
// and sign the user out. With single-flight, only one POST /auth/refresh goes out and
// every caller waits for that one result.

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'

export class ApiError extends Error {
  status: number
  body: unknown
  retryAfter: string | null

  constructor(status: number, body: unknown, retryAfter: string | null) {
    super(`API error ${status}`)
    this.status = status
    this.body = body
    this.retryAfter = retryAfter
  }
}

// The access token lives ONLY here. Never in localStorage, sessionStorage, or a JS-visible cookie.
let token: string | null = null

type SignedOutListener = () => void
const signedOutListeners = new Set<SignedOutListener>()
export function onSignedOut(fn: SignedOutListener): () => void {
  signedOutListeners.add(fn)
  return () => signedOutListeners.delete(fn)
}
function signalSignedOut() {
  token = null
  signedOutListeners.forEach((fn) => fn())
}

let inFlight: Promise<string | null> | null = null

async function doRefresh(): Promise<string | null> {
  const res = await fetch(`${API_BASE}/api/v1/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok) {
    signalSignedOut()
    return null
  }
  const body = (await res.json()) as { accessToken?: string }
  const newToken = body.accessToken ?? null
  token = newToken
  return newToken
}

export function refreshAccessToken(): Promise<string | null> {
  if (inFlight) return inFlight
  inFlight = doRefresh().finally(() => {
    inFlight = null
  })
  return inFlight
}

export async function login(email: string, password: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) {
    const body = await res.json().catch(() => null)
    throw new ApiError(res.status, body, res.headers.get('Retry-After'))
  }
  const body = (await res.json()) as { accessToken: string }
  token = body.accessToken
}

export async function logout(): Promise<void> {
  token = null
  try {
    await fetch(`${API_BASE}/api/v1/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    })
  } catch {
    // ignore
  } finally {
    signalSignedOut()
  }
}

export async function authFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const isRequest = input instanceof Request
  const originalRequest = isRequest ? input : new Request(input, init)
  const bodyForRetry = originalRequest.clone()
  const hadTokenBefore = token !== null

  if (token === null) {
    await refreshAccessToken()
    if (token === null) {
      return fetch(originalRequest)
    }
  }

  const requestWithAuth = new Request(originalRequest, {
    headers: withBearer(originalRequest.headers, token!),
  })
  const firstResponse = await fetch(requestWithAuth)

  if (firstResponse.status !== 401) return firstResponse

  if (!hadTokenBefore) {
    return firstResponse
  }

  const refreshedToken = inFlight ? await inFlight : await refreshAccessToken()
  if (refreshedToken === null) {
    return firstResponse
  }

  const retryRequest = new Request(bodyForRetry, {
    headers: withBearer(bodyForRetry.headers, refreshedToken),
  })
  return fetch(retryRequest)
}

function withBearer(headers: Headers, bearer: string): Headers {
  const copy = new Headers(headers)
  copy.set('Authorization', `Bearer ${bearer}`)
  return copy
}

export function __resetForTests() {
  token = null
  inFlight = null
  signedOutListeners.clear()
}