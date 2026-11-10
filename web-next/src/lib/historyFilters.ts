import { createLoader, createParser, parseAsStringLiteral } from 'nuqs/server'
import { validIsoDate } from './southAfricanDates'
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
const asMember = createParser({ parse: value => uuid.test(value) ? value : null,
  serialize: value => value })
const asDate = createParser({ parse: value => validIsoDate(value) ? value : null,
  serialize: value => value })
export const historyParsers = {
  member: asMember.withDefault(''),
  from: asDate.withDefault(''),
  to: asDate.withDefault(''),
  sort: parseAsStringLiteral(['newest', 'oldest'] as const).withDefault('newest'),
}
export type HistoryFilters = { member: string; from: string; to: string; sort: 'newest' | 'oldest' }
export const loadHistoryFilters = createLoader(historyParsers)
export function normalizedFilters(filters: HistoryFilters): HistoryFilters {
  return filters.from && filters.to && filters.to < filters.from ? { ...filters, to: '' } : filters
}
export function historyKey(stokvelId: string, f: HistoryFilters): readonly unknown[] {
  return ['contributions', stokvelId, f.member, f.from, f.to, f.sort] as const
}
