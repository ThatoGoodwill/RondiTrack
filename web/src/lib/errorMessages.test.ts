import { describe, expect, it } from 'vitest'
import { parseRetryAfter, describeError } from './errorMessages'
import { ApiError } from '../auth/session'

describe('parseRetryAfter', () => {
  it('parses seconds', () => {
    expect(parseRetryAfter('23')).toBe(23)
    expect(parseRetryAfter('0')).toBe(0)
  })

  it('returns null for empty or missing header', () => {
    expect(parseRetryAfter(null)).toBeNull()
    expect(parseRetryAfter('')).toBeNull()
    expect(parseRetryAfter('   ')).toBeNull()
  })

  it('parses an HTTP date in the future', () => {
    const now = Date.now()
    const future = new Date(now + 30_000).toUTCString()
    const result = parseRetryAfter(future)
    expect(result).not.toBeNull()
    expect(result).toBeGreaterThanOrEqual(28)
    expect(result).toBeLessThanOrEqual(30)
  })

  it('returns 0 for an HTTP date in the past', () => {
    const past = new Date(Date.now() - 60_000).toUTCString()
    expect(parseRetryAfter(past)).toBe(0)
  })

  it('returns null for garbage', () => {
    expect(parseRetryAfter('not a date')).toBeNull()
  })
})

describe('describeError', () => {
  it('404 says Stokvel not found and carries the correlationId', () => {
    const err = new ApiError(404, { correlationId: 'abc123' }, null)
    const d = describeError(err)
    expect(d.title).toBe('Stokvel not found')
    expect(d.correlationId).toBe('abc123')
    expect(d.canRetry).toBe(false)
  })

  it('429 shows the wait time from Retry-After', () => {
    const err = new ApiError(429, { correlationId: 'xyz' }, '23')
    const d = describeError(err)
    expect(d.title).toBe('Too many requests')
    expect(d.message).toContain('23')
    expect(d.retryAfterSeconds).toBe(23)
  })

  it('500 shows a generic message and never leaks detail', () => {
    const err = new ApiError(500, { detail: 'System.NullReferenceException: ...', correlationId: 'ref1' }, null)
    const d = describeError(err)
    expect(d.title).toBe('Something went wrong on our side')
    expect(d.message).not.toContain('Exception')
    expect(d.correlationId).toBe('ref1')
  })

  it('non-ApiError returns a generic description', () => {
    const d = describeError(new Error('boom'))
    expect(d.title).toBe('Something went wrong')
    expect(d.correlationId).toBeNull()
  })
})