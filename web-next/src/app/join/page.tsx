import type { Metadata } from 'next'
import Link from 'next/link'
import { redirect } from 'next/navigation'
import { Suspense } from 'react'
import { apiBase } from '@/lib/serverApi'
import { getSession } from '@/lib/session'
import { formatCents } from '@/lib/money'
import { JoinForm } from './JoinForm'

export const metadata: Metadata = {
  title: 'Join a stokvel | RondiTrack',
  description: 'Private RondiTrack stokvel invitation. Sign in to review it.',
  robots: { index: false, follow: false },
  openGraph: { title: 'RondiTrack invitation', description: 'A private stokvel invitation on RondiTrack.' },
}
type Q = { code?: string; step?: string }
const steps = ['code', 'rules', 'confirm'] as const
function href(code: string, step: string) {
  return `/join?code=${encodeURIComponent(code)}&step=${step}`
}
function StepLinks({ step }: { step: string }) {
  return <ol className="flex gap-4 text-sm" aria-label="Join steps">
    {steps.map((item, i) => <li key={item} aria-current={step === item ? 'step' : undefined}>
      {i + 1}. {item === 'code' ? 'Invite code' : item === 'rules' ? 'Rules' : 'Confirm'}</li>)}
  </ol>
}
async function JoinContent({ searchParams }: { searchParams: Promise<Q> }) {
  const query = await searchParams
  const code = (query.code ?? '').trim().toLowerCase()
  const step = steps.includes(query.step as typeof steps[number]) ? query.step! : 'code'
  if (step !== 'code' && !code) redirect('/join')
  if (step === 'code') return <div className="space-y-4">
    <StepLinks step="code"/>
    <form action="/join" method="GET" className="card space-y-3">
      <h1 className="text-xl font-bold">Step 1 of 3: Enter your invite</h1>
      <label className="label" htmlFor="inviteCode">Invite code</label>
      <input className="input" id="inviteCode" name="code" defaultValue={code}
        autoComplete="off" required minLength={48} maxLength={48}/>
      <input name="step" type="hidden" value="rules" />
      <button className="btn" type="submit">Check invite</button>
    </form>
  </div>
  const session = await getSession()
  if (!session) redirect(`/login?next=${encodeURIComponent(href(code, step))}`)
  if (!/^[0-9a-f]{48}$/.test(code)) return <div className="card">
    <p role="alert">Check the invite code and try again.</p><Link className="a" href="/join">Enter a code</Link>
  </div>
  const response = await fetch(`${apiBase()}/api/v1/invites/${code}`, {
    headers: { Authorization: `Bearer ${session.accessToken}` }, cache: 'no-store',
  }).catch(() => null)
  if (!response?.ok) return <div className="card space-y-3">
    <h1 className="text-xl font-bold">Invite unavailable</h1>
    <p role="alert">{response?.status === 429 ? 'Too many attempts. Try again later.' :
      'This invite is invalid or expired. Ask for a new invite.'}</p>
    <Link className="a" href={href(code, 'code')}>Check the code again</Link>
  </div>
  const preview = await response.json() as { name: string; contributionCents: number; frequency: string;
    memberCount: number; maxMembers: number; rules: string; expiresAtUtc: string }
  return <div className="space-y-4"><StepLinks step={step}/>
    {step === 'rules' ? <section className="card space-y-3">
      <h1 className="text-xl font-bold">Step 2 of 3: Review the stokvel rules</h1>
      <h2 className="font-semibold">{preview.name}</h2>
      <p>Contribution: {formatCents(preview.contributionCents)} · {preview.frequency}</p>
      <p>{preview.memberCount} of {preview.maxMembers} members</p>
      <p>{preview.rules}</p>
      <p className="text-sm muted">Valid until {new Date(preview.expiresAtUtc).toLocaleDateString('en-ZA',
        { timeZone: 'Africa/Johannesburg' })}</p>
      <div className="flex gap-3"><Link className="a" href={href(code, 'code')}>Back</Link>
        <Link className="btn" href={href(code, 'confirm')}>Continue to confirm</Link></div>
    </section> : <div className="space-y-3">
      <div className="card">Joining <strong>{preview.name}</strong> · {formatCents(preview.contributionCents)} every {preview.frequency.toLowerCase()}</div>
      <JoinForm code={code}/>
      <Link className="a" href={href(code, 'rules')}>Back to rules</Link>
    </div>}
  </div>
}
export default function JoinPage({ searchParams }: { searchParams: Promise<Q> }) {
  return <div className="mx-auto max-w-2xl space-y-5 p-5">
    <Link className="a" href="/">RondiTrack</Link>
    <Suspense fallback={<div className="card h-56 animate-pulse">Checking your invite…</div>}>
      <JoinContent searchParams={searchParams}/>
    </Suspense>
  </div>
}
