import { LoginForm } from '../LoginForm'
import { ClearOldPins } from '../ClearOldPins'
import { safeNextPath } from '@/lib/safeNext'
export default async function LoginPage({ searchParams }: {
  searchParams: Promise<{ next?: string; expired?: string; registered?: string }>
}) {
  const query = await searchParams
  return <><ClearOldPins /><LoginForm next={safeNextPath(query.next)} expired={query.expired === '1'} registered={query.registered === '1'} /></>
}
