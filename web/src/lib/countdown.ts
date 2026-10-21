export function countdownText(days: number): string {
  if (days === 0) return 'Due today'
  if (days === 1) return 'Due tomorrow'
  if (days > 1) return `Due in ${days} days`
  const late = Math.abs(days)
  return `Overdue by ${late} ${late === 1 ? 'day' : 'days'}`
}