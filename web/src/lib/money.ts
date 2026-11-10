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
export function parseRandsToCents(input: string): number {
  const trimmed = input.trim()
  // No grouping or decimal comma: "1 500,50" is deliberately rejected.
  if (!/^(0|[1-9][0-9]*)(\.[0-9]{1,2})?$/.test(trimmed))
    throw new Error('Enter rands with at most two decimal places, for example 150.50.')
  const [rands, decimals = ''] = trimmed.split('.')
  const cents = BigInt(rands) * 100n + BigInt(decimals.padEnd(2, '0'))
  if (cents > BigInt(Number.MAX_SAFE_INTEGER)) throw new Error('Amount is too large.')
  return Number(cents)
}

/** Round-trippable, editable value without any currency sign or grouping. */
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) throw new Error('Invalid number of cents.')
  const whole = (BigInt(cents) / 100n).toString()
  const fraction = (BigInt(cents) % 100n).toString().padStart(2, '0')
  return `${whole}.${fraction}`
}
