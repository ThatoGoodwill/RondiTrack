import { describe, it, expect } from 'vitest'
import { parseRandsToCents, centsToInput } from '../lib/money'
describe('exact rands and cents conversion', () => {
  it('rejects rounding and non-standard input', () => {
    expect(parseRandsToCents('150.50')).toBe(15050)
    expect(parseRandsToCents('150.5')).toBe(15050)
    expect(parseRandsToCents('19.99')).toBe(1999)
    expect(() => parseRandsToCents('150.505')).toThrow()
    expect(() => parseRandsToCents('1 500,50')).toThrow()
    expect(() => parseRandsToCents('1e2')).toThrow()
  })
  it('round trips every cent from zero to 100000', () => {
    for (let cents = 0; cents <= 100000; cents++)
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
  })
})
