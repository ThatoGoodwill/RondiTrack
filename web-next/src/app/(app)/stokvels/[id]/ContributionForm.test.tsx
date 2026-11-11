// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, cleanup } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ContributionForm } from './ContributionForm'
import type { ContributionActionState } from './contributionActions'

const recorded = vi.hoisted(() => vi.fn())
vi.mock('./contributionActions', () => ({
  recordContributionAction: (...args: unknown[]) => recorded(...args),
}))
const ownId = '10000000-0000-4000-8000-000000000001'
const stokvelId = '20000000-0000-4000-8000-000000000001'
const params = {
  stokvelId, cycleId: '30000000-0000-4000-8000-000000000001',
  cycleLabel: '2026-11', role: 'Member', ownId, owedCents: 15050,
  members: [{ userId: ownId, role: 'Member', stokvelId, joinedAtUtc: '2026-11-01T00:00:00Z' }],
  initialKey: 'idempotency-key-1',
}
describe('recording a contribution safely', () => {
  beforeEach(() => { recorded.mockReset(); cleanup() })
  it('does not expose a member picker to an ordinary member', () => {
    render(<ContributionForm {...params} />)
    expect(screen.queryByRole('combobox', { name: 'Paying member' })).toBeNull()
  })
  it('shows a member picker to a treasurer', () => {
    render(<ContributionForm {...params} role="Treasurer" />)
    expect(screen.getByRole('combobox', { name: 'Paying member' })).toBeTruthy()
  })
  it('keeps idempotency key after failure; resets only after success', async () => {
    const user = userEvent.setup()
    recorded.mockImplementationOnce(async (previous: ContributionActionState, data: FormData) => ({
      ...previous, attempt: previous.attempt + 1, success: false,
      message: 'Connection dropped; retry safely.',
      values: { amount: String(data.get('amount')), memberId: String(data.get('memberId')) },
    }))
    recorded.mockImplementationOnce(async (previous: ContributionActionState) => ({
      ...previous, attempt: previous.attempt + 1, success: true, key: 'new-key',
      message: 'Recorded R 150,50', values: { amount: '', memberId: ownId },
    }))
    render(<ContributionForm {...params} />)
    await user.click(screen.getByRole('button', { name: 'Record contribution' }))
    await screen.findByText(/Connection dropped/)
    expect((screen.getByRole('textbox', { name: 'Amount in rands' }) as HTMLInputElement).value).toBe('150.50')
    await user.click(screen.getByRole('button', { name: 'Record contribution' }))
    await screen.findByText(/Recorded R 150,50/)
    const calls = recorded.mock.calls
    expect(calls).toHaveLength(2)
    expect((calls[0][1] as FormData).get('idempotencyKey')).toBe('idempotency-key-1')
    expect((calls[1][1] as FormData).get('idempotencyKey')).toBe('idempotency-key-1')
    await expect(recorded.mock.results[1].value as Promise<ContributionActionState>).resolves.toMatchObject({ key: 'new-key' })
  })
  it('disables submit during a pending Server Action', async () => {
    const user = userEvent.setup()
    let reply!: (state: ContributionActionState) => void
    recorded.mockImplementationOnce(() => new Promise<ContributionActionState>(resolve => { reply = resolve }))
    render(<ContributionForm {...params} />)
    await user.click(screen.getByRole('button', { name: 'Record contribution' }))
    expect(screen.getByRole('button', { name: 'Saving…' }).hasAttribute('disabled')).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Saving…' }))
    expect(recorded).toHaveBeenCalledTimes(1)
    reply({ key: 'next-key', lastIntent: '', attempt: 1, success: true, message: 'Saved',
      reference: null, fieldErrors: {}, values: { amount: '', memberId: ownId } })
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Saved'))
  })
})
