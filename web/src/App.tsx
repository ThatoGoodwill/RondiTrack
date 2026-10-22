import { useEffect, useState } from 'react'
import { api } from './api/client'
import { ApiError } from './auth/session'
import { HomePage } from './pages/HomePage'
import { SignInForm } from './components/SignInForm'
import { ThemeToggle } from './components/ThemeToggle'
import { ErrorView } from './components/ErrorView'

type MeState = 
  | { status: 'loading' }
  | { status: 'authenticated' }
  | { status: 'unauthenticated' }
  | { status: 'error'; error: unknown }

export default function App() {
  const [meState, setMeState] = useState<MeState>({ status: 'loading' })
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    let cancelled = false
    async function checkAuth() {
      setMeState({ status: 'loading' })
      try {
        const res = await api.GET('/api/v1/me', { credentials: 'include' })
        if (cancelled) return
        
        if (res.response.ok) {
          setMeState({ status: 'authenticated' })
        } else if (res.response.status === 401) {
          setMeState({ status: 'unauthenticated' })
        } else {
          setMeState({ status: 'error', error: new ApiError(res.response.status, res.error, res.response.headers.get('Retry-After')) })
        }
      } catch (err) {
        if (!cancelled) setMeState({ status: 'error', error: err })
      }
    }
    checkAuth()
    return () => { cancelled = true }
  }, [refreshKey])

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto max-w-5xl px-4 py-4">
        <div className="mb-6 flex justify-end"><ThemeToggle /></div>

        {meState.status === 'loading' && (
          <p className="text-center text-sm text-muted-foreground">Loading…</p>
        )}
        
        {meState.status === 'unauthenticated' && (
          <SignInForm onSuccess={() => setRefreshKey(k => k + 1)} />
        )}
        
        {meState.status === 'error' && (
          <ErrorView error={meState.error} />
        )}
        
        {/* PASS THE CALLBACK HERE */}
        {meState.status === 'authenticated' && (
          <HomePage onSignOut={() => setRefreshKey(k => k + 1)} />
        )}
      </div>
    </div>
  )
}