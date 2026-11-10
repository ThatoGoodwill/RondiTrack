 'use client'
import { useActionState } from 'react'
import { signIn, initialAuthState } from './actions'
export function LoginForm({ next, expired, registered }: { next: string; expired: boolean; registered: boolean }) {
  const [state, action, pending] = useActionState(signIn, initialAuthState)
  return <form action={action} className="card space-y-4">
    <h1 className="text-2xl font-bold">Sign in to RondiTrack</h1>
    {expired && <p role="status">Your session ended. Sign in again.</p>}
    {registered && <p role="status">Account created. Please sign in.</p>}
    <input type="hidden" name="next" value={next} />
    {state.message && <div role="alert" className="text-red-700">{state.message}
      {state.reference && <p>Reference: <code>{state.reference}</code></p>}
    </div>}
    <div><label className="label" htmlFor="email">Email</label><input className="input" id="email" name="email" type="email" autoComplete="username" required /></div>
    <div><label className="label" htmlFor="password">Password</label><input className="input" id="password" name="password" type="password" autoComplete="current-password" required /></div>
    <button className="btn w-full" type="submit" disabled={pending}>{pending ? 'Signing in…' : 'Sign in'}</button>
    <p className="text-sm">New member? <a className="a" href="/register">Create an account</a></p>
  </form>
}
