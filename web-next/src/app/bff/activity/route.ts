import { getSession } from '@/lib/session'
import { apiBase } from '@/lib/serverApi'
export async function GET(request: Request) {
  const session = await getSession()
  if (!session) return Response.json({ title: 'Please sign in' }, { status: 401 })
  const url = new URL(request.url)
  const params = new URLSearchParams({ pageSize: '10' })
  const token = url.searchParams.get('pageToken')
  if (token) params.set('pageToken', token)
  let res: Response
  try { res = await fetch(`${apiBase()}/api/v1/activity?${params}`, {
    headers: { Authorization: `Bearer ${session.accessToken}` }, cache: 'no-store',
  }) } catch { return Response.json({ title: 'Service unavailable' }, { status: 502 }) }
  if (!res.ok) return Response.json({ title: 'Could not load activity' }, { status: res.status })
  return Response.json(await res.json(), { headers: { 'Cache-Control': 'no-store' } })
}
