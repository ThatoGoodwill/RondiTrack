'use server'

import { getSession } from '@/lib/session'
import { apiBase } from '@/lib/serverApi'
import { errorFromResponse, describeError } from '@/lib/errorMessages'

export type ProofState = { attempt: number; success: boolean; message: string; reference: string | null }
export async function uploadProofAction(previous: ProofState, data: FormData): Promise<ProofState> {
  const state: ProofState = { attempt: previous.attempt + 1, success: false, message: '', reference: null }
  const session = await getSession()
  if (!session) return { ...state, message: 'Sign in again to upload a document.' }
  const stokvelId = String(data.get('stokvelId') ?? '')
  const contributionId = String(data.get('contributionId') ?? '')
  const file = data.get('file')
  if (!/^[a-f\d-]{36}$/i.test(stokvelId) || !/^[a-f\d-]{36}$/i.test(contributionId))
    return { ...state, message: 'Invalid contribution.' }
  if (!(file instanceof File) || !file.size || file.size > 5 * 1024 * 1024)
    return { ...state, message: 'Choose an image or PDF up to 5 MB.' }
  if (!['application/pdf', 'image/png', 'image/jpeg'].includes(file.type))
    return { ...state, message: 'Choose a PDF, PNG or JPEG image.' }
  const body = new FormData()
  body.set('file', file)
  try {
    const response = await fetch(`${apiBase()}/api/v1/stokvels/${stokvelId}/contributions/${contributionId}/proof`, {
      method: 'PUT', headers: { Authorization: `Bearer ${session.accessToken}` },
      body, cache: 'no-store',
    })
    if (!response.ok) {
      const info = describeError(await errorFromResponse(response))
      return { ...state, message: info.message, reference: info.reference }
    }
    return { ...state, success: true, message: 'Proof of payment uploaded.' }
  } catch { return { ...state, message: 'Upload failed. Retry, or contact support if it continues.' } }
}
