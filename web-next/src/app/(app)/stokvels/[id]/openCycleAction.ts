'use server'

import { refresh } from 'next/cache'
import { getSession } from '@/lib/session'
import { apiBase } from '@/lib/serverApi'
import { parseRandsToCents, centsToInput } from '@/lib/money'

export type OpenCycleState = { message: string; success: boolean; label: string; amount: string }

export async function openCycleAction(_previous: OpenCycleState, data: FormData): Promise<OpenCycleState> {
  const label = String(data.get('label') ?? '').trim()
  const amount = String(data.get('amount') ?? '')
  const state = { label, amount, success: false, message: '' }
  const stokvelId = String(data.get('stokvelId') ?? '')
  if (!/^[a-f0-9-]{36}$/i.test(stokvelId)) return { ...state, message: 'Choose a valid stokvel.' }
  if (!label || label.length > 40) return { ...state, message: 'Cycle name must be 1 to 40 characters.' }
  let cents: number
  try { cents = parseRandsToCents(amount) } catch {
    return { ...state, message: 'Enter an amount with at most two decimal places.' }
  }
  if (cents <= 0 || cents > 100_000_000)
    return { ...state, message: 'Enter an amount between R 0,01 and R 1 000 000,00.' }
  const session = await getSession()
  if (!session) return { ...state, message: 'Session ended. Sign in again.' }
  try {
    const result = await fetch(`${apiBase()}/api/v1/stokvels/${encodeURIComponent(stokvelId)}/cycles`, {
      method: 'POST', cache: 'no-store',
      headers: { Authorization: `Bearer ${session.accessToken}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ label, targetAmount: Number(centsToInput(cents)) }),
    })
    if (!result.ok) {
      const problem = await result.json().catch(() => null) as { detail?: string; correlationId?: string } | null
      return { ...state, message: `${problem?.detail ?? 'Could not open the cycle.'}${problem?.correlationId ? ` Reference: ${problem.correlationId}` : ''}` }
    }
    refresh()
    return { ...state, success: true, message: 'Cycle opened. The contribution form will appear after the page refreshes.' }
  } catch {
    return { ...state, message: 'Could not reach RondiTrack. Check your connection before retrying.' }
  }
}
