import { expect, it } from 'vitest'
import { formatCalendarDate, formatInstant } from './dates'

it('shows the midnight payment in Johannesburg time', () => {
  expect(formatInstant('2026-10-31T22:30:00Z')).toBe('01 Nov 2026, 00:30')
})

it('does not shift a calendar date', () => {
  expect(formatCalendarDate('2026-10-31')).toBe('31 Oct 2026')
})
