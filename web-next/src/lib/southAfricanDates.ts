/** Convert local SAST calendar dates to an exclusive UTC interval. Pure and TZ-independent. */
const SAST_OFFSET_MS = 2 * 60 * 60 * 1000;
export function validIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  if (year < 1000 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31) return false;
  const d = new Date(Date.UTC(year, month - 1, day));
  return d.getUTCFullYear() === year && d.getUTCMonth() + 1 === month && d.getUTCDate() === day;
}
function startUtc(date: string, dayDelta = 0): string {
  if (!validIsoDate(date)) throw new Error(`Invalid date: ${date}`);
  const [year, month, day] = date.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day + dayDelta) - SAST_OFFSET_MS).toISOString();
}
export function southAfricanDaysToUtcRange(from?: string | null, to?: string | null): { from?: string; to?: string } {
  if (from && !validIsoDate(from)) throw new Error('Invalid from date');
  if (to && !validIsoDate(to)) throw new Error('Invalid to date');
  if (from && to && to < from) throw new Error('To date precedes from date');
  return { ...(from ? { from: startUtc(from) } : {}), ...(to ? { to: startUtc(to, 1) } : {}) };
}
export function displaySouthAfricanDate(instant: string): string {
  return new Intl.DateTimeFormat('en-ZA', { timeZone: 'Africa/Johannesburg', year: 'numeric', month: 'long', day: 'numeric' }).format(new Date(instant));
}
