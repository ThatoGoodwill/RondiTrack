import { ApiError } from '../auth/session'

export type ErrorDescription = {
  title: string
  message: string
  correlationId: string | null
  retryAfterSeconds: number | null
  canRetry: boolean
}

/**
 * Parse a Retry-After header. The header may be:
 *  - a number of seconds ("23")
 *  - an HTTP date ("Wed, 21 Oct 2026 07:28:00 GMT")
 *  - garbage
 * Returns the number of seconds to wait, or null if the header is missing/invalid/past.
 */
export function parseRetryAfter(header: string | null): number | null {
  if (!header) return null
  const trimmed = header.trim()
  if (trimmed === '') return null

  // Number of seconds
  if (/^\d+$/.test(trimmed)) {
    const seconds = parseInt(trimmed, 10)
    return seconds >= 0 ? seconds : null
  }

  // HTTP date
  const asDate = new Date(trimmed)
  if (!isNaN(asDate.getTime())) {
    const seconds = Math.max(0, Math.floor((asDate.getTime() - Date.now()) / 1000))
    return seconds
  }

  return null
}

/**
 * Turn an unknown error (usually ApiError) into a description a member can read.
 */
export function describeError(error: unknown): ErrorDescription {
  if (!(error instanceof ApiError)) {
    return {
      title: 'Something went wrong',
      message: 'Please try again. If it keeps happening, contact support.',
      correlationId: null,
      retryAfterSeconds: null,
      canRetry: true,
    }
  }

  const body = (error.body ?? {}) as { title?: string; detail?: string; correlationId?: string }
  const correlationId = body.correlationId ?? null

  switch (error.status) {
    case 400:
      return {
        title: 'Please check the fields',
        message: body.detail ?? 'One or more fields are invalid.',
        correlationId,
        retryAfterSeconds: null,
        canRetry: false,
      }

    case 401:
      return {
        title: 'Email or password is incorrect',
        message: 'Please check your details and try again.',
        correlationId,
        retryAfterSeconds: null,
        canRetry: false,
      }

    case 403:
      return {
        title: 'You do not have permission',
        message: body.detail ?? 'You are signed in, but this action is not allowed for your role.',
        correlationId,
        retryAfterSeconds: null,
        canRetry: false,
      }

    case 404:
      return {
        title: 'Stokvel not found',
        message:
          'This stokvel may have been removed, or you may not be a member. Check your stokvel list.',
        correlationId,
        retryAfterSeconds: null,
        canRetry: false,
      }

    case 429: {
      const seconds = parseRetryAfter(error.retryAfter)
      const message =
        seconds === null
          ? 'Too many requests. Please try again later.'
          : `Too many requests. Try again in ${seconds} second${seconds === 1 ? '' : 's'}.`
      return {
        title: 'Too many requests',
        message,
        correlationId,
        retryAfterSeconds: seconds,
        canRetry: seconds === 0,
      }
    }

    case 500:
    case 502:
    case 503:
    case 504:
      return {
        title: 'Something went wrong on our side',
        message: 'Please try again. If it keeps happening, contact support with the reference below.',
        correlationId,
        retryAfterSeconds: null,
        canRetry: true,
      }

    default:
      return {
        title: 'Something went wrong',
        message: 'Please try again.',
        correlationId,
        retryAfterSeconds: null,
        canRetry: true,
      }
  }
}