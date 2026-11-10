/** Accept only local paths; reject protocol-relative and backslash redirects. */
export function safeNextPath(value: string | null | undefined, fallback = '/dashboard/member'): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || /[\\\x00-\x1f\x7f]/.test(value)) return fallback
  try {
    const url = new URL(value, 'https://ronditrack.invalid')
    return url.origin === 'https://ronditrack.invalid' && !url.pathname.startsWith('//')
      ? `${url.pathname}${url.search}${url.hash}` : fallback
  } catch { return fallback }
}
