'use server'

import { refresh } from 'next/cache'
import { apiRequest } from '@/lib/serverApi'
import { parseRandsToCents, centsToInput } from '@/lib/money'
import { errorFromResponse, describeError } from '@/lib/errorMessages'

export type ResultMessage = { ok: boolean; message: string; reference?: string | null }
export type InviteResult = ResultMessage & { path?: string }
export type CloseMember = { userId: string; displayName: string; arrearsCents: number }
export type ClosePreview = { cycleId: string; label: string; asOfSouthAfricanDate: string;
  version: string; totalArrearsCents: number; members: CloseMember[] }

async function failure(response: Response): Promise<ResultMessage> {
  const info = describeError(await errorFromResponse(response))
  return { ok: false, message: `${info.message} ${info.advice}`.trim(), reference: info.reference }
}
export async function openCycleAction(stokvelId: string, label: string,
  amount: string, endsOn: string): Promise<ResultMessage> {
  if (!/^[0-9a-f-]{36}$/i.test(stokvelId) || !label.trim() || !/^\d{4}-\d{2}-\d{2}$/.test(endsOn))
    return { ok: false, message: 'Enter a cycle label and valid South African end date.' }
  let cents: number
  try { cents = parseRandsToCents(amount); if (cents <= 0) throw Error() }
  catch { return { ok: false, message: 'Enter a positive contribution amount in rands.' } }
  try {
    const response = await apiRequest(`/api/v1/stokvels/${stokvelId}/cycles`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ label: label.trim(), targetAmount: Number(centsToInput(cents)), endsOn }),
    })
    if (!response.ok) return failure(response)
    refresh()
    return { ok: true, message: `Cycle ${label} opened. Members can now contribute.` }
  } catch { return { ok: false, message: 'Unable to reach RondiTrack. Check the API connection.' } }
}

export async function createInviteAction(stokvelId: string): Promise<InviteResult> {
  try {
    const response = await apiRequest(`/api/v1/stokvels/${stokvelId}/invites`, { method: 'POST' })
    if (!response.ok) return failure(response)
    const data = await response.json() as { joinPath: string }
    return { ok: true, message: 'Invite generated. Share the link privately.', path: data.joinPath }
  } catch { return { ok: false, message: 'Could not create an invite. Check the connection.' } }
}

export async function getClosePreviewAction(stokvelId: string, cycleId: string): Promise<
  { ok: true; preview: ClosePreview } | ({ ok: false } & ResultMessage)> {
  try {
    const response = await apiRequest(`/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/close-preview`)
    if (!response.ok) return { ...await failure(response), ok: false }
    return { ok: true, preview: await response.json() as ClosePreview }
  } catch { return { ok: false, message: 'Could not load close preview.' } }
}

export async function confirmCloseAction(stokvelId: string, cycleId: string,
  previewVersion: string, idempotencyKey: string): Promise<ResultMessage & { stale?: boolean }> {
  try {
    const response = await apiRequest(`/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/close`, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ previewVersion }),
    })
    if (!response.ok) return { ...await failure(response), stale: response.status === 409 }
    refresh()
    return { ok: true, message: 'Cycle closed and arrears recorded exactly once.' }
  } catch { return { ok: false, message: 'The request may have succeeded. Retrying with the same key is safe.' } }
}
