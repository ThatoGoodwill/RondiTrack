import { Suspense } from 'react'
import { LoginForm } from '../LoginForm'
import { ClearOldPins } from '../ClearOldPins'
import { safeNextPath } from '@/lib/safeNext'

type Params = { searchParams: Promise<{ next?: string; expired?: string; registered?: string }> }

async function LoginContent({ searchParams }: Params) {
  const query = await searchParams
  return <><ClearOldPins /><LoginForm next={safeNextPath(query.next)}
    expired={query.expired === '1'} registered={query.registered === '1'} /></>
}
export default function LoginPage(props: Params) {
  return <Suspense fallback={<p role="status">Loading sign-in…</p>}><LoginContent {...props} /></Suspense>
}
