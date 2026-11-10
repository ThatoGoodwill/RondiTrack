 'use client'
import { useEffect, useRef } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { useQueryStates } from 'nuqs'
import { historyParsers, historyKey, type HistoryFilters } from '@/lib/historyFilters'
import { displaySouthAfricanDate } from '@/lib/southAfricanDates'
import { formatCents } from '@/lib/money'
import type { Contribution, Membership, Page } from '@/lib/types'

export function HistoryBrowser({ stokvelId, filters, firstPage, members }: {
  stokvelId: string; filters: HistoryFilters; firstPage: Page<Contribution>; members: Membership[]
}) {
  const [urlFilters, update] = useQueryStates(historyParsers, {
    shallow: false, history: 'push', clearOnDefault: true,
  })
  const current: HistoryFilters = { ...urlFilters }
  const query = useInfiniteQuery({
    queryKey: historyKey(stokvelId, filters), initialPageParam: null as string | null,
    initialData: { pages: [firstPage], pageParams: [null] },
    getNextPageParam: (last: Page<Contribution>) => last.nextPageToken || undefined,
    queryFn: async ({ pageParam }) => {
      const params = new URLSearchParams({ sort: filters.sort })
      if (filters.member) params.set('member', filters.member)
      if (filters.from) params.set('from', filters.from)
      if (filters.to) params.set('to', filters.to)
      if (pageParam) params.set('pageToken', pageParam)
      const res = await fetch(`/bff/stokvels/${encodeURIComponent(stokvelId)}/contributions?${params}`, { cache: 'no-store' })
      if (!res.ok) throw new Error(`Could not load history (${res.status})`)
      return await res.json() as Page<Contribution>
    },
  })
  const pages = query.data?.pages ?? []
  const rows = pages.flatMap(p => p.items)
  const end = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const target = end.current
    if (!target || !query.hasNextPage) return
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting && query.hasNextPage && !query.isFetchingNextPage) void query.fetchNextPage()
    }, { rootMargin: '100px' })
    observer.observe(target)
    return () => observer.disconnect()
  }, [query.hasNextPage, query.isFetchingNextPage, query.fetchNextPage])
  const hasFilters = !!(current.member || current.from || current.to || current.sort !== 'newest')
  return <div className="space-y-4">
    <div className="card grid gap-3 sm:grid-cols-4">
      <div><label className="label" htmlFor="filterMember">Member</label>
        <select className="input" id="filterMember" value={current.member} onChange={e => void update({ member: e.target.value })}>
          <option value="">Everyone</option>
          {members.map(m => <option value={m.userId} key={m.userId}>Member {m.userId.slice(0, 8)}</option>)}
        </select></div>
      <div><label className="label" htmlFor="filterFrom">From (SAST)</label>
        <input className="input" type="date" id="filterFrom" value={current.from} onChange={e => void update({ from: e.target.value })} /></div>
      <div><label className="label" htmlFor="filterTo">To (SAST)</label>
        <input className="input" type="date" id="filterTo" value={current.to} onChange={e => void update({ to: e.target.value })} /></div>
      <div><label className="label" htmlFor="filterSort">Sort</label>
        <select className="input" id="filterSort" value={current.sort} onChange={e => void update({ sort: e.target.value as 'newest' | 'oldest' })}>
          <option value="newest">Newest first</option><option value="oldest">Oldest first</option>
        </select></div>
      {hasFilters && <button className="a col-span-full text-left text-sm" type="button"
        onClick={() => void update({ member: '', from: '', to: '', sort: 'newest' })}>Clear filters</button>}
    </div>
    {query.isFetching && !query.isFetchingNextPage && <p role="status">Updating filtered list…</p>}
    {query.error && <p role="alert">Could not load more contributions. Please retry.</p>}
    {rows.length === 0 ? <p className="card">No contributions match these filters. <button className="a" type="button" onClick={() => void update({ member: '', from: '', to: '', sort: 'newest' })}>Show everything</button></p> :
      <ul className="space-y-2">{rows.map(item => <li className="card text-sm" key={item.id}>
        <span className="font-semibold">{formatCents(item.amountCents)}</span> · Member {item.userId.slice(0, 8)} · {displaySouthAfricanDate(item.recordedAt)} · {item.cycleLabel}
      </li>)}</ul>}
    <div ref={end} aria-hidden="true" />
    {query.hasNextPage && <button type="button" className="btn" disabled={query.isFetchingNextPage}
      onClick={() => void query.fetchNextPage()}>{query.isFetchingNextPage ? 'Loading…' : 'Load more'}</button>}
    <p role="status" className="muted text-sm">Showing {rows.length} contributions.
      {query.hasNextPage || query.isFetchingNextPage ? ' More are available.' : ' End of results.'}</p>
  </div>
}
