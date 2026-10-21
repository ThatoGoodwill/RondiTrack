import { useState } from 'react'
import { ChevronDown } from 'lucide-react'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Button } from '@/components/ui/button'
import { formatCents } from '../lib/money'
import { formatCalendarDate } from '../lib/dates'
import { groupContributionsByCycle } from '../lib/groupContributions'
import { ContributionCard } from './ContributionCard'
import { DueCountdown } from './DueCountdown'
import { StatusBadge } from './StatusBadge'
import type { ContributionItem, CycleItem } from '../api/types'

export function ContributionList({ cycles, contributions }: { cycles: CycleItem[]; contributions: ContributionItem[] }) {
  const groups = groupContributionsByCycle(cycles, contributions) // derived, not state

  // Which groups are open, keyed by cycle id. Starts with the Open cycle(s) expanded.
  const [openIds, setOpenIds] = useState<ReadonlySet<string>>(
    () => new Set(cycles.filter((c) => c.status === 'Open').map((c) => c.id)),
  )

  const allOpen = groups.every((g) => openIds.has(g.key)) // derived label, not a second state
  const toggle = (key: string) =>
    setOpenIds((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  const setAll = () => setOpenIds(allOpen ? new Set() : new Set(groups.map((g) => g.key)))

  return (
    <section className="space-y-3">
      <Button type="button" variant="outline" className="min-h-9" onClick={setAll}>
        {allOpen ? 'Collapse all' : 'Expand all'}
      </Button>

      {groups.map(({ key, cycle, contributions: rows }) => (
        <Collapsible key={key} open={openIds.has(key)} onOpenChange={() => toggle(key)}
          className="rounded-lg border bg-card text-card-foreground">
          <CollapsibleTrigger className="flex min-h-11 w-full items-start gap-2 p-3 text-left">
            <ChevronDown aria-hidden="true" className="mt-1 size-4 shrink-0" />
            <div className="min-w-0 flex-1 break-words">
              <h3 className="font-semibold">{cycle ? `Cycle ${cycle.label}` : 'Unknown cycle'}</h3>
              {cycle && (
                <p className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                  <StatusBadge kind="cycle" status={cycle.status} />
                  {cycle.endsOn && (
                    <>
                      <span>Due {formatCalendarDate(cycle.endsOn)}</span>
                      <DueCountdown dueDate={cycle.endsOn} status={cycle.status} />
                    </>
                  )}
                  <span className="whitespace-nowrap">
                    {formatCents(cycle.collectedCents)} of {formatCents(cycle.dueCents)}
                  </span>
                </p>
              )}
            </div>
          </CollapsibleTrigger>
          <CollapsibleContent className="space-y-2 p-3 pt-0">
            {rows.length === 0 ? (
              <p className="text-sm text-muted-foreground">No contributions yet</p>
            ) : (
              <ul className="space-y-2">
                {rows.map((item) => <ContributionCard key={item.id} item={item} />)}
              </ul>
            )}
          </CollapsibleContent>
        </Collapsible>
      ))}
    </section>
  )
}