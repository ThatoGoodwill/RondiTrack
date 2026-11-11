'use server'

import { redirect } from 'next/navigation'
import { apiRequest } from '@/lib/serverApi'
import { errorFromResponse, describeError } from '@/lib/errorMessages'

export type JoinState = { message: string; reference: string | null }
export async function joinInviteAction(_: JoinState, data: FormData): Promise<JoinState> {
  const code = String(data.get('code') ?? '').toLowerCase()
  if (!/^[0-9a-f]{48}$/.test(code))
    return { message: 'Check the invite code and try again.', reference: null }
  let stokvelId: string | null = null
  try {
    const response = await apiRequest(`/api/v1/invites/${code}/join`, { method: 'POST' })
    if (!response.ok) {
      const desc = describeError(await errorFromResponse(response))
      return { message: desc.message, reference: desc.reference }
    }
    const result = await response.json() as { stokvelId: string; alreadyMember: boolean }
    stokvelId = result.stokvelId
  } catch { return { message: 'Could not join just now. Try again safely.', reference: null } }
  redirect(`/stokvels/${stokvelId}`)
}
