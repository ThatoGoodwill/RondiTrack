import { describe, expect, it } from 'vitest'
import { centsToInput, formatCents, MAX_CONTRIBUTION_CENTS, parseRandsToCents } from './money'
describe('exact South African money parsing', () => {
  it.each([['1500',150000],['1 500',150000],['1 500,50',150050],
    ['R1500',150000],['R 1 500,50',150050],['R\u00a01\u00a0500,50',150050],
    ['150,5',15050],['19,99',1999],['19.99',1999]])('parses %s', (text,cents) => {
    expect(parseRandsToCents(text)).toBe(cents)
  })
  it.each(['1,500.50','150,505','150.','', 'abc','-1','R -1,00','1000000,01','12 34'])
    ('rejects ambiguous money %s', s => { expect(() => parseRandsToCents(s)).toThrow() })
  it('roundtrips 100001 values and boundaries exactly', () => {
    for (let cents=0; cents<=100000; cents++) expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
    for (const cents of [0,1,99,100,150050,Number(MAX_CONTRIBUTION_CENTS)])
      expect(parseRandsToCents(formatCents(cents))).toBe(cents)
  })
  it('refuses fractional cents', () => { expect(()=>formatCents(1.25)).toThrow() })
})
