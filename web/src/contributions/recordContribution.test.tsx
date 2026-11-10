import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { render, screen, waitFor, cleanup } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { login, __resetForTests } from '../auth/session'
import { RecordContributionForm } from './RecordContributionForm'

const API = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'
const stokvelId = '4ece96e2-3bf5-41bd-980d-5d64b83de92f'
const memberId = '91d1149b-8f92-498e-a0df-4fdc6023dfef'
const cycleId = 'e73547e3-693d-49dc-bdf8-b97e92c7060e'
const keysSeen: string[] = []
const amountsSeen: number[] = []
const authSeen: string[] = []
let attempt = 0
let scenario: 'network' | 'success' | 'rate' | 'unauthorized' = 'success'
const recorded = { id: crypto.randomUUID(), stokvelId, contributionCycleId: cycleId, userId: memberId,
  amountCents: 15050, recordedAt: new Date().toISOString(), cycleLabel: 'Oct 2026' }

const server = setupServer(
  http.post(`${API}/api/v1/auth/login`, () => HttpResponse.json({ accessToken: 'old' })),
  http.post(`${API}/api/v1/auth/refresh`, () => HttpResponse.json({ accessToken: 'new' })),
  http.post(`${API}/api/v1/stokvels/:stokvelId/cycles/:cycleId/contributions`, async ({ request }) => {
    attempt++
    keysSeen.push(request.headers.get('Idempotency-Key') ?? '')
    authSeen.push(request.headers.get('Authorization') ?? '')
    const data = await request.json() as { amountInCents: number }
    amountsSeen.push(data.amountInCents)
    if (scenario === 'network' && attempt === 1) return HttpResponse.error()
    if (scenario === 'rate' && attempt === 1)
      return HttpResponse.json({ detail: 'Rate limit', correlationId: 'RATE001' }, { status: 429, headers: { 'Retry-After': '1' } })
    if (scenario === 'unauthorized' && attempt === 1)
      return HttpResponse.json({ detail: 'Expired' }, { status: 401 })
    return HttpResponse.json(recorded, { status: 201 })
  }),
)
function form() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}>
    <RecordContributionForm stokvelId={stokvelId} cycleId={cycleId} cycleLabel="Oct 2026"
      status="Open" ownId={memberId} role="Member" owedCents={15050}
      members={[{ userId: memberId, label: 'You' }]} />
  </QueryClientProvider>)
}
async function submit() {
  await userEvent.click(screen.getByRole('button', { name: 'Record contribution' }))
  await waitFor(() => expect(keysSeen.length).toBeGreaterThan(0))
}
async function waitForFinish() {
  await waitFor(() => expect(screen.getByRole('button', { name: 'Record contribution' })).not.toBeDisabled())
}

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterAll(() => server.close())
beforeEach(async () => {
  __resetForTests(); keysSeen.length = 0; amountsSeen.length = 0; authSeen.length = 0
  attempt = 0; scenario = 'success'
  await login('member@example.org', 'correct-password')
})
afterEach(() => { cleanup(); server.resetHandlers() })

describe('7.4 payment intent over the real typed client and authFetch, with MSW', () => {
  it('a retry after a network error sends the same key', async () => {
    scenario = 'network'; form()
    await submit()
    expect(await screen.findByText(/payment may have gone through/i)).toBeInTheDocument()
    await submit()
    await waitFor(() => expect(keysSeen).toHaveLength(2))
    expect(keysSeen[0]).toBeTruthy()
    expect(keysSeen[0]).toBe(keysSeen[1])
  })
  it('the next contribution gets a new key', async () => {
    form(); await submit(); await screen.findByText(/Recorded.*Oct 2026/i)
    await submit(); await waitFor(() => expect(keysSeen).toHaveLength(2))
    expect(keysSeen[1]).not.toBe(keysSeen[0])
  })
  it('a 429 keeps the key, and Retry-After is visible', async () => {
    scenario = 'rate'; form(); await submit()
    await screen.findByText(/Try again in 1 second/i)
    await submit(); await waitFor(() => expect(keysSeen).toHaveLength(2))
    expect(keysSeen[0]).toBe(keysSeen[1])
  })
  it('changing the amount after failure starts a new intent', async () => {
    scenario = 'network'; form(); await submit(); await screen.findByText(/payment may have gone through/i)
    const input = screen.getByRole('textbox', { name: /Amount in rands/i })
    await userEvent.clear(input); await userEvent.type(input, '151.50')
    await submit(); await waitFor(() => expect(keysSeen).toHaveLength(2))
    expect(keysSeen[0]).not.toBe(keysSeen[1]); expect(amountsSeen[1]).toBe(15150)
  })
  it('a 401 retry preserves the key while changing Authorization', async () => {
    scenario = 'unauthorized'; form(); await submit()
    await waitFor(() => expect(keysSeen).toHaveLength(2))
    expect(keysSeen[0]).toBe(keysSeen[1])
    expect(authSeen).toEqual(['Bearer old', 'Bearer new'])
  })
  it('cents survive the trip as a JSON number', async () => {
    form(); await submit()
    expect(amountsSeen[0]).toBe(15050)
    expect(typeof amountsSeen[0]).toBe('number')
  })
})
