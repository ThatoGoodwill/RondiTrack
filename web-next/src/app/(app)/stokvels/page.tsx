import { Suspense } from 'react'
import Link from 'next/link'
import { apiForSession, unwrapApi } from '@/lib/serverApi'
import type { Home } from '@/lib/types'
async function Stokvels() {
  const api = await apiForSession()
  const home = unwrapApi<Home>(await api.GET('/api/v1/me/home'))
  return <section className="space-y-4"><h1 className="text-2xl font-bold">My stokvels</h1>
    {home.stokvels.length === 0 ? <div className="card">You have not joined any stokvels yet.</div> :
      <ul className="grid gap-3 sm:grid-cols-2">{home.stokvels.map(s =>
        <li className="card" key={s.stokvelId}>
          <Link className="a font-semibold" href={`/stokvels/${s.stokvelId}`}>{s.name}</Link>
          <p className="text-sm muted">Your role in this stokvel: {s.role}</p>
        </li>)}</ul>}
  </section>
}
export default function Page() { return <Suspense fallback={<div className="card">Loading your stokvels…</div>}><Stokvels /></Suspense> }
