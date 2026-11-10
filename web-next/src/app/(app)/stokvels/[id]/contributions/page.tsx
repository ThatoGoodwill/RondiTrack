import { Suspense } from 'react'
import Link from 'next/link'
import { notFound, redirect } from 'next/navigation'
import { apiForSession, unwrapApi } from '@/lib/serverApi'
import { fetchHistory } from '@/lib/historyData'
import { loadHistoryFilters, normalizedFilters } from '@/lib/historyFilters'
import { HistoryBrowser } from './HistoryBrowser'
import type { Stokvel, Membership, Page } from '@/lib/types'
async function HistoryData({ id, searchParams }: { id: string; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const filters = normalizedFilters(loadHistoryFilters(await searchParams))
  const api = await apiForSession()
  const [detail, memberships, history] = await Promise.all([
    api.GET('/api/v1/stokvels/{stokvelId}', { params: { path: { stokvelId: id } } }),
    api.GET('/api/v1/stokvels/{stokvelId}/members', { params: { path: { stokvelId: id }, query: { pageSize: 100 } } }),
    fetchHistory(id, filters),
  ])
  const stokvel = unwrapApi<Stokvel>(detail)
  const members = unwrapApi<Page<Membership>>(memberships)
  if (history.response.status === 401) redirect('/login?expired=1')
  if (history.response.status === 404) notFound()
  if (!history.response.ok || !history.data) throw new Error(`Could not load contribution history (${history.response.status}).`)
  return <section className="space-y-4">
    <Link className="a text-sm" href={`/stokvels/${id}`}>Back to {stokvel.name}</Link>
    <h1 className="text-2xl font-bold">Contribution history</h1>
    <HistoryBrowser stokvelId={id} filters={filters} firstPage={history.data} members={members.items} />
  </section>
}
async function HistoryParams({ params, searchParams }: {
  params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>>
}) {
  const { id } = await params
  return <HistoryData id={id} searchParams={searchParams} />
}
export default function Page({ params, searchParams }: {
  params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>>
}) {
  return <Suspense fallback={<div className="card animate-pulse">Loading contribution history…</div>}>
    <HistoryParams params={params} searchParams={searchParams} />
  </Suspense>
}
