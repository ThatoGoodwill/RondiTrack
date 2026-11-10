import { Suspense } from 'react'
import Link from 'next/link'
import { requireSession } from '@/lib/session'
import { apiForSession, unwrapApi } from '@/lib/serverApi'
import type { Home } from '@/lib/types'
import { PinsProvider } from '@/store/pins'
import { PinMenu } from '@/components/PinMenu'
import { SignOutForm } from '@/components/SignOutForm'

async function AppChrome({ children }: { children: React.ReactNode }) {
  const session = await requireSession()
  const api = await apiForSession()
  const home = unwrapApi<Home>(await api.GET('/api/v1/me/home'))
  const allowedIds = home.stokvels.map(item => item.stokvelId)
  return <PinsProvider allowedIds={allowedIds}>
    <div className="mx-auto min-h-screen max-w-6xl gap-5 px-4 py-5 md:grid md:grid-cols-[13rem_1fr]">
      <aside className="card mb-5 h-fit space-y-5">
        <Link href="/dashboard/member" className="text-xl font-bold text-blue-800">RondiTrack</Link>
        <div className="text-xs muted">Signed in as<br/><strong>{session.user.displayName}</strong></div>
        <nav aria-label="Main navigation" className="flex flex-col gap-3 text-sm">
          <Link className="a" href="/dashboard/member">Member dashboard</Link>
          <Link className="a" href="/dashboard/treasurer">Treasurer dashboard</Link>
          <Link className="a" href="/stokvels">My stokvels</Link>
          <Link className="a" href="/activity">Activity</Link>
        </nav>
        <section><h2 className="mb-2 text-xs font-bold uppercase">Pinned</h2><PinMenu /></section>
        <SignOutForm />
      </aside>
      <main className="min-w-0">{children}</main>
    </div>
  </PinsProvider>
}
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<div className="mx-auto max-w-6xl p-5">Loading account…</div>}>
    <AppChrome>{children}</AppChrome>
  </Suspense>
}
