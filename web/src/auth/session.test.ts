import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

type FetchMock = ReturnType<typeof vi.fn>

let fetchMock: FetchMock
let refreshCount: number

function makeResponse(status: number, json?: unknown): Response {
  return new Response(json === undefined ? null : JSON.stringify(json), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function extractAuth(headers: HeadersInit | undefined): string | null {
  if (!headers) return null
  if (headers instanceof Headers) return headers.get('Authorization')
  if (Array.isArray(headers)) {
    const found = headers.find(([k]) => k.toLowerCase() === 'authorization')
    return found ? found[1] : null
  }
  for (const [k, v] of Object.entries(headers)) {
    if (k.toLowerCase() === 'authorization') return v
  }
  return null
}

async function installFakeApi() {
  fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url =
      input instanceof Request ? input.url : typeof input === 'string' ? input : input.toString()
    const method = input instanceof Request ? input.method : init?.method ?? 'GET'
    const headers = input instanceof Request ? input.headers : init?.headers

    if (url.endsWith('/api/v1/auth/login') && method === 'POST') {
      return makeResponse(200, { accessToken: 'old' })
    }

    if (url.endsWith('/api/v1/auth/refresh') && method === 'POST') {
      refreshCount++
      await new Promise((r) => setTimeout(r, 20))
      return makeResponse(200, { accessToken: 'new' })
    }

    const auth = extractAuth(headers)
    if (auth === 'Bearer new') return makeResponse(200, { ok: true })
    return makeResponse(401, { title: 'unauthorized' })
  })
  vi.stubGlobal('fetch', fetchMock)
}

async function freshSession() {
  vi.resetModules()
  refreshCount = 0
  const mod = await import('./session')
  return mod
}

beforeEach(async () => {
  await installFakeApi()
})
afterEach(() => {
  vi.unstubAllGlobals()
})

describe('session — single-flight', () => {
  it('five parallel 401s cause exactly one refresh', async () => {
    const s = await freshSession()
    await s.login('a@b', 'pw')
    const calls = Array.from({ length: 5 }, () =>
      s.authFetch('http://localhost:8080/api/v1/protected'),
    )
    const results = await Promise.all(calls)
    expect(refreshCount).toBe(1)
    results.forEach((r) => expect(r.status).toBe(200))
  })

  it('a later expiry starts a new refresh', async () => {
    const s = await freshSession()
    await s.login('a@b', 'pw')
    await Promise.all(
      Array.from({ length: 5 }, () => s.authFetch('http://localhost:8080/api/v1/protected')),
    )
    expect(refreshCount).toBe(1)

    s.__resetForTests()
    await s.login('a@b', 'pw')
    await Promise.all(
      Array.from({ length: 2 }, () => s.authFetch('http://localhost:8080/api/v1/protected')),
    )
    expect(refreshCount).toBe(2)
  })

  it('a failed refresh signs out once', async () => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : String(input)
      if (url.endsWith('/api/v1/auth/login')) return makeResponse(200, { accessToken: 'old' })
      if (url.endsWith('/api/v1/auth/refresh')) return makeResponse(401)
      return makeResponse(401)
    })
    vi.stubGlobal('fetch', fetchMock)

    const s = await freshSession()
    let signedOut = 0
    s.onSignedOut(() => signedOut++)
    await s.login('a@b', 'pw')
    const res = await s.authFetch('http://localhost:8080/api/v1/protected')
    expect(res.status).toBe(401)
    expect(signedOut).toBe(1)
  })

  it('no token means no loop', async () => {
    const s = await freshSession()
    fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : String(input)
      if (url.endsWith('/api/v1/auth/refresh')) return makeResponse(401)
      return makeResponse(401)
    })
    vi.stubGlobal('fetch', fetchMock)

    const res = await s.authFetch('http://localhost:8080/api/v1/protected')
    expect(res.status).toBe(401)
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('a POST body survives the retry', async () => {
    const s = await freshSession()
    await s.login('a@b', 'pw')
    const body = JSON.stringify({ amount: 100 })
    let capturedSecondBody: string | null = null
    let callCount = 0

    fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = input instanceof Request ? input.url : String(input)
      if (url.endsWith('/api/v1/auth/refresh')) {
        return makeResponse(200, { accessToken: 'new' })
      }
      callCount++
      const req = input instanceof Request ? input : new Request(input, init)
      const bodyText = await req.text()
      if (callCount === 1) {
        return makeResponse(401)
      }
      capturedSecondBody = bodyText
      return makeResponse(200, { ok: true })
    })
    vi.stubGlobal('fetch', fetchMock)

    const res = await s.authFetch('http://localhost:8080/api/v1/protected', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
    })
    expect(res.status).toBe(200)
    expect(capturedSecondBody).toBe(body)
  })
})