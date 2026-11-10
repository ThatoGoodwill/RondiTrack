/** Handful of projections used for display; the canonical schema is src/api/schema.d.ts. */
import type { components } from '@/api/schema'
export type Me = components['schemas']['MeResponse']
export type Home = components['schemas']['HomeResponse']
export type HomeStokvel = components['schemas']['HomeStokvelResponse']
export type Stokvel = components['schemas']['StokvelResponse']
export type Cycle = components['schemas']['ContributionCycleResponse']
export type Membership = components['schemas']['MembershipResponse']
export type Contribution = components['schemas']['ContributionResponse']
export type Page<T> = { items: T[]; nextPageToken: string | null }
export type Activity = { id: string; stokvelId: string; kind: string; occurredAt: string; amountCents: number | null; description: string }
