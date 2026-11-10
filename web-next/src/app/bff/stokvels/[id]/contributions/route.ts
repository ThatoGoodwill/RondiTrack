import { loadHistoryFilters, normalizedFilters } from '@/lib/historyFilters'
import { fetchHistory } from '@/lib/historyData'
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  const url = new URL(request.url)
  const filters = normalizedFilters(loadHistoryFilters(url.searchParams))
  const { response, data } = await fetchHistory(id, filters, url.searchParams.get('pageToken'))
  return data ? Response.json(data, { headers: { 'Cache-Control': 'no-store' } }) :
    Response.json({ title: response.status === 401 ? 'Session expired' : 'History unavailable' },
      { status: response.status, headers: { 'Cache-Control': 'no-store' } })
}
