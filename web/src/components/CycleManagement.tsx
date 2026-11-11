import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authFetch, ApiError } from '../auth/session'
import { queryKeys } from '../api/queries'
import { centsToInput, parseRandsToCents } from '../lib/money'

type Props = { stokvelId: string; defaultAmountCents: number; hasOpenCycle: boolean }
export function CycleManagement({ stokvelId, defaultAmountCents, hasOpenCycle }: Props) {
  const [formOpen, setFormOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [invite, setInvite] = useState('')
  const qc = useQueryClient()
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'
  async function createCycle(data: FormData) {
    setBusy(true); setMessage('')
    try {
      const label = String(data.get('label') ?? '').trim()
      const amount = parseRandsToCents(String(data.get('amount') ?? ''))
      const endsOn = String(data.get('endsOn') ?? '')
      if (!label || amount <= 0 || !/^\d{4}-\d{2}-\d{2}$/.test(endsOn)) throw new Error('Enter valid cycle details.')
      const response = await authFetch(`${base}/api/v1/stokvels/${stokvelId}/cycles`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ label, targetAmount: Number(centsToInput(amount)), endsOn }),
      })
      if (!response.ok) {
        const body = await response.json().catch(() => null) as { detail?: string; correlationId?: string } | null
        throw new ApiError(response.status, body, response.headers.get('Retry-After'))
      }
      await Promise.all([qc.invalidateQueries({ queryKey: queryKeys.cycles(stokvelId) }),
        qc.invalidateQueries({ queryKey: queryKeys.home })])
      setMessage('Cycle opened. Members may now record contributions.')
      setFormOpen(false)
    } catch (error) {
      setMessage(error instanceof ApiError ?
        `${(error.body as { detail?: string } | null)?.detail ?? 'The API refused the cycle.'}` :
        error instanceof Error ? error.message : 'Unable to open cycle.')
    } finally { setBusy(false) }
  }
  async function createInvite() {
    setBusy(true); setMessage('')
    try {
      const response = await authFetch(`${base}/api/v1/stokvels/${stokvelId}/invites`, { method: 'POST' })
      if (!response.ok) throw new Error('Could not create invite.')
      const data = await response.json() as { joinPath: string }
      setInvite(window.location.origin + data.joinPath)
      setMessage('Private invite created. Share it with the member you want to invite.')
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Invite failed.') }
    finally { setBusy(false) }
  }
  return <section className="space-y-3 rounded-lg border p-4" aria-label="Treasurer actions">
    <h3 className="font-semibold">Treasurer actions</h3>
    <div className="flex flex-wrap gap-2">
      {!hasOpenCycle && <button className="rounded border px-3 py-2" type="button" onClick={() => setFormOpen(!formOpen)}>
        {formOpen ? 'Cancel' : 'Open a new cycle'}</button>}
      <button className="rounded border px-3 py-2" type="button" disabled={busy} onClick={createInvite}>Generate invite</button>
    </div>
    {formOpen && <form action={createCycle} className="space-y-2">
      <label className="block text-sm">Cycle label<input className="mt-1 block w-full rounded border p-2" name="label" required /></label>
      <label className="block text-sm">Amount per member (rands)
        <input className="mt-1 block w-full rounded border p-2" name="amount" inputMode="decimal" defaultValue={centsToInput(defaultAmountCents)} required /></label>
      <label className="block text-sm">End date (South African time)<input className="mt-1 block w-full rounded border p-2" name="endsOn" type="date" required /></label>
      <button className="rounded bg-primary px-3 py-2 text-primary-foreground" type="submit" disabled={busy}>Open cycle</button>
    </form>}
    {invite && <p className="break-all text-sm">Invite URL: <a href={invite}>{invite}</a></p>}
    {message && <p role="status" className="text-sm">{message}</p>}
  </section>
}
