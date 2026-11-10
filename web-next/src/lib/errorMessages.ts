export type Problem = {
  title?: string;
  detail?: string;
  status?: number;
  correlationId?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: Problem | null,
    public readonly retryAfter: string | null = null,
  ) {
    super(problem?.title || `Request failed (${status})`);
    this.name = 'ApiError';
  }
  get correlationId(): string | null { return this.problem?.correlationId ?? null; }
}

export async function errorFromResponse(response: Response): Promise<ApiError> {
  const contentType = response.headers.get('content-type') || '';
  let problem: Problem | null = null;
  if (contentType.includes('json')) {
    try {
      const data: unknown = await response.clone().json();
      if (data && typeof data === 'object' && !Array.isArray(data)) problem = data as Problem;
    } catch { /* invalid problem body: fall back to status */ }
  }
  return new ApiError(response.status, problem, response.headers.get('Retry-After'));
}

// Supports both HTTP-date and delta-seconds. No guessed number for malformed headers.
export function parseRetryAfter(header: string | null, now = Date.now()): number | null {
  if (!header) return null;
  const trimmed = header.trim();
  if (/^\d+$/.test(trimmed)) {
    const seconds = Number(trimmed);
    return Number.isSafeInteger(seconds) ? seconds : null;
  }
  // HTTP dates should look like RFC 7231 IMF-fixdate, not arbitrary Date.parse input.
  if (!/^(Mon|Tue|Wed|Thu|Fri|Sat|Sun), \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(trimmed)) return null;
  const target = Date.parse(trimmed);
  return Number.isFinite(target) ? Math.max(0, Math.ceil((target - now) / 1000)) : null;
}

export type ErrorDescription = { message: string; advice: string; reference: string | null; retryInSeconds: number | null };
export function describeError(error: unknown, context: 'login' | 'stokvel' | 'general' = 'general'): ErrorDescription {
  if (!(error instanceof ApiError)) {
    return { message: "Can't reach RondiTrack.", advice: 'Check your connection and try again.', reference: null, retryInSeconds: null };
  }
  const reference = error.correlationId;
  if (error.status === 401 && context === 'login') return { message: 'Email or password is incorrect.', advice: 'Check your details and try again.', reference, retryInSeconds: null };
  if (error.status === 401) return { message: 'Your session has ended.', advice: 'Please sign in again.', reference, retryInSeconds: null };
  if (error.status === 404) return { message: 'Stokvel not found.', advice: 'Return to your stokvels.', reference, retryInSeconds: null };
  if (error.status === 403) return { message: 'You do not have permission for this action.', advice: 'Ask your stokvel Treasurer if you need access.', reference, retryInSeconds: null };
  if (error.status === 429) {
    const seconds = parseRetryAfter(error.retryAfter);
    return { message: seconds === null ? 'Too many requests.' : `Too many requests. Try again in ${seconds} seconds.`, advice: 'Wait before retrying.', reference, retryInSeconds: seconds };
  }
  if (error.status === 400 || error.status === 409 || error.status === 422) {
    return { message: error.problem?.detail || 'Please check the information you entered.', advice: 'Correct the fields and try again.', reference, retryInSeconds: null };
  }
  // Never leak a backend 500 detail to the member.
  return { message: 'Something went wrong.', advice: 'Please try again or contact support.', reference, retryInSeconds: null };
}
