import { ContributionCard } from './components/ContributionCard'
import { StokvelCard } from './components/StokvelCard'
import { contributions, home } from './data/sample'

export default function App() {
  return (
    <main>
      <h1>RondiTrack</h1>

      <section>
        <h2>My stokvels</h2>
        <div className="grid">
          {home.map((entry) => (
            <StokvelCard key={entry.stokvelId} entry={entry} />
          ))}
        </div>
      </section>

      <section>
        <h2>Contributions</h2>
        <ul className="grid">
          {contributions.map((item) => (
            <ContributionCard key={item.id} item={item} />
          ))}
        </ul>
      </section>
    </main>
  )
}
