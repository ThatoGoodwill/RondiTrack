import { useTheme } from '../hooks/useTheme'

const options: { value: 'light' | 'dark' | 'system'; label: string }[] = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'system', label: 'System' },
]

export function ThemeToggle() {
  const { mode, setMode } = useTheme()

  return (
    <div className="flex gap-1 rounded-md border p-1" role="group" aria-label="Theme">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          onClick={() => setMode(o.value)}
          className={`min-h-9 rounded px-3 text-sm ${
            mode === o.value
              ? 'bg-primary text-primary-foreground'
              : 'hover:bg-accent'
          }`}
          aria-pressed={mode === o.value}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}