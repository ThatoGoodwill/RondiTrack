type CycleLike = { id: string; endsOn: string | null }
type ContributionLike = { id: string; contributionCycleId: string; recordedAt?: string | null }

export type CycleGroup<C, T> = { key: string; cycle: C | null; contributions: T[] }

export function groupContributionsByCycle<C extends CycleLike, T extends ContributionLike>(
  cycles: C[],
  contributions: T[],
): CycleGroup<C, T>[] {
  const known = new Set(cycles.map((c) => c.id))
  const newestFirst = (a: T, b: T) => (b.recordedAt ?? '').localeCompare(a.recordedAt ?? '')
  const groups: CycleGroup<C, T>[] = [...cycles]
    .sort((a, b) => (b.endsOn ?? '').localeCompare(a.endsOn ?? ''))
    .map((cycle) => ({
      key: cycle.id,
      cycle,
      contributions: contributions.filter((c) => c.contributionCycleId === cycle.id)

.sort(newestFirst),
    }))

  const orphans = contributions.filter((c) => !known.has(c.contributionCycleId)).sort(newestFirst)
  if (orphans.length > 0) groups.push({ key: 'unknown', cycle: null, contributions: orphans })
  return groups
}