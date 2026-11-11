import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from '../api/client'
import { queryKeys } from '../api/queries'
import { parseRandsToCents, centsToInput } from '../lib/money'
import { describeError } from '../lib/errorMessages'

function currentSastMonth() {
  const pieces = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Africa/Johannesburg', year: 'numeric', month: '2-digit',
  }).formatToParts(new Date())
  return `${pieces.find(p => p.type === 'year')?.value}-${pieces.find(p => p.type === 'month')?.value}`
}

/** Create a cycle without resetting the database when seeded cycles have already closed. */
export function CreateCycleForm({ stokvelId, targetRands }: { stokvelId: string; targetRands: number }) {
  const qc = useQueryClient()
  const [label, setLabel] = useState(currentSastMonth)
  const [amount, setAmount] = useState(String(targetRands))
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const create = useMutation({
    mutationFn: async () => {
      const cents = parseRandsToCents(amount)
      if (cents <= 0 || cents > 100_000_000) throw new Error('Enter an amount between R 0,01 and R 1 000 000,00.')
      if (!label.trim() || label.trim().length > 40) throw new Error('Enter a cycle name of 1 to 40 characters.')
      return unwrap(api.POST('/api/v1/stokvels/{stokvelId}/cycles', {
        params: { path: { stokvelId } },
        body: { label: label.trim(), targetAmount: Number(centsToInput(cents)) },
      }))
    },
  })
  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (create.isPending) return
    setError(''); setNotice('')
    try {
      await create.mutateAsync()
      await Promise.all([
        qc.invalidateQueries({ queryKey: queryKeys.cycles(stokvelId) }),
        qc.invalidateQueries({ queryKey: queryKeys.home }),
      ])
      setNotice('The new cycle is open. You can now record contributions.')
    } catch (reason) {
      setError(reason instanceof Error && !(reason as { status?: number }).status
        ? reason.message
        : describeError(reason).message)
    }
  }
  return <form onSubmit={submit} className="space-y-3 rounded-lg border p-4" aria-label="Open a contribution cycle">
    <h3 className="font-semibold">Open a contribution cycle</h3>
    <p className="text-sm text-muted-foreground">Your previous cycles are closed. Open another cycle to accept payments; existing payment history is preserved.</p>
    {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
    {notice && <p role="status" className="text-sm text-green-700">{notice}</p>}
    <label className="block text-sm">Cycle label
      <input className="mt-1 block w-full rounded border p-2" value={label} maxLength={40} required onChange={event => setLabel(event.target.value)} />
    </label>
    <label className="block text-sm">Contribution target (rands)
      <input className="mt-1 block w-full rounded border p-2" inputMode="decimal" value={amount} required onChange={event => setAmount(event.target.value)} />
    </label>
    <button className="rounded bg-primary px-4 py-2 text-primary-foreground disabled:opacity-50" disabled={create.isPending}>
      {create.isPending ? 'Opening cycle…' : 'Open cycle'}
    </button>
  </form>
}
