 'use client'
import { useEffect } from 'react'
/** Sign-in is a clean security boundary, including after an expired session on a shared phone. */
export function ClearOldPins() {
  useEffect(() => { localStorage.removeItem('ronditrack-pins'); window.dispatchEvent(new Event('rondi:clear-session')) }, [])
  return null
}
