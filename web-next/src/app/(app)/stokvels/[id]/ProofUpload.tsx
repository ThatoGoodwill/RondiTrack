'use client'
import { useActionState, useState } from 'react'
import { uploadProofAction, type ProofState } from './proofActions'

const initial: ProofState = { attempt: 0, success: false, message: '', reference: null }
export function ProofUpload({ stokvelId, contributionId }: { stokvelId: string; contributionId: string }) {
  const [state, action, pending] = useActionState(uploadProofAction, initial)
  const [error, setError] = useState('')
  return <form action={action} key={state.attempt} className="my-2 space-y-2" aria-label="Upload proof of payment">
    <input type="hidden" name="stokvelId" value={stokvelId} />
    <input type="hidden" name="contributionId" value={contributionId} />
    <label className="label" htmlFor={`proof-${contributionId}`}>Attach proof (PDF, PNG or JPEG; maximum 5 MB)</label>
    <input id={`proof-${contributionId}`} type="file" name="file" accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg"
      aria-invalid={!!error || !!state.message && !state.success}
      aria-describedby={`proof-help-${contributionId}`} onChange={e => {
        const selected = e.currentTarget.files?.[0]
        setError(selected && (selected.size > 5 * 1024 * 1024 ||
          !['application/pdf', 'image/jpeg', 'image/png'].includes(selected.type))
          ? 'File must be a PDF, PNG or JPEG under 5 MB.' : '')
      }}/>
    {(error || state.message) && <p id={`proof-help-${contributionId}`} role={state.success ? 'status' : 'alert'}
      className="text-sm">{error || state.message}{state.reference && ` Reference: ${state.reference}`}</p>}
    <button className="btn" type="submit" disabled={pending || !!error}>{pending ? 'Uploading…' : 'Upload proof'}</button>
  </form>
}
