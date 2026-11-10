/** Temporary local contract extension for the newly added .NET endpoint.
 * Regenerate schema.d.ts from the live API with npm run api:types after starting it.
 */
import type { components } from './schema'
type Problem = components['schemas']['ProblemDetails']
type Recorded = components['schemas']['ContributionResponse']
type Content<T> = { headers: Record<string, unknown>; content: { 'application/json': T } }
export type CycleContributionPaths = {
  '/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions': {
    parameters: { path: { stokvelId: string; cycleId: string }; query?: never; header?: never; cookie?: never }
    post: {
      parameters: {
        path: { stokvelId: string; cycleId: string }
        header: { 'Idempotency-Key': string }
        query?: never; cookie?: never
      }
      requestBody: { content: { 'application/json': { userId: string; amountInCents: number } } }
      responses: {
        200: Content<Recorded>; 201: Content<Recorded>;
        400: Content<Problem>; 401: Content<Problem>; 403: Content<Problem>;
        409: Content<Problem>; 422: Content<Problem>; 429: Content<Problem>
      }
    }
  }
}
