import type { ContributionItem } from '../api/types'
import { formatInstant } from '../lib/dates'
import { formatCents } from '../lib/money'

type ContributionCardProps = { item: ContributionItem }

export function ContributionCard({ item }: ContributionCardProps) {
  return (
    <li className="card">
      <p className="amount">{formatCents(item.amountCents)}</p>
      {item.recordedAt && <p>Paid: {formatInstant(item.recordedAt)}</p>}
      <p>Cycle: {item.cycleLabel}</p>
    </li>
  )
}
