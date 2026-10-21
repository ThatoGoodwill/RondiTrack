import { ContributionList } from './components/ContributionList'
import { StokvelCard } from './components/StokvelCard'
import { contributions, home } from './data/sample'
import { ThemeToggle } from './components/ThemeToggle'

// For 7.2 we group contributions by cycle. Since our sample data doesn't include a full cycles array yet,
// we can derive a minimal one from the unique cycleIds in the contributions.
const sampleCycles = [
  {
    id: '01a156b2-3892-7ce7-8557-70eac36e6aad',
    stokvelId: '01a156b2-383d-7434-8703-9cdb57228788',
    label: '2026-09',
    targetAmount: 500,
    status: 'Open' as const,
    endsOn: '2026-10-31',
    collectedCents: 50010,
    dueCents: 0,
  },
]

export default function App() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4">
        <h1 className="text-xl font-semibold">RondiTrack</h1>
        <ThemeToggle />
      </header>

      <main className="mx-auto max-w-5xl space-y-8 px-4 pb-12">
        <section>
          <h2 className="mb-4 text-lg font-medium">My stokvels</h2>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {home.map((entry) => (
              <StokvelCard key={entry.stokvelId} entry={entry} />
            ))}
          </div>
        </section>

        <section>
          <h2 className="mb-4 text-lg font-medium">Contributions</h2>
          <ContributionList cycles={sampleCycles} contributions={contributions} />
        </section>
      </main>
    </div>
  )
}