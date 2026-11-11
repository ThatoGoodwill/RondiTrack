import { NextResponse } from 'next/server'
import { getSession } from '@/lib/session'
import { apiBase } from '@/lib/serverApi'

// This endpoint belongs to the site; the browser never receives an API token or storage URL.
export async function GET(_: Request, { params }: {
  params: Promise<{ id: string; contributionId: string }>
}) {
  const session = await getSession()
  if (!session) return NextResponse.json({ title: 'Sign in required' }, {
    status: 401, headers: { 'Cache-Control': 'private, no-store', 'X-Robots-Tag': 'noindex' },
  })
  const { id, contributionId } = await params
  if (!/^[0-9a-f-]{36}$/i.test(id) || !/^[0-9a-f-]{36}$/i.test(contributionId))
    return NextResponse.json({ title: 'Not found' }, { status: 404 })
  try {
    const upstream = await fetch(`${apiBase()}/api/v1/stokvels/${id}/contributions/${contributionId}/proof`, {
      headers: { Authorization: `Bearer ${session.accessToken}` }, cache: 'no-store',
    })
    if (!upstream.ok) return NextResponse.json({ title: 'Document not found' }, {
      status: upstream.status === 401 ? 401 : 404,
      headers: { 'Cache-Control': 'private, no-store', 'X-Robots-Tag': 'noindex' },
    })
    return new Response(upstream.body, {
      status: 200,
      headers: {
        'Content-Type': upstream.headers.get('Content-Type') ?? 'application/octet-stream',
        'Content-Disposition': upstream.headers.get('Content-Disposition') ?? 'attachment; filename="proof-of-payment"',
        'Cache-Control': 'private, no-store', 'X-Content-Type-Options': 'nosniff', 'X-Robots-Tag': 'noindex',
      },
    })
  } catch { return NextResponse.json({ title: 'Document is unavailable' }, { status: 503 }) }
}
