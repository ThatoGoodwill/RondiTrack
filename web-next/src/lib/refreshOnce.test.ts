import { describe, it, expect } from 'vitest'
import { singleFlight } from './singleFlight'
describe('server refresh single-flight primitive', () => {
  it('five parallel requests share one in-flight refresh', async () => {
    let calls = 0
    const refresh = singleFlight(async (_: string) => {
      calls++; await new Promise(resolve => setTimeout(resolve, 20)); return 'rotated'
    })
    expect(await Promise.all(Array.from({ length: 5 }, () => refresh('old')))).toEqual(Array(5).fill('rotated'))
    expect(calls).toBe(1)
  })
})
