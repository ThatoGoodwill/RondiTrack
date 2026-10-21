import { AlertTriangle, Banknote, Check, CircleDot, Clock, HelpCircle, Lock, ShieldCheck } from 'lucide-react'
import type { ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { formatCents } from '../lib/money'

type Look = { label: string; icon: ReactNode; tone: string }

const neutral = 'bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-slate-100'
const cycle: Record<string, Look> = {
  Open: { label: 'Open', icon: <CircleDot />, tone: 'bg-blue-100 text-blue-950 dark:bg-blue-950 dark:text-blue-100' },
  Closed: { label: 'Closed', icon: <Lock />, tone: neutral },
  PaidOut: { label: 'Paid out', icon: <Banknote />, tone: 'bg-purple-100 text-purple-950 dark:bg-purple-950 dark:text-purple-100' },
}
const contribution: Record<string, Look> = {
  Pending: { label: 'Pending', icon: <Clock />, tone: 'bg-amber-100 text-amber-950 dark:bg-amber-950 dark:text-amber-100' },
  Paid: { label: 'Paid (member says)', icon: <Check />, tone: 'bg-green-100 text-green-950 dark:bg-green-950 dark:text-green-100' },
  Confirmed: { label: 'Confirmed by treasurer', icon: <ShieldCheck />, tone: 'bg-emerald-200 text-emerald-950 dark:bg-emerald-900 dark:text-emerald-50' },
}

type Props =
  | { kind: 'cycle' | 'contribution'; status: string }
  | { kind: 'arrears'; cents: number | null | undefined }

export function StatusBadge(props: Props): ReactNode {
  let look: Look
  if (props.kind === 'arrears') {
    if (!props.cents || props.cents <= 0) return null // no arrears means no badge
    look = {
      label: `In arrears: ${formatCents(props.cents)}`,
      icon: <AlertTriangle />,
      tone: 'bg-red-100 text-red-950 dark:bg-red-950 dark:text-red-100',
    }
  } else {
    const table = props.kind === 'cycle' ? cycle : contribution
    look = table[props.status] ?? { label: props.status, icon: <HelpCircle />, tone: neutral }
  }
  return (
    <Badge variant="outline" className={`gap-1 whitespace-nowrap border-transparent ${look.tone}`}>
      <span aria-hidden="true" className="[&_svg]:size-3.5">{look.icon}</span>
      {look.label}
    </Badge>
  )
}