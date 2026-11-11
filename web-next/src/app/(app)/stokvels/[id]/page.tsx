import { Suspense } from 'react'
import Link from 'next/link'
import { apiForSession, unwrapApi } from '@/lib/serverApi'
import { requireSession } from '@/lib/session'
import { formatCents, parseRandsToCents } from '@/lib/money'
import { PinToggle } from '@/components/PinMenu'
import { ContributionForm } from './ContributionForm'
import { CycleControls } from './CycleControls'
import { ProofUpload } from './ProofUpload'
import type { Stokvel, Cycle, Membership, Home, Contribution, Page } from '@/lib/types'

async function RecentContributions({ result, stokvelId, ownId, manager, openCycleId }: {
  result: Promise<Page<Contribution>>; stokvelId: string; ownId: string; manager: boolean; openCycleId: string | null
}) {
  const page = await result
  return <section className="card space-y-2"><h2 className="font-semibold">Recent contributions</h2>
    {page.items.length === 0 ? <p className="muted text-sm">No contributions yet.</p> :
      <ul>{page.items.map(row => <li key={row.id} className="border-b py-2 text-sm">
        <strong>{formatCents(row.amountCents)}</strong> · {row.cycleLabel} · {new Date(row.recordedAt).toLocaleDateString('en-ZA', { timeZone: 'Africa/Johannesburg' })}
        {(manager || row.userId === ownId) && <div><a className="a" href={`/bff/stokvels/${stokvelId}/contributions/${row.id}/proof`}>Download proof (if uploaded)</a></div>}
        {row.userId === ownId && row.contributionCycleId === openCycleId &&
          <ProofUpload stokvelId={stokvelId} contributionId={row.id}/>}
      </li>)}</ul>}
  </section>
}
async function StokvelDetail({ id }: { id: string }) {
  const session = await requireSession()
  const api = await apiForSession()
  // Start the slower feed without awaiting it; a separate Suspense can stream later.
  const recent = api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
    params: { path: { stokvelId: id }, query: { pageSize: 5, sort: 'RecordedAt_desc' } },
  }).then(r => unwrapApi<Page<Contribution>>(r))
  const [stokvelResponse, cycleResponse, memberResponse, homeResponse] = await Promise.all([
    api.GET('/api/v1/stokvels/{stokvelId}', { params: { path: { stokvelId: id } } }),
    api.GET('/api/v1/stokvels/{stokvelId}/cycles', { params: { path: { stokvelId: id } } }),
    api.GET('/api/v1/stokvels/{stokvelId}/members', { params: { path: { stokvelId: id }, query: { pageSize: 100 } } }),
    api.GET('/api/v1/me/home'),
  ])
  const stokvel = unwrapApi<Stokvel>(stokvelResponse)
  const cycles = unwrapApi<Cycle[]>(cycleResponse)
  const members = unwrapApi<Page<Membership>>(memberResponse)
  const home = unwrapApi<Home>(homeResponse)
  const entry = home.stokvels.find(s => s.stokvelId === id)
  const cycle = cycles.find(item => item.status === 'Open')
  const manager = entry?.role === 'Treasurer' || entry?.role === 'Admin'
  return <div className="space-y-4"><header className="flex flex-wrap items-start justify-between gap-3">
    <div><Link className="a text-sm" href="/stokvels">All stokvels</Link><h1 className="text-2xl font-bold">{stokvel.name}</h1>
      <p className="muted text-sm">Your role: {entry?.role ?? 'Member'} · {stokvel.memberCount} members</p></div>
    <PinToggle id={id} name={stokvel.name} />
  </header>
  <section className="grid gap-3 sm:grid-cols-2"><div className="card">
    <h2 className="font-semibold">Contribution</h2><p>{String(stokvel.contributionAmount)} rand per cycle</p></div>
    <div className="card"><h2 className="font-semibold">Current cycle</h2>
      {cycle ? <><p>{cycle.label} · {cycle.status}</p><p>Collected: {formatCents(cycle.collectedCents)}</p>
        <p>Still due: {formatCents(cycle.dueCents)}</p></> : <p>No open cycle. A closed or paid-out cycle cannot accept contributions.</p>}
    </div></section>
  {manager && <CycleControls stokvelId={id} cycleId={cycle?.id} cycleLabel={cycle?.label}
    targetCents={parseRandsToCents(String(stokvel.contributionAmount))} />}
  {!cycle && !manager && <p className="card">There is no open contribution cycle. Ask your treasurer to open the next one.</p>}
  {cycle && <ContributionForm key={`${id}-${cycle.id}`} stokvelId={id} cycleId={cycle.id} cycleLabel={cycle.label}
    role={entry?.role ?? 'Member'} ownId={session.user.id} owedCents={entry?.currentCycle?.owedCents ?? 0}
    members={members.items} initialKey={crypto.randomUUID()} />}
  <Suspense fallback={<div className="card animate-pulse" role="status">Loading recent contributions…</div>}>
    <RecentContributions result={recent} stokvelId={id} ownId={session.user.id} manager={manager} openCycleId={cycle?.id ?? null} />
  </Suspense>
  <Link className="a" href={`/stokvels/${id}/contributions`}>View all contribution history and filters</Link>
  </div>
}
async function DetailParams({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <StokvelDetail id={id} />
}
export default function Page({ params }: PageProps<'/stokvels/[id]'>) {
  return <Suspense fallback={<div className="card animate-pulse">Loading stokvel details…</div>}>
    <DetailParams params={params} />
  </Suspense>
}
