import { Suspense } from 'react'
import Link from 'next/link'
import { apiForSession, unwrapApi } from '@/lib/serverApi'
import { stokvelsWhereTreasurer } from '@/lib/treasurer'
import type { Home } from '@/lib/types'
async function TreasurerDashboard() {
  const api = await apiForSession()
  const home = unwrapApi<Home>(await api.GET('/api/v1/me/home'))
  const managed = stokvelsWhereTreasurer(home.stokvels)
  return <section className="space-y-4"><h1 className="text-2xl font-bold">Treasurer dashboard</h1>
    {managed.length === 0 ? <div className="card">You do not run any stokvels yet. <Link className="a" href="/dashboard/member">Go to your member dashboard</Link>.</div> :
      <ul className="grid gap-3 sm:grid-cols-2">{managed.map(s => <li className="card" key={s.stokvelId}>
        <Link className="a font-semibold" href={`/stokvels/${s.stokvelId}`}>{s.name}</Link>
        <p className="text-sm muted">Your role: {s.role}</p>
      </li>)}</ul>}
  </section>
}
export default function Page() { return <Suspense fallback={<div className="card">Loading treasurer dashboard…</div>}><TreasurerDashboard /></Suspense> }
