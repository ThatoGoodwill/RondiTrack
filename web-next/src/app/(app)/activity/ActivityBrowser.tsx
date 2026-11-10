 'use client'
import { useEffect, useRef } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { displaySouthAfricanDate } from '@/lib/southAfricanDates'
import { formatCents } from '@/lib/money'
import type { Activity, Page } from '@/lib/types'
export function ActivityBrowser({ firstPage }: { firstPage: Page<Activity> }) {
  const query = useInfiniteQuery({
    queryKey: ['activity'], initialPageParam: null as string | null,
    initialData: { pages: [firstPage], pageParams: [null] },
    getNextPageParam: (page: Page<Activity>) => page.nextPageToken || undefined,
    queryFn: async ({ pageParam }) => {
      const query = pageParam ? `?pageToken=${encodeURIComponent(pageParam)}` : ''
      const res = await fetch(`/bff/activity${query}`, { cache: 'no-store' })
      if (!res.ok) throw new Error(`Could not load activity (${res.status}).`)
      return await res.json() as Page<Activity>
    },
  })
  const rows = query.data?.pages.flatMap(p => p.items) ?? []
  const end = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!end.current || !query.hasNextPage) return
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting && query.hasNextPage && !query.isFetchingNextPage) void query.fetchNextPage()
    }, { rootMargin: '100px' })
    observer.observe(end.current)
    return () => observer.disconnect()
  }, [query.hasNextPage, query.isFetchingNextPage, query.fetchNextPage])
  return <div className="space-y-3">
    {rows.length === 0 && <p className="card">No recent activity yet.</p>}
    <ul className="space-y-2">{rows.map(item => <li className="card text-sm" key={`${item.kind}-${item.id}-${item.stokvelId}`}>
      <strong>{item.description}</strong>{item.amountCents != null && <> · {formatCents(item.amountCents)}</>}
      <span className="muted"> · {displaySouthAfricanDate(item.occurredAt)}</span>
    </li>)}</ul>
    {query.isError && <p role="alert">Could not load activity. Try again.</p>}
    <div ref={end} aria-hidden="true" />
    {query.hasNextPage && <button className="btn" type="button" onClick={() => void query.fetchNextPage()}
      disabled={query.isFetchingNextPage}>{query.isFetchingNextPage ? 'Loading…' : 'Load more'}</button>}
    <p role="status" className="muted text-sm">Showing {rows.length} items.
      {query.hasNextPage || query.isFetchingNextPage ? ' More activity may be available.' : ' End of activity.'}</p>
  </div>
}
