import { useMe } from './api/queries'
import { HomePage } from './pages/HomePage'
import { SignInForm } from './components/SignInForm'
import { ThemeToggle } from './components/ThemeToggle'
import { ErrorView } from './components/ErrorView'

export default function App() {
  const me = useMe()

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto max-w-5xl px-4 py-4">
        <div className="mb-6 flex justify-end">
          <ThemeToggle />
        </div>

        {me.isLoading && <p className="text-center text-sm text-muted-foreground">Loading…</p>}
        {me.isError && me.error && (
          // 401 from /me is expected when not signed in — not an error state to show
          (me.error as { status?: number }).status === 401 ? (
            <SignInForm />
          ) : (
            <ErrorView error={me.error} onRetry={() => me.refetch()} />
          )
        )}
        {me.data && <HomePage />}
      </div>
    </div>
  )
}