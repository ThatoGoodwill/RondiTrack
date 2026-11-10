 'use client'
import { useActionState } from 'react'
import { register, initialAuthState } from './actions'
export function RegisterForm() {
  const [state, action, pending] = useActionState(register, initialAuthState)
  return <form action={action} className="card space-y-4">
    <h1 className="text-2xl font-bold">Create your account</h1>
    {state.message && <div role="alert" className="text-red-700">{state.message}
      {state.reference && <p>Reference: <code>{state.reference}</code></p>}</div>}
    <div><label className="label" htmlFor="firstName">First name</label><input className="input" id="firstName" name="firstName" autoComplete="given-name" required /></div>
    <div><label className="label" htmlFor="lastName">Last name</label><input className="input" id="lastName" name="lastName" autoComplete="family-name" required /></div>
    <div><label className="label" htmlFor="dateOfBirth">Date of birth</label><input className="input" id="dateOfBirth" name="dateOfBirth" type="date" required /></div>
    <div><label className="label" htmlFor="regEmail">Email</label><input className="input" id="regEmail" name="email" type="email" autoComplete="username" required /></div>
    <div><label className="label" htmlFor="regPassword">Password (12+ characters)</label><input className="input" id="regPassword" name="password" type="password" minLength={12} autoComplete="new-password" required /></div>
    <button className="btn w-full" type="submit" disabled={pending}>{pending ? 'Creating…' : 'Register'}</button>
    <a className="a" href="/login">Already have an account? Sign in</a>
  </form>
}
