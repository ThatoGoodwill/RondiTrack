import { useState } from 'react'
import { useLogin } from '../api/queries'
import { ErrorView } from './ErrorView'

type Props = {
  onSuccess: () => void
}

export function SignInForm({ onSuccess }: Props) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const login = useLogin()

  return (
    <div className="mx-auto max-w-sm rounded-lg border bg-card p-6 text-card-foreground">
      <h2 className="mb-4 text-lg font-semibold">Sign in to RondiTrack</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          login.mutate({ email, password }, {
            onSuccess: () => {
              onSuccess()
            }
          })
        }}
        className="space-y-3"
      >
        <label className="block text-sm">
          Email
          <input
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 block w-full rounded border px-3 py-2"
            autoComplete="username"
          />
        </label>
        <label className="block text-sm">
          Password
          <input
            type="password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="mt-1 block w-full rounded border px-3 py-2"
            autoComplete="current-password"
          />
        </label>
        <button
          type="submit"
          disabled={login.isPending}
          className="min-h-11 w-full rounded bg-primary px-4 text-primary-foreground disabled:opacity-50"
        >
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      {login.isError && (
        <div className="mt-4">
          <ErrorView error={login.error} />
        </div>
      )}
    </div>
  )
}