import type { StokvelResponse } from '../api/queries'

type Props = {
  stokvels: StokvelResponse[]
  selectedId: string | null
  onSelect: (id: string) => void
}

export function StokvelSwitcher({ stokvels, selectedId, onSelect }: Props) {
  if (stokvels.length === 0) return null

  return (
    <div className="flex flex-wrap gap-2">
      {stokvels.map((s) => (
        <button
          key={s.id}
          type="button"
          onClick={() => onSelect(s.id)}
          className={`min-h-9 rounded border px-3 text-sm ${
            selectedId === s.id
              ? 'border-primary bg-primary text-primary-foreground'
              : 'hover:bg-accent'
          }`}
          aria-pressed={selectedId === s.id}
        >
          {s.name}
        </button>
      ))}
    </div>
  )
}