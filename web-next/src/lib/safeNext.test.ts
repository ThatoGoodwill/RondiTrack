import { describe, expect, it } from 'vitest';
import { safeNextPath } from './safeNext';
it('keeps good on-site return paths and rejects offsite redirects', () => {
  expect(safeNextPath('/stokvels/123?from=2026-10-01')).toBe('/stokvels/123?from=2026-10-01');
  for (const value of ['//evil.example', '/\\evil.example', 'https://evil.example', 'javascript:alert(1)', '/\n/evil']) {
    expect(safeNextPath(value)).toBe('/dashboard/member');
  }
});
