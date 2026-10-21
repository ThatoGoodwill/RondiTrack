import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { HomePage } from './HomePage'

function makeFakeApi() {
  return vi.fn(async (input: RequestInfo | URL) => {
    const url = input instanceof Request ? input.url : String(input)

    if (url.endsWith('/api/v1/me/home')) {
      return new Response(
        JSON.stringify({
          stokvels: [
            { stokvelId: 'A', name: 'Stokvel A', role: 'Member', currentCycle: null, arrearsCents: 0, nextPayoutDueUtc: null },
            { stokvelId: 'B', name: 'Stokvel B', role: 'Member', currentCycle: null, arrearsCents: 0, nextPayoutDueUtc: null },
          ],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      )
    }
    if (url.endsWith('/api/v1/stokvels')) {
      return new Response(
        JSON.stringify([
          { id: 'A', name: 'Stokvel A' },
          { id: 'B', name: 'Stokvel B' },
        ]),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      )
    }
    if (url.endsWith('/api/v1/stokvels/A/cycles')) {
      return new Response(
        JSON.stringify([
          { id: 'cycle-A', stokvelId: 'A', label: 'CYCLE-A', targetAmount: 500, status: 'Open', endsOn: '2099-12-31', collectedCents: 1111, dueCents: 0 },
        ]),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      )
    }
    if (url.endsWith('/api/v1/stokvels/B/cycles')) {
      return new Response(
        JSON.stringify([
          { id: 'cycle-B', stokvelId: 'B', label: 'CYCLE-B', targetAmount: 500, status: 'Open', endsOn: '2099-12-31', collectedCents: 2222, dueCents: 0 },
        ]),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      )
    }
    if (url.endsWith('/api/v1/stokvels/A/contributions')) {
      return new Response(
        JSON.stringify({
          items: [
            { id: 'a1', stokvelId: 'A', userId: 'u', contributionCycleId: 'cycle-A', amountCents: 1111, recordedAt: '2026-10-01T00:00:00Z', cycleLabel: 'CYCLE-A' },
          ],
          nextPageToken: null,
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      )
    }
    if (url.endsWith('/api/v1/stokvels/B/contributions')) {
      await new Promise((r) => setTimeout(r, 300))
      return new Response(
        JSON.stringify({
          items: [
            { id: 'b1', stokvelId: 'B', userId: 'u', contributionCycleId: 'cycle-B', amountCents: 2222, recordedAt: '2026-10-02T00:00:00Z', cycleLabel: 'CYCLE-B' },
          ],
          nextPageToken: null,
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      )
    }
    return new Response(null, { status: 404 })
  })
}

function renderWithQuery(ui: React.ReactNode) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>)
}

describe('stokvel switch', () => {
  it('does not show the previous stokvel’s contributions after the switch', async () => {
    vi.stubGlobal('fetch', makeFakeApi())
    const user = userEvent.setup()

    renderWithQuery(<HomePage />)

    // Stokvel A's cycle is "Open", so its group starts expanded automatically.
    // The contribution amount should appear without needing to click anything.
    await screen.findByText(/Cycle CYCLE-A/i)
    expect(screen.getByText('R 11,11')).toBeInTheDocument()
    expect(screen.queryByText('R 22,22')).toBeNull()

    // Switch to B.
    await user.click(screen.getByRole('button', { name: 'Stokvel B' }))

    // Immediately after the click, A's data must be gone — no flicker.
    // This is the exact bug that keepPreviousData introduces.
    expect(screen.queryByText('R 11,11')).toBeNull()
    expect(screen.queryByText(/Cycle CYCLE-A/i)).toBeNull()

    // Then B's data appears when its slow response arrives.
    await screen.findByText(/Cycle CYCLE-B/i, undefined, { timeout: 2000 })
    expect(screen.getByText('R 22,22')).toBeInTheDocument()
  })
})