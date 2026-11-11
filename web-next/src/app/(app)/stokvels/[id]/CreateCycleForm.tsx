'use client'

import { useActionState } from 'react'
import { openCycleAction, type OpenCycleState } from './openCycleAction'

export function CreateCycleForm({ stokvelId, initialTarget, initialLabel }: {
  stokvelId: string
  initialTarget: string
  initialLabel: string
}) {
  const [state, action, pending] = useActionState(openCycleAction, {
    message: '', success: false, label: initialLabel, amount: initialTarget,
  } satisfies OpenCycleState)
  return <form action={action} className="card space-y-3" aria-label="Open a contribution cycle">
    <h2 className="font-semibold">Open a contribution cycle</h2>
    <p className="text-sm muted">There is no open cycle. Open one to allow members to record payments.</p>
    <input type="hidden" name="stokvelId" value={stokvelId} />
    {state.message && <p role={state.success ? 'status' : 'alert'}>{state.message}</p>}
    <div>
      <label htmlFor="newCycleLabel" className="label">Cycle label</label>
      <input id="newCycleLabel" className="input" name="label" maxLength={40} defaultValue={state.label} required />
    </div>
    <div>
      <label htmlFor="newCycleTarget" className="label">Contribution target (rands)</label>
      <input id="newCycleTarget" className="input" name="amount" inputMode="decimal" defaultValue={state.amount} required />
    </div>
    <button type="submit" className="btn" disabled={pending}>{pending ? 'Opening…' : 'Open cycle'}</button>
  </form>
}
