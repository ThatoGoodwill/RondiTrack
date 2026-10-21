import { countdownText } from '../lib/countdown'
import { daysUntilDue } from '../lib/deadline'
import { useSaNow } from '../hooks/useSaNow'

export function DueCountdown({ dueDate, status }: { dueDate: string; status: string }) {
  const now = useSaNow()
  if (status !== 'Open') return null // Closed / PaidOut: the badge already says what happened
  return <span>{countdownText(daysUntilDue(dueDate, now))}</span>
}