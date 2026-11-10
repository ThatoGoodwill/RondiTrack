import { validIsoDate } from './southAfricanDates';
export type HistoryFilters = { member: string | null; from: string | null; to: string | null; sort: 'newest' | 'oldest' };
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
export function parseHistoryFilters(input: URLSearchParams): HistoryFilters {
  const member = input.get('member');
  const from = input.get('from');
  const to = input.get('to');
  const validFrom = from && validIsoDate(from) ? from : null;
  const validTo = to && validIsoDate(to) ? to : null;
  return {
    member: member && UUID.test(member) ? member : null,
    from: validFrom,
    to: validFrom && validTo && validTo < validFrom ? null : validTo,
    sort: input.get('sort') === 'oldest' ? 'oldest' : 'newest',
  };
}
export function nextPageToken(token: string | null | undefined): string | undefined {
  return token || undefined;
}
export function historyQueryKey(stokvelId: string, filter: HistoryFilters): readonly unknown[] {
  return ['contributions', stokvelId, filter.member, filter.from, filter.to, filter.sort] as const;
}
