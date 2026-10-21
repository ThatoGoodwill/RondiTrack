import { describe, expect, it } from 'vitest'
import { daysUntilDue, deadlineFromDueDate, msUntilNextSaMidnight } from './deadline'
import { countdownText } from './countdown'

describe('deadlineFromDueDate', () => {
  it('ends the day at 22:00Z, not UTC midnight', () => {
    expect(deadlineFromDueDate('2026-10-31').toISOString()).toBe('2026-10-31T22:00:00.000Z')
  })
})

describe('daysUntilDue', () => {
  it('is due today at 23:30 SAST on the due date', () =>
    expect(daysUntilDue('2026-10-31', new Date('2026-10-31T21:30:00Z'))).toBe(0))
  it('is overdue by 1 day one second after the deadline', () =>
    expect(daysUntilDue('2026-10-31', new Date('2026-10-31T22:00:01Z'))).toBe(-1))
  it('is due today at 00:30 SAST on the 31st', () =>
    expect(daysUntilDue('2026-10-31', new Date('2026-10-30T22:30:00Z'))).toBe(0))
  it('is due tomorrow the day before', () =>
    expect(daysUntilDue('2026-10-31', new Date('2026-10-30T21:59:00Z'))).toBe(1))
})

describe('countdownText', () => {
  it('words each case', () => {
    expect(countdownText(0)).toBe('Due today')
    expect(countdownText(1)).toBe('Due tomorrow')
    expect(countdownText(5)).toBe('Due in 5 days')
    expect(countdownText(-1)).toBe('Overdue by 1 day')
    expect(countdownText(-3)).toBe('Overdue by 3 days')
  })
})

describe('msUntilNextSaMidnight', () => {
  it('is 30 minutes from 23:30 SAST', () =>
    expect(msUntilNextSaMidnight(new Date('2026-10-31T21:30:00Z'))).toBe(30 * 60_000))
  it('is 24 hours from exactly midnight, not 0', () =>
    expect(msUntilNextSaMidnight(new Date('2026-10-31T22:00:00Z'))).toBe(24 * 3_600_000))
})