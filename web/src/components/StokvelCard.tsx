import type { MeHomeEntry } from '../api/types'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCalendarDate, formatInstantDate } from '../lib/dates'
import { isTreasurer, roleText } from '../lib/labels'
import { formatCents } from '../lib/money'
import { DueCountdown } from './DueCountdown'
import { StatusBadge } from './StatusBadge'

type StokvelCardProps = { entry: MeHomeEntry }

export function StokvelCard({ entry }: StokvelCardProps) {
  const cycle = entry.currentCycle

  return (
    <Card className="w-full">
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-2">
          <CardTitle className="break-words min-w-0">
            {entry.name}
          </CardTitle>
          <StatusBadge kind="arrears" cents={entry.arrearsCents} />
        </div>
        <p className="text-sm text-muted-foreground">{roleText(entry.role)}</p>
      </CardHeader>

      <CardContent className="space-y-3">
        {cycle ? (
          <>
            <div className="flex flex-wrap items-center gap-2">
              <StatusBadge kind="cycle" status={cycle.status} />
              {cycle.endsOn && (
                <>
                  <span className="text-sm text-muted-foreground">
                    Due {formatCalendarDate(cycle.endsOn)}
                  </span>
                  <DueCountdown dueDate={cycle.endsOn} status={cycle.status} />
                </>
              )}
            </div>

            <p className="text-sm">
              Paid this cycle:{' '}
              <span className="whitespace-nowrap">{formatCents(cycle.paidCents)}</span>
            </p>
            {cycle.owedCents > 0 ? (
              <p className="text-sm">
                Still owed:{' '}
                <span className="whitespace-nowrap">{formatCents(cycle.owedCents)}</span>
              </p>
            ) : (
              <p className="text-sm">All paid up for this cycle</p>
            )}
          </>
        ) : (
          <p className="text-sm text-muted-foreground">No open cycle</p>
        )}

        <p className="text-sm">
          {entry.nextPayoutDueUtc
            ? `Next payout: ${formatInstantDate(entry.nextPayoutDueUtc)}`
            : 'No payout scheduled'}
        </p>

        {isTreasurer(entry.role) && (
          <div className="mt-2 rounded-md border p-3">
            <h4 className="mb-2 font-semibold">Treasurer tools</h4>
            <div className="flex gap-2">
              <button type="button" className="min-h-9 rounded-md border px-3 text-sm">
                Close cycle
              </button>
              <button type="button" className="min-h-9 rounded-md border px-3 text-sm">
                Record payout
              </button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}