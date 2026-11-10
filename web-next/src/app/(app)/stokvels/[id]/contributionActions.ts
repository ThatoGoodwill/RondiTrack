 'use server'
import { refresh } from 'next/cache'
import { z } from 'zod'
import { getSession } from '@/lib/session'
import { apiBase } from '@/lib/serverApi'
import { parseRandsToCents } from '@/lib/money'
import { describeError, errorFromResponse } from '@/lib/errorMessages'

export type ContributionActionState = {
  key: string
  lastIntent: string
  attempt: number
  values: { amount: string; memberId: string }
  message: string
  reference: string | null
  fieldErrors: { amount?: string; memberId?: string }
  success: boolean
}
const requestSchema = z.object({ stokvelId: z.string().uuid(), cycleId: z.string().uuid(),
  memberId: z.string().uuid(), amount: z.string().min(1) })
export async function recordContributionAction(previous: ContributionActionState, data: FormData): Promise<ContributionActionState> {
  const session = await getSession()
  const values = { amount: String(data.get('amount') ?? ''), memberId: String(data.get('memberId') ?? '') }
  const base = { ...previous, attempt: previous.attempt + 1, values, success: false, message: '', reference: null, fieldErrors: {} }
  if (!session) return { ...base, message: 'Session ended. Please sign in again.' }
  const parsed = requestSchema.safeParse({
    stokvelId: String(data.get('stokvelId') ?? ''), cycleId: String(data.get('cycleId') ?? ''), ...values,
  })
  if (!parsed.success) return { ...base, message: 'Please complete the form correctly.' }
  let amountInCents: number
  try {
    amountInCents = parseRandsToCents(values.amount)
    if (amountInCents <= 0) throw new Error('Non-positive amount')
  } catch { return { ...base, fieldErrors: { amount: 'Enter a positive amount with at most two decimal places.' } } }
  // The server chooses a new key only after success or a changed payment intent.
  // Unchanged retry after unknown outcome, 5xx or 429 preserves the old key.
  const intent = JSON.stringify([parsed.data.stokvelId, parsed.data.cycleId, parsed.data.memberId, amountInCents])
  const key = previous.lastIntent && previous.lastIntent !== intent ? crypto.randomUUID() : previous.key
  const pending = { ...base, key, lastIntent: intent }
  let response: Response
  try {
    response = await fetch(`${apiBase()}/api/v1/stokvels/${encodeURIComponent(parsed.data.stokvelId)}/cycles/${encodeURIComponent(parsed.data.cycleId)}/contributions`, {
      method: 'POST', headers: { Authorization: `Bearer ${session.accessToken}`,
        'Content-Type': 'application/json', 'Idempotency-Key': key },
      body: JSON.stringify({ userId: parsed.data.memberId, amountInCents }), cache: 'no-store',
    })
  } catch {
    return { ...pending, message: 'Connection dropped. This payment may have gone through. Retrying is safe.' }
  }
  if (response.ok) {
    // The contribution is definitive only after the API confirms it (including replayed 200).
    refresh() // uncached Server Components return the new cycle total in this response
    return { ...pending, key: crypto.randomUUID(), lastIntent: '', success: true,
      values: { amount: '', memberId: parsed.data.memberId },
      message: response.headers.get('Idempotency-Replayed') === 'true'
        ? `Already recorded for this cycle (${amountInCents} cents).`
        : `Recorded ${amountInCents} cents for this cycle.`,
    }
  }
  if (response.status === 401)
    return { ...pending, message: 'Your session has ended. Sign in again.' }
  const error = await errorFromResponse(response)
  const details = describeError(error)
  const problems = error.problem?.errors ?? {}
  const extra: string[] = []
  const fieldErrors: ContributionActionState['fieldErrors'] = {}
  for (const [field, messages] of Object.entries(problems)) {
    const name = field.toLowerCase()
    if (name === 'amount' || name === 'amountincents') fieldErrors.amount = messages.join(' ')
    else if (name === 'memberid' || name === 'userid') fieldErrors.memberId = messages.join(' ')
    else extra.push(`${field}: ${messages.join(' ')}`)
  }
  return { ...pending, message: [details.message, details.advice, ...extra].join(' '),
    reference: details.reference, fieldErrors }
}
