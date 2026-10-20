import type { ContributionItem, MeHomeEntry } from '../api/types'

// Captured from the real API on 2026-10-20, logged in as lerato.mokoena@example.com.
//   GET /api/v1/me/home
//   GET /api/v1/stokvels/01a156b2-383d-7434-8703-9cdb57228788/contributions
// The annotations check the captured shape against the generated contract; if the API
// changes, npm run typecheck fails here.
export const home: MeHomeEntry[] = [
  {
    stokvelId: '01a156b2-383d-7434-8703-9cdb57228788',
    name: 'Ubuntu Savers',
    role: 'Member',
    currentCycle: {
      id: '01a156b2-3892-7ce7-8557-70eac36e6aad',
      label: '2026-09',
      paidCents: 10,
      owedCents: 49990,
      status: 'Open',
      endsOn: null,
    },
    nextPayoutDueUtc: '2026-11-20T00:24:42.898021Z',
  },
  {
    stokvelId: '01a156b2-383e-74fd-81fe-93bfe9ae6954',
    name: 'Umoja Grocery Club',
    role: 'Treasurer',
    currentCycle: {
      id: '01a156b2-3892-7402-a0e5-8b5c2b4898be',
      label: '2026-09',
      paidCents: 35050,
      owedCents: 0,
      status: 'Open',
      endsOn: null,
    },
    nextPayoutDueUtc: '2026-11-20T00:24:42.898233Z',
  },
]

export const contributions: ContributionItem[] = [
  {
    id: '01a156b2-38d9-774f-9490-eeffadb1a089',
    stokvelId: '01a156b2-383d-7434-8703-9cdb57228788',
    userId: '01a156b2-3404-71bf-88c7-4f24100828c6',
    contributionCycleId: '01a156b2-3892-7ce7-8557-70eac36e6aad',
    amountCents: 50000,
    recordedAt: '2026-10-20T00:24:42.969794+00:00',
    cycleLabel: '2026-09',
  },
  {
    id: '01a156b2-38bd-7f5d-b6ea-1f43e247d85a',
    stokvelId: '01a156b2-383d-7434-8703-9cdb57228788',
    userId: '01a156b2-352e-7f98-bc73-6d6e3bd222b7',
    contributionCycleId: '01a156b2-3892-7ce7-8557-70eac36e6aad',
    amountCents: 10,
    recordedAt: '2026-10-20T00:24:42.941297+00:00',
    cycleLabel: '2026-09',
  },
]
