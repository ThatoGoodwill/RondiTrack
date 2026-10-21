import { useEffect, useState } from 'react'
import { msUntilNextSaMidnight } from '../lib/deadline'

export function useSaNow(): Date {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    // Re-render just after the next Johannesburg midnight, then schedule the next.
    const id = window.setTimeout(() => setNow(new Date()), msUntilNextSaMidnight(now) + 1000)
    return () => window.clearTimeout(id)
  }, [now])

  return now
}