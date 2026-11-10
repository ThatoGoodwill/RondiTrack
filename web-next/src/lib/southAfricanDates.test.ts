import { describe, it, expect } from 'vitest'
import { southAfricanDaysToUtcRange, validIsoDate, displaySouthAfricanDate } from './southAfricanDates'
describe('RondiTrack SAST calendar boundaries', () => {
  it('October in Johannesburg includes 31 October at 23:30 SAST, excludes 1 Nov at midnight', () => {
    const range = southAfricanDaysToUtcRange('2026-10-01','2026-10-31')
    expect(range).toEqual({ from: '2026-09-30T22:00:00.000Z', to: '2026-10-31T22:00:00.000Z' })
    expect('2026-10-31T21:30:00.000Z' >= range.from!).toBe(true)
    expect('2026-10-31T21:30:00.000Z' < range.to!).toBe(true)
    expect('2026-10-31T22:00:00.000Z' < range.to!).toBe(false)
    expect(displaySouthAfricanDate('2026-10-31T21:30:00Z')).toMatch(/31/)
  })
  it('a single day covers 24 hours in the fixed SAST time zone', () => {
    const range = southAfricanDaysToUtcRange('2026-10-31', '2026-10-31')
    expect(Date.parse(range.to!) - Date.parse(range.from!)).toBe(86_400_000)
  })
  it('open-ended ranges, inverted ranges and nonexistent dates', () => {
    expect(southAfricanDaysToUtcRange('2026-10-31', undefined).to).toBeUndefined()
    expect(southAfricanDaysToUtcRange(undefined, '2026-10-31').from).toBeUndefined()
    expect(() => southAfricanDaysToUtcRange('2026-11-01','2026-10-31')).toThrow()
    expect(validIsoDate('2026-02-30')).toBe(false)
  })
})
