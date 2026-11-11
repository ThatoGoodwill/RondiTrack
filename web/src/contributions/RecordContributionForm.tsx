import { useRef, useState, type FormEvent } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../auth/session'
import { describeError } from '../lib/errorMessages'
import { parseRandsToCents, centsToInput, formatCents } from '../lib/money'
import { queryKeys } from '../api/queries'
import { ContributionIntentKey } from './intent'
import { recordContribution } from './recordContribution'

const formSchema = z.object({
  amount: z.string().refine(v => {
    try { return parseRandsToCents(v) > 0 } catch { return false }
  }, 'Enter a positive amount with at most two decimal places.'),
  memberId: z.string().uuid('Choose a member.'),
})
type Values = z.infer<typeof formSchema>
type Props = {
  stokvelId: string; cycleId: string; cycleLabel: string; status: string
  ownId: string; role: string; owedCents: number
  members: { userId: string; label: string }[]
}
export function RecordContributionForm(props: Props) {
  const { stokvelId, cycleId, cycleLabel, status, ownId, role, owedCents, members } = props
  const isTreasurer = role === 'Treasurer' || role === 'Admin'
  const [keys] = useState(() => new ContributionIntentKey())
  const sendingRef = useRef(false)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const [failure, setFailure] = useState('')
  const qc = useQueryClient()
  const mutation = useMutation({ mutationFn: recordContribution })
  const { register, handleSubmit, reset, setError, formState: { errors } } = useForm<Values>({
    resolver: zodResolver(formSchema),
    defaultValues: { amount: centsToInput(owedCents), memberId: ownId },
  })
  const onValid = async (values: Values) => {
    if (sendingRef.current) return // synchronous guard against two clicks in one event loop
    sendingRef.current = true
    setBusy(true); setFailure(''); setNotice('')
    const amountInCents = parseRandsToCents(values.amount)
    const memberId = isTreasurer ? values.memberId : ownId
    const idempotencyKey = keys.current({ stokvelId, cycleId, memberId, amountInCents })
    try {
      await mutation.mutateAsync({ stokvelId, cycleId, memberId, amountInCents, idempotencyKey })
      await Promise.all([
        qc.invalidateQueries({ queryKey: queryKeys.home }),
        qc.invalidateQueries({ queryKey: queryKeys.contributions(stokvelId) }),
        qc.invalidateQueries({ queryKey: queryKeys.cycles(stokvelId) }),
      ])
      keys.confirmRecorded()
      reset({ amount: centsToInput(owedCents), memberId: ownId })
      const who = members.find(m => m.userId === memberId)?.label ?? 'you'
      setNotice(`Recorded ${formatCents(amountInCents)} for ${who} in ${cycleLabel}.`)
    } catch (err) {
      if (err instanceof ApiError) {
        const problem = err.body as { errors?: Record<string, string[]>; detail?: string; correlationId?: string } | null
        const extras: string[] = []
        for (const [field, messages] of Object.entries(problem?.errors ?? {})) {
          const name = field.toLowerCase()
          if (name === 'amount' || name === 'amountincents')
            setError('amount', { type: 'server', message: messages.join(' ') })
          else if (name === 'userid' || name === 'memberid')
            setError('memberId', { type: 'server', message: messages.join(' ') })
          else extras.push(`${field}: ${messages.join(' ')}`)
        }
        const info = describeError(err)
        setFailure([err.status === 409 || err.status === 422 ? problem?.detail : info.message,
          ...extras, info.correlationId ? `Reference: ${info.correlationId}` : 'Reference unavailable'].filter(Boolean).join(' '))
      } else {
        setFailure('Could not reach RondiTrack. Your payment may have gone through. Retry safely with the same key. Reference unavailable.')
      }
      // Keep the same key after a network error, 5xx, 429, 401 or unchanged validation failure.
      // The key changes automatically if amount, member or cycle changes.
    } finally { sendingRef.current = false; setBusy(false) }
  }
  const submit = (event: FormEvent<HTMLFormElement>) => { void handleSubmit(onValid)(event) }
  if (status !== 'Open') return <p>This cycle is {status}. Contributions cannot be recorded.</p>
  return <form onSubmit={submit} className="space-y-3 rounded-lg border p-4" aria-label="Record contribution">
    <h3 className="font-semibold">Record contribution · {cycleLabel}</h3>
    {failure && <p role="alert" className="text-sm text-red-700">{failure}</p>}
    {notice && <p role="status" className="text-sm text-green-700">{notice}</p>}
    {isTreasurer && <label className="block text-sm">Who paid?
      <select className="mt-1 block w-full rounded border p-2" {...register('memberId')}>
        {members.map(m => <option key={m.userId} value={m.userId}>{m.label}</option>)}
      </select>
      {errors.memberId && <span role="alert">{errors.memberId.message}</span>}
    </label>}
    <label className="block text-sm">Amount in rands
      <input className="mt-1 block w-full rounded border p-2" inputMode="decimal" autoComplete="off" {...register('amount')} />
      {errors.amount && <span role="alert">{errors.amount.message}</span>}
    </label>
    <button type="submit" disabled={busy || mutation.isPending} className="rounded bg-primary px-4 py-2 text-primary-foreground disabled:opacity-50">
      {busy ? 'Saving…' : 'Record contribution'}
    </button>
  </form>
}
