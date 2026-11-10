 'use client'
import { useActionState, useEffect, useRef } from 'react'
import { recordContributionAction, type ContributionActionState } from './contributionActions'
import type { Membership } from '@/lib/types'
import { centsToInput, formatCents } from '@/lib/money'

export function ContributionForm({ stokvelId, cycleId, cycleLabel, role, ownId, owedCents,
  members, initialKey }: {
  stokvelId: string; cycleId: string; cycleLabel: string; role: string; ownId: string; owedCents: number
  members: Membership[]; initialKey: string
}) {
  const isTreasurer = role === 'Treasurer' || role === 'Admin'
  const initial: ContributionActionState = {
    key: initialKey, lastIntent: '', attempt: 0, success: false, message: '', reference: null,
    values: { amount: centsToInput(owedCents), memberId: ownId }, fieldErrors: {},
  }
  const [state, action, pending] = useActionState(recordContributionAction, initial)
  const lastNotice = useRef(0)
  useEffect(() => {
    if (state.success && state.attempt !== lastNotice.current) {
      lastNotice.current = state.attempt
      window.dispatchEvent(new Event('rondi:contribution-recorded'))
    }
  }, [state.success, state.attempt])
  return <form action={action} key={state.attempt} className="card space-y-3" aria-label="Record a contribution">
    <h2 className="font-semibold">Record contribution · {cycleLabel}</h2>
    <p className="text-sm muted">Amount still owed by you: {formatCents(owedCents)}</p>
    <input type="hidden" name="stokvelId" value={stokvelId} />
    <input type="hidden" name="cycleId" value={cycleId} />
    <input type="hidden" name="idempotencyKey" value={state.key} />
    {state.message && <div role={state.success ? 'status' : 'alert'} className="text-sm">
      {state.message}{state.reference && <p>Reference: <code>{state.reference}</code></p>}
    </div>}
    {isTreasurer ? <div><label htmlFor="contribMember" className="label">Paying member</label>
      <select className="input" id="contribMember" name="memberId" defaultValue={state.values.memberId}>
        {members.map(m => <option key={m.userId} value={m.userId}>
          {m.userId === ownId ? 'You' : `Member ${m.userId.slice(0, 8)}`}
        </option>)}
      </select>{state.fieldErrors.memberId && <p role="alert" className="text-red-700">{state.fieldErrors.memberId}</p>}
    </div> : <input type="hidden" name="memberId" value={ownId} />}
    <div><label className="label" htmlFor="contribAmount">Amount in rands</label>
      <input className="input" id="contribAmount" name="amount" inputMode="decimal"
        defaultValue={state.values.amount} required aria-invalid={!!state.fieldErrors.amount} />
      {state.fieldErrors.amount && <p role="alert" className="text-red-700">{state.fieldErrors.amount}</p>}
    </div>
    <button type="submit" className="btn" disabled={pending}>{pending ? 'Saving…' : 'Record contribution'}</button>
  </form>
}
