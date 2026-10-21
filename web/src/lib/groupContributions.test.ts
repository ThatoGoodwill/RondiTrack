import { expect, it } from 'vitest'
import { groupContributionsByCycle } from './groupContributions'

const cycles = [
  { id: 'c1', endsOn: '2026-09-30' },
  { id: 'c2', endsOn: '2026-10-31' },
  { id: 'c3', endsOn: '2026-11-30' },
]
const contributions = [
  { id: 'a', contributionCycleId: 'c1', recordedAt: '2026-09-10T08:00:00Z' },
  { id: 'b', contributionCycleId: 'c2', recordedAt: '2026-10-01T08:00:00Z' },
  { id: 'c', contributionCycleId: 'c2', recordedAt: '2026-10-05T08:00:00Z' },
  { id: 'd', contributionCycleId: 'ghost', recordedAt: '2026-10-06T08:00:00Z' },
]

it('orders cycles newest first and keeps the empty one', () => {
  const groups = groupContributionsByCycle(cycles, contributions)
  expect(groups.map((g) => g.key)).toEqual(['c3', 'c2', 'c1', 'unknown'])
  expect(groups[0].contributions).toEqual([])
})

it('orders contributions newest first', () => {
  const g = groupContributionsByCycle(cycles, contributions)
  expect(g[1].contributions.map((c) => c.id)).toEqual(['c', 'b'])
})

it('never drops an unknown-cycle contribution', () => {
  const groups = groupContributionsByCycle(cycles, contributions)
  expect(groups.find((g) => g.key === 'unknown')?.contributions.map((c) => c.id)).toEqual(['d'])
  expect(groups.reduce((n, g) => n + g.contributions.length, 0)).toBe(contributions.length)
})