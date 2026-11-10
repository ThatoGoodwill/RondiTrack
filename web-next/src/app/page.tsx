import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { getSession } from '@/lib/session'
async function HomeRedirect() { redirect(await getSession() ? '/dashboard/member' : '/login') }
export default function Home() { return <Suspense fallback={<p className="p-6">Loading…</p>}><HomeRedirect /></Suspense> }
