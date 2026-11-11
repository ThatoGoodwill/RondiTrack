import { describe, it, expect } from 'vitest'
import { centsToInput, parseRandsToCents, formatCents } from './money'
describe('Week 9 South African rands',()=>{
  it.each([['1500',150000],['1 500',150000],['1 500,50',150050],['R 1 500,50',150050],
    ['R\u00a01\u00a0500,50',150050],['150,5',15050],['19.99',1999 ]])('parses %s without floating-point rands',(s,cents)=>expect(parseRandsToCents(s)).toBe(cents))
  it.each(['1,500.50','12 34','150,505','-1','','x','1000000,01'])('refuses ambiguity %s',s=>expect(()=>parseRandsToCents(s)).toThrow())
  it('roundtrips all cents to R1000',()=>{
    for(let cents=0;cents<=100000;cents++)
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
    expect(parseRandsToCents(formatCents(150050))).toBe(150050)
  })
})
