'use client'
import { useActionState, useEffect, useRef } from 'react'
import { joinInviteAction, type JoinState } from './actions'

export function JoinForm({ code }: { code: string }) {
  const [state, action, pending] = useActionState(joinInviteAction,
    { message: '', reference: null } as JoinState)
  const focus = useRef<HTMLHeadingElement>(null)
  useEffect(() => { focus.current?.focus() }, [])
  return <form action={action} className="card space-y-3">
    <h1 ref={focus} tabIndex={-1} className="text-xl font-bold">Step 3 of 3: Confirm joining</h1>
    <input name="code" type="hidden" value={code}/>
    <p>By continuing, you agree to the stokvel contribution schedule shown in the previous step.</p>
    {state.message && <p role="alert">{state.message}{state.reference && ` Reference: ${state.reference}`}</p>}
    <button className="btn" type="submit" disabled={pending}>{pending ? 'Joining…' : 'Confirm and join stokvel'}</button>
  </form>
}
