 'use client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
export function AppQueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(() => new QueryClient({ defaultOptions: { queries: {
    retry: (attempt, error) => !(error instanceof Error && 'status' in error &&
      typeof error.status === 'number' && error.status >= 400 && error.status < 500) && attempt < 2,
    refetchOnWindowFocus: false,
  } } }))
  useEffect(() => {
    const handle = () => { void client.invalidateQueries({ queryKey: ['activity'] }); void client.invalidateQueries({ queryKey: ['contributions'] }) }
    const clear = () => client.clear()
    window.addEventListener('rondi:contribution-recorded', handle)
    window.addEventListener('rondi:clear-session', clear)
    return () => {
      window.removeEventListener('rondi:contribution-recorded', handle)
      window.removeEventListener('rondi:clear-session', clear)
    }
  }, [client])
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}
