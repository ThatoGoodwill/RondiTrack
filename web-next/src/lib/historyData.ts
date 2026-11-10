import 'server-only'
import { apiBase } from './serverApi'
import { getSession } from './session'
import { southAfricanDaysToUtcRange } from './southAfricanDates'
import { type HistoryFilters, normalizedFilters } from './historyFilters'
import type { Contribution, Page } from './types'

/** The ONLY date-to-instant conversion point for both Server Components and BFF. */
export async function fetchHistory(stokvelId: string, filters: HistoryFilters,
  pageToken: string | null = null): Promise<{ response: Response; data: Page<Contribution> | null }> {
  const session = await getSession()
  if (!session) return { response: Response.json({ title: 'Please sign in' }, { status: 401 }), data: null }
  const f = normalizedFilters(filters)
  const range = southAfricanDaysToUtcRange(f.from || null, f.to || null)
  const params = new URLSearchParams({ pageSize: '10', sort: f.sort === 'newest' ? 'RecordedAt_desc' : 'RecordedAt_asc' })
  if (f.member) params.set('member', f.member)
  if (range.from) params.set('from', range.from)
  if (range.to) params.set('to', range.to)
  if (pageToken) params.set('pageToken', pageToken)
  const response = await fetch(`${apiBase()}/api/v1/stokvels/${encodeURIComponent(stokvelId)}/contributions?${params}`, {
    cache: 'no-store', headers: { Authorization: `Bearer ${session.accessToken}` },
  })
  return { response, data: response.ok ? await response.json() as Page<Contribution> : null }
}
