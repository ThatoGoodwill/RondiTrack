import { describe, expect, it } from 'vitest'
import { formatCents } from './money'

const NBSP = String.fromCharCode(0xa0)

describe('formatCents', () => {
  it('shows ten cents', () => expect(formatCents(10)).toBe(`R${NBSP}0,10`))
  it('shows zero', () => expect(formatCents(0)).toBe(`R${NBSP}0,00`))
  it('shows 15050', () => expect(formatCents(15050)).toBe(`R${NBSP}150,50`))
  it('groups thousands', () =>
    expect(formatCents(1234567)).toBe(`R${NBSP}12${NBSP}345,67`))
  it('formats a negative', () => expect(formatCents(-150)).toBe(`-R${NBSP}1,50`))
  it('shows 99 cents', () => expect(formatCents(99)).toBe(`R${NBSP}0,99`))
  it('refuses a fraction of a cent', () =>
    expect(() => formatCents(10.5)).toThrow('not a whole number of cents'))
})
