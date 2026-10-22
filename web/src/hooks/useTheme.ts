import { useEffect, useState } from 'react'

type Mode = 'light' | 'dark' | 'system'

const STORAGE_KEY = 'theme'

function readStored(): Mode {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    if (v === 'light' || v === 'dark' || v === 'system') return v
  } catch {
    // private browsing: localStorage is blocked, fall back to system
  }
  return 'system'
}

function applyTheme(mode: Mode) {
  const prefersDark = matchMedia('(prefers-color-scheme: dark)').matches
  const dark = mode === 'dark' || (mode === 'system' && prefersDark)
  document.documentElement.classList.toggle('dark', dark)
}

export function useTheme() {
  const [mode, setMode] = useState<Mode>(readStored)

  useEffect(() => {
    applyTheme(mode)
    try {
      localStorage.setItem(STORAGE_KEY, mode)
    } catch {
      // private browsing: cannot save theme preference
    }
  }, [mode])

  // System-mode OS change listener
  useEffect(() => {
    if (mode !== 'system') return
    const mq = matchMedia('(prefers-color-scheme: dark)')
    const handler = () => applyTheme('system')
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [mode])

  return { mode, setMode }
}