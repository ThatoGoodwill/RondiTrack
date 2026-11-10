 'use client'
import { signOut } from '@/app/(app)/actions'
import { clearPinsFromBrowser } from '@/store/pins'
export function SignOutForm() {
  return <form action={signOut} onSubmit={() => { clearPinsFromBrowser(); window.dispatchEvent(new Event('rondi:clear-session')) }}>
    <button type="submit" className="rounded border px-3 py-2 text-sm">Sign out</button>
  </form>
}
