 'use client'
import Link from 'next/link'
import { usePins, type Pin } from '@/store/pins'
export function PinMenu() {
  const pins = usePins(s => s.pins)
  if (!pins.length) return <p className="muted text-xs">Pinned stokvels will appear here.</p>
  return <ul className="space-y-1 text-sm">{pins.map(p =>
    <li key={p.id}><Link className="a" href={`/stokvels/${p.id}`}>{p.name}</Link></li>)}</ul>
}
export function PinToggle({ id, name }: Pin) {
  const pinned = usePins(s => s.pins.some(p => p.id === id))
  const toggle = usePins(s => s.toggle)
  return <button className="rounded border px-3 py-1 text-sm" type="button" aria-pressed={pinned}
    onClick={() => toggle({ id, name })}>{pinned ? 'Unpin' : 'Pin this stokvel'}</button>
}
