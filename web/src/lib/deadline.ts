const HOUR = 3_600_000
const DAY = 24 * HOUR
const SAST = 2 * HOUR // South Africa has been UTC+2 all year, no daylight saving since 1944

/** Day number in Johannesburg. */
export const saDay = (ms: number) => Math.floor((ms + SAST) / DAY)

/**
 * "2026-10-31" is a South African calendar date. It is due at the END of that day
 * in Johannesburg: 2026-10-31T22:00:00Z.
 * Any other string is treated as an instant (has a time / Z / offset).
 */
export function deadlineFromDueDate(due: string): Date {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(due)
  if (m) return new Date(Date.UTC(+m[1], +m[2] - 1, +m[3]) + 22 * HOUR)
  return new Date(due)
}

/** 0 = due today, 1 = tomorrow, negative = overdue, in SA calendar days. */
export function daysUntilDue(due: string, now: Date): number {
  const deadline = deadlineFromDueDate(due)
  return saDay(deadline.getTime() - 1) - saDay(now.getTime())
}

/** Milliseconds from now to the next midnight in Johannesburg (24h at exactly midnight). */
export function msUntilNextSaMidnight(now: Date): number {
  return (saDay(now.getTime()) + 1) * DAY - SAST - now.getTime()
}