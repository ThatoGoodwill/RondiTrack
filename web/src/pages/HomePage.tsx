import { useState } from 'react'
import { useHome, useStokvels, useCycles, useContributions, useLogout } from '../api/queries'
import { StokvelCard } from '../components/StokvelCard'
import { StokvelSwitcher } from '../components/StokvelSwitcher'
import { ErrorView } from '../components/ErrorView'
import { ContributionList } from '../components/ContributionList'

export function HomePage() {
  const home = useHome()
  const stokvels = useStokvels()
  const logout = useLogout()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  // On first load, auto-select the first stokvel.
  const effectiveId = selectedId ?? stokvels.data?.[0]?.id ?? null

  const cycles = useCycles(effectiveId ?? undefined)
  const contributions = useContributions(effectiveId ?? undefined)

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">My stokvels</h1>
        <button
          type="button"
          onClick={() => logout.mutate()}
          className="min-h-9 rounded border px-3 text-sm"
        >
          Sign out
        </button>
      </header>

      {home.isLoading && <SkeletonCard />}
      {home.isError && <ErrorView error={home.error} onRetry={() => home.refetch()} />}
      {home.data && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {home.data.stokvels.map((entry) => (
            <StokvelCard key={entry.stokvelId} entry={entry} />
          ))}
        </div>
      )}

      <section>
        <h2 className="mb-3 text-lg font-medium">Contributions</h2>
        {stokvels.data && stokvels.data.length > 0 && (
          <StokvelSwitcher
            stokvels={stokvels.data}
            selectedId={effectiveId}
            onSelect={setSelectedId}
          />
        )}

        {cycles.isLoading && <SkeletonList />}
        {cycles.isError && <ErrorView error={cycles.error} />}
        {cycles.data && contributions.data && effectiveId && (
          <ContributionList
            cycles={cycles.data}
            contributions={contributions.data.pages.flatMap((p) => p.items)}
          />
        )}
      </section>
    </div>
  )
}

function SkeletonCard() {
  return <div className="h-32 animate-pulse rounded-lg bg-muted" />
}

function SkeletonList() {
  return (
    <div className="mt-4 space-y-2">
      <div className="h-16 animate-pulse rounded bg-muted" />
      <div className="h-16 animate-pulse rounded bg-muted" />
    </div>
  )
}