 'use client'
import { useEffect } from 'react'
export default function AppError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => { console.error('RondiTrack error', error.message) }, [error])
  return <section role="alert" className="card space-y-3"><h1 className="font-bold">Could not load your data</h1>
    <p>Please try again. If this continues, contact support with the reference shown below.</p>
    <p>Reference: <code>{error.digest ?? 'Unavailable'}</code></p>
    <button className="btn" type="button" onClick={reset}>Try again</button>
  </section>
}
