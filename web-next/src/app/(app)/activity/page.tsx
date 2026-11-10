import { Suspense } from 'react'
import { apiRequest, readOrFail } from '@/lib/serverApi'
import { ActivityBrowser } from './ActivityBrowser'
import type { Activity, Page } from '@/lib/types'
async function InitialActivity() {
  const result = await apiRequest('/api/v1/activity?pageSize=10')
  const page = await readOrFail<Page<Activity>>(result)
  return <section className="space-y-4"><h1 className="text-2xl font-bold">Recent activity</h1>
    <p className="muted text-sm">Updates from the stokvels you belong to, newest first.</p>
    <ActivityBrowser firstPage={page} />
  </section>
}
export default function Page() { return <Suspense fallback={<div className="card animate-pulse">Loading activity…</div>}><InitialActivity /></Suspense> }
