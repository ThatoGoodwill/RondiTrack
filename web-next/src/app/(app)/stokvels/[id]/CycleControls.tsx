'use client'

import { useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { centsToInput, formatCents } from '@/lib/money'
import { openCycleAction, createInviteAction, confirmCloseAction, getClosePreviewAction,
  type ClosePreview, type ResultMessage } from './manageActions'

type Props = { stokvelId: string; cycleId?: string; cycleLabel?: string; targetCents: number }

export function CycleControls({ stokvelId, cycleId, cycleLabel, targetCents }: Props) {
  const router = useRouter()
  const [creatingCycle, setCreatingCycle] = useState(false)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<ResultMessage | null>(null)
  const [joinPath, setJoinPath] = useState('')
  const [preview, setPreview] = useState<ClosePreview | null>(null)
  const dialogRef = useRef<HTMLDialogElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const closeKey = useRef(crypto.randomUUID())
  const month = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg', year: 'numeric', month: '2-digit' }).format(new Date())
  const end = new Date(); end.setUTCMonth(end.getUTCMonth() + 1, 0)
  const defaultEnd = end.toISOString().slice(0, 10)

  async function handleCycle(form: FormData) {
    setBusy(true); setNotice(null)
    try {
      const result = await openCycleAction(stokvelId, String(form.get('label') ?? ''),
        String(form.get('amount') ?? ''), String(form.get('endsOn') ?? ''))
      setNotice(result)
      if (result.ok) { setCreatingCycle(false); router.refresh() }
    } finally { setBusy(false) }
  }
  async function invite() {
    setBusy(true)
    try {
      const result = await createInviteAction(stokvelId)
      setNotice(result)
      if (result.ok && result.path) setJoinPath(result.path)
    } finally { setBusy(false) }
  }
  async function openPreview() {
    dialogRef.current?.showModal()
    setPreview(null); setNotice(null)
    await reloadPreview()
  }
  async function reloadPreview() {
    if (!cycleId) return
    setBusy(true)
    try {
      const result = await getClosePreviewAction(stokvelId, cycleId)
      if (result.ok) { setPreview(result.preview); closeKey.current = crypto.randomUUID() }
      else setNotice(result)
    } finally { setBusy(false) }
  }
  async function confirm() {
    if (!cycleId || !preview) return
    setBusy(true)
    const result = await confirmCloseAction(stokvelId, cycleId, preview.version, closeKey.current)
    setNotice(result)
    if (result.ok) { dialogRef.current?.close(); router.refresh() }
    else if (result.stale) { setPreview(null); await reloadPreview() }
    setBusy(false)
  }
  return <section className="card space-y-3" aria-label="Treasurer actions">
    <h2 className="font-semibold">Treasurer actions</h2>
    <div className="flex flex-wrap gap-2">
      {!cycleId && <button className="btn" type="button" onClick={() => setCreatingCycle(v => !v)}>
        {creatingCycle ? 'Cancel new cycle' : 'Open new contribution cycle'}</button>}
      <button className="btn" type="button" disabled={busy} onClick={invite}>Create invite link</button>
      {cycleId && <button className="btn" type="button" ref={triggerRef} onClick={openPreview}>Close cycle safely</button>}
    </div>
    {creatingCycle && <form action={handleCycle} className="space-y-2">
      <label className="label" htmlFor="cycleLabel">Cycle label</label>
      <input className="input" id="cycleLabel" name="label" defaultValue={month} required />
      <label className="label" htmlFor="cycleAmount">Contribution per member (rands)</label>
      <input className="input" id="cycleAmount" name="amount" inputMode="decimal" defaultValue={centsToInput(targetCents)} required />
      <label className="label" htmlFor="cycleEnd">Ends on (South African date)</label>
      <input className="input" id="cycleEnd" name="endsOn" type="date" defaultValue={defaultEnd} required />
      <button className="btn" type="submit" disabled={busy}>{busy ? 'Opening…' : 'Open cycle'}</button>
    </form>}
    {joinPath && <p className="text-sm">Invite: <a className="a break-all" href={joinPath}>{joinPath}</a>
      <button className="btn ml-2" type="button" onClick={() => navigator.clipboard.writeText(location.origin + joinPath)}>Copy link</button></p>}
    {notice && <p role={notice.ok ? 'status' : 'alert'}>{notice.message}{notice.reference && ` Reference: ${notice.reference}`}</p>}
    <dialog className="m-auto w-[min(96vw,34rem)] rounded-xl border bg-white p-5 text-slate-900 shadow-xl"
      ref={dialogRef} onClose={() => triggerRef.current?.focus()} aria-labelledby="close-title">
      <h2 id="close-title" className="text-xl font-semibold">Close {cycleLabel ?? 'cycle'}?</h2>
      <p className="my-2 text-sm">This records arrears and cannot be undone.</p>
      {!preview ? <div className="space-y-2" aria-busy={busy}>
        <div className="h-12 animate-pulse rounded bg-slate-100" />
        <div className="h-12 animate-pulse rounded bg-slate-100" />
      </div> : <><p>As of {preview.asOfSouthAfricanDate} (South Africa)</p>
        <ul className="my-2 max-h-64 overflow-y-auto">{preview.members.map(m =>
          <li key={m.userId} className="border-b py-2">{m.displayName}: {formatCents(m.arrearsCents)}</li>)}</ul>
        {preview.members.length === 0 && <p>No member will go into arrears.</p>}
        <p className="font-semibold">Total arrears: {formatCents(preview.totalArrearsCents)}</p></>}
      {notice && !notice.ok && <p role="alert" className="my-2 text-red-700">{notice.message}</p>}
      <div className="mt-4 flex flex-wrap gap-3">
        <button className="btn" type="button" autoFocus onClick={() => dialogRef.current?.close()}>Cancel</button>
        <button className="btn" disabled={busy || !preview} type="button" onClick={confirm}>
          {busy ? 'Closing…' : preview ? `Close ${preview.label} and record ${formatCents(preview.totalArrearsCents)} in arrears` : 'Loading preview…'}</button>
      </div>
    </dialog>
  </section>
}
