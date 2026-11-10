import { api, unwrap } from '../api/client'
import type { components } from '../api/schema'
export type ContributionSubmission = {
  stokvelId: string; cycleId: string; memberId: string
  amountInCents: number; idempotencyKey: string
}
export async function recordContribution(input: ContributionSubmission) {
  return unwrap<components['schemas']['ContributionResponse']>(
    api.POST('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
      params: {
        path: { stokvelId: input.stokvelId, cycleId: input.cycleId },
        header: { 'Idempotency-Key': input.idempotencyKey },
      },
      body: { userId: input.memberId, amountInCents: input.amountInCents },
    }),
  )
}
