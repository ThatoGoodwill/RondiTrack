const zar = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' })

export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents)) {
    throw new RangeError(`${cents} is not a whole number of cents`)
  }
  const sign = cents < 0 ? '-' : ''
  const absolute = Math.abs(cents)
  const remainder = absolute % 100
  const rands = (absolute - remainder) / 100
  // Intl accepts a BigInt rand value, so formatting does not turn cents into floating-point rands.
  const formattedWhole = zar.format(BigInt(rands))
  return `${sign}${formattedWhole.slice(0, -2)}${String(remainder).padStart(2, '0')}`
}


/** Input is a decimal string, never a binary floating-point rand value. */
export const MAX_CONTRIBUTION_CENTS = 9_000_000_000_000n
/** Exact money parsing: accepts SA groups, R prefix, comma or period decimals. No rounding. */
export function parseRandsToCents(input: string): number {
  const text = input.trim().replace(/\u00a0/g, ' ')
  const numeric = text.replace(/^R\s?/, '').trim()
  // A grouping separator is a space, never a comma or period.
  if (!/^(?:0|[1-9]\d*|[1-9]\d{0,2}(?: \d{3})+)(?:[,.]\d{1,2})?$/.test(numeric))
    throw new Error('Enter rands, for example R 1 500,50 (at most two decimals).')
  const raw = numeric.replace(/ /g, '')
  const [rands, fraction = ''] = raw.split(/[,.]/)
  const cents = BigInt(rands) * 100n + BigInt(fraction.padEnd(2, '0'))
  if (cents > MAX_CONTRIBUTION_CENTS || cents > BigInt(Number.MAX_SAFE_INTEGER))
    throw new Error('Amount exceeds the API limit.')
  return Number(cents)
}

/** Round-trippable, editable value without any currency sign or grouping. */
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) throw new Error('Invalid number of cents.')
  const whole = (BigInt(cents) / 100n).toString()
  const fraction = (BigInt(cents) % 100n).toString().padStart(2, '0')
  return `${whole}.${fraction}`
}
