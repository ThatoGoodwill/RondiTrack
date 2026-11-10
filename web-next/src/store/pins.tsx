 'use client'
import { createContext, useContext, useEffect, useState } from 'react'
import { createStore, useStore } from 'zustand'
import { persist } from 'zustand/middleware'

export type Pin = { id: string; name: string }
type PinState = {
  pins: Pin[]
  toggle: (pin: Pin) => void
  clear: () => void
  keepOnly: (ids: readonly string[]) => void
}
function createPinStore() {
  return createStore<PinState>()(persist((set) => ({
    pins: [],
    toggle: (pin) => set(s => ({ pins: s.pins.some(p => p.id === pin.id)
      ? s.pins.filter(p => p.id !== pin.id) : [...s.pins, pin] })),
    clear: () => set({ pins: [] }),
    keepOnly: (ids) => set(s => ({ pins: s.pins.filter(p => ids.includes(p.id)) })),
  }), {
    name: 'ronditrack-pins',
    skipHydration: true,
    partialize: state => ({ pins: state.pins }),
  }))
}
type Store = ReturnType<typeof createPinStore>
const Context = createContext<Store | null>(null)
export function PinsProvider({ children, allowedIds }: { children: React.ReactNode; allowedIds: string[] }) {
  const [store] = useState(createPinStore)
  useEffect(() => {
    // Hydration only after mount avoids mismatching server-rendered sidebar markup.
    void store.persist.rehydrate().then(() => store.getState().keepOnly(allowedIds))
  }, [store, allowedIds.join('|')]) // stable membership signature, no secret data
  return <Context.Provider value={store}>{children}</Context.Provider>
}
export function usePins<T>(selector: (s: PinState) => T): T {
  const store = useContext(Context)
  if (!store) throw new Error('PinsProvider missing')
  return useStore(store, selector)
}
export function clearPinsFromBrowser() {
  if (typeof window !== 'undefined') localStorage.removeItem('ronditrack-pins')
}
