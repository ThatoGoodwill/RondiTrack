import { useState } from 'react'
import { useHome, useStokvels, useStokvel, useCycles, useContributions, useLogout, useMe, useMembers } from '../api/queries'
import { StokvelCard } from '../components/StokvelCard'
import { StokvelSwitcher } from '../components/StokvelSwitcher'
import { ErrorView } from '../components/ErrorView'
import { ContributionList } from '../components/ContributionList'
import { RecordContributionForm } from '../contributions/RecordContributionForm'
import { CreateCycleForm } from '../contributions/CreateCycleForm'

type Props = {
  onSignOut: () => void
}

export function HomePage({ onSignOut }: Props) {
  const home = useHome()
  const me = useMe()
  const stokvels = useStokvels()
  const logout = useLogout()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const effectiveId = selectedId ?? stokvels.data?.[0]?.id ?? null

  const stokvel = useStokvel(effectiveId ?? undefined)
  const cycles = useCycles(effectiveId ?? undefined)
  const contributions = useContributions(effectiveId ?? undefined)
  const members = useMembers(effectiveId ?? undefined)
  const currentHome = home.data?.stokvels.find(s => s.stokvelId === effectiveId)
  const openCycle = cycles.data?.find(c => c.status === 'Open')

  const handleSignOut = () => {
    // onSettled runs whether the network call succeeds or fails.
    // We tell the parent to wipe its state and re-check auth.
    logout.mutate(undefined, {
      onSettled: () => {
        onSignOut()
      }
    })
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">My stokvels</h1>
        <button
          type="button"
          onClick={handleSignOut}
          className="min-h-9 rounded border px-3 text-sm"
          disabled={logout.isPending}
        >
          {logout.isPending ? 'Signing out…' : 'Sign out'}
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
        {cycles.data && cycles.data.length === 0 && <p>No cycles yet. Ask your treasurer to open one.</p>}
        {contributions.data && contributions.data.pages.every(p => p.items.length === 0) &&
          <p>No contributions have been recorded yet.</p>}
        {effectiveId && openCycle && me.data && currentHome && (
          <div className="mt-5">
            <RecordContributionForm
              key={`${effectiveId}-${openCycle.id}`}
              stokvelId={effectiveId} cycleId={openCycle.id} cycleLabel={openCycle.label}
              status={openCycle.status} ownId={me.data.id} role={currentHome.role}
              owedCents={currentHome.currentCycle?.owedCents ?? 0}
              members={members.data?.items.map(m => ({ userId: m.userId,
                label: m.userId === me.data!.id ? 'You' : `Member ${m.userId.slice(0, 8)}` })) ?? []}
            />
          </div>
        )}
        {effectiveId && cycles.data && !openCycle && <>
          <p>No open cycle. Contributions can only be recorded in an open cycle.</p>
          {(currentHome?.role === 'Treasurer' || currentHome?.role === 'Admin') && stokvel.data &&
            <CreateCycleForm stokvelId={effectiveId} targetRands={stokvel.data.contributionAmount} />}
          {currentHome?.role === 'Member' && <p>Ask your treasurer to open the next cycle.</p>}
        </>}
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
