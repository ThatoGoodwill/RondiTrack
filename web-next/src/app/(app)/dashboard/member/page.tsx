import { Suspense } from 'react'
import Link from 'next/link'
import { apiForSession, unwrapApi } from '@/lib/serverApi'
import type { Home } from '@/lib/types'
import { formatCents } from '@/lib/money'
async function MemberDashboard() {
  const api = await apiForSession()
  const home = unwrapApi<Home>(await api.GET('/api/v1/me/home'))
  return <section className="space-y-4"><h1 className="text-2xl font-bold">Member dashboard</h1>
    {home.stokvels.length === 0 ? <div className="card">You do not belong to a stokvel yet. Ask a treasurer to invite you.</div> :
      <div className="grid gap-3 sm:grid-cols-2">{home.stokvels.map(s => <article className="card space-y-2" key={s.stokvelId}>
        <h2 className="font-semibold"><Link className="a" href={`/stokvels/${s.stokvelId}`}>{s.name}</Link></h2>
        <p className="text-xs muted">Role here: {s.role}</p>
        {s.currentCycle ? <><p>Paid: <strong>{formatCents(s.currentCycle.paidCents)}</strong></p>
          <p>Still owed: <strong>{formatCents(s.currentCycle.owedCents)}</strong></p>
          <p>Cycle: {s.currentCycle.label}</p></> : <p>No open cycle.</p>}
        <p className="text-sm">Next payout: {s.nextPayoutDueUtc ? new Date(s.nextPayoutDueUtc).toLocaleDateString('en-ZA', { timeZone: 'Africa/Johannesburg' }) : 'Not scheduled'}</p>
      </article>)}</div>}
  </section>
}
export default function Page() { return <Suspense fallback={<div className="card">Loading member dashboard…</div>}><MemberDashboard /></Suspense> }
