import type { MeHomeEntry } from '../api/types'
import { formatCalendarDate, formatInstantDate } from '../lib/dates'
import { isTreasurer, roleText } from '../lib/labels'
import { formatCents } from '../lib/money'

type StokvelCardProps = { entry: MeHomeEntry }

export function StokvelCard({ entry }: StokvelCardProps) {
  const cycle = entry.currentCycle

  return (
    <article className="card">
      <h3>{entry.name}</h3>
      <p>{roleText(entry.role)}</p>

      {cycle ? (
        <>
          <p>
            Cycle {cycle.label}: {cycle.status}
            {cycle.endsOn ? ` (due ${formatCalendarDate(cycle.endsOn)})` : ''}
          </p>
          <p>Paid this cycle: {formatCents(cycle.paidCents)}</p>
          {cycle.owedCents > 0 ? (
            <p>Still owed: {formatCents(cycle.owedCents)}</p>
          ) : (
            <p>All paid up for this cycle</p>
          )}
        </>
      ) : (
        <p>No open cycle</p>
      )}

      <p>
        {entry.nextPayoutDueUtc
          ? `Next payout: ${formatInstantDate(entry.nextPayoutDueUtc)}`
          : 'No payout scheduled'}
      </p>

      {isTreasurer(entry.role) && (
        <div className="treasurer">
          <h4>Treasurer tools</h4>
          <button type="button">Close cycle</button>
          <button type="button">Record payout</button>
        </div>
      )}
    </article>
  )
}
