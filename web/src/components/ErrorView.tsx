import { describeError } from '../lib/errorMessages'

type Props = {
  error: unknown
  onRetry?: () => void
}

export function ErrorView({ error, onRetry }: Props) {
  const d = describeError(error)

  return (
    <div role="alert" className="rounded-md border border-red-300 bg-red-50 p-4 text-sm text-red-900">
      <h3 className="font-semibold">{d.title}</h3>
      <p className="mt-1">{d.message}</p>
      {d.correlationId && (
        <p className="mt-2 font-mono text-xs">
          Reference: <span className="select-all">{d.correlationId}</span>
        </p>
      )}
      {onRetry && d.canRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-3 rounded border border-red-900 px-3 py-1 font-medium"
        >
          Try again
        </button>
      )}
    </div>
  )
}