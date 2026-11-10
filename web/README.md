# RondiTrack Vite — Assignments 7.1 through 7.4

This `web/` app is deliberately retained alongside `../web-next/`, as required by Assignment 8.1. The repository ZIP already contained much of Assignment 7.3: `auth/session.ts`, `session.test.ts`, typed API client, TanStack queries and component tests. This update adds the 7.4 contribution form and fixes the 401 race in `authFetch`.

## Running it

Use Node.js 24, run the .NET API at <http://localhost:8080>, then `npm install` and `npm run dev` in this folder. `npm install` is needed to update the existing lockfile for pinned `react-hook-form`, Zod, resolver and MSW dependencies. Re-run `npm run api:types` from the live API and commit the generated schema before submission.

## API contract notes (7.3)

The downloaded .NET source configures exact-origin CORS with credentials, exposes `Retry-After`, `Idempotency-Replayed`, and `X-Correlation-Id`, and has an HttpOnly rotating `refresh_token` cookie on `/api/v1/auth`. `/me` and `/me/home` are present; `/me/home` is the authoritative per-stokvel role source. The contribution paging response is `{items, nextPageToken}` and uses `null` to end. Reuse of a spent refresh token revokes the family. **Confirm each against the running API** with DevTools/curl before submitting.

## Session, single-flight proof (7.3)

`src/auth/session.ts` keeps access token in memory only. `refreshAccessToken` is single-flight within a browser tab. `authFetch` clones requests before first send so POST bodies survive a 401 retry, and uses a token another concurrent request just refreshed instead of rotating a second time. `session.test.ts` covers the five required cases. Cross-tab refresh races remain a documented limitation of this Week 7 architecture; Next's proxy handles the later week.

## Switching stokvels (7.3)

TanStack keys are stokvel-scoped. `HomePage` renders a new stokvel's data only after its queries resolve, and uses no previous-data placeholder across stokvels. The repository already contains `HomePage.test.tsx` for switching. The query client does not retry 4xx errors. Sign-out calls `queryClient.clear()` and resets page local selection on remount; verify TanStack Devtools with a shared-phone test.

## Error contract (7.3)

`lib/errorMessages.ts` handles validation, 401, 403, 404, 429, network and 5xx. It uses `Retry-After` and a selectable correlation reference. The generic 5xx screen must never expose server exception detail. See existing `errorMessages.test.ts` and the API source `RondiTrackExceptionHandler`.

## Idempotency contract (7.4)

The newly added API endpoint is `POST /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions`; it takes integer `amountInCents` and an `Idempotency-Key` header. The previous endpoint remains backwards compatible. The service uses a required header, a fingerprint of all payment fields and a unique contribution constraint. A replay uses existing `200` plus `Idempotency-Replayed: true`; a reused key with different amount returns 409; a member who already paid once gets 409; missing key gets 400; preflight includes the key. CORS exposes replay and Retry-After. Re-test 429 and preflight with the real API; those outcomes are **not yet independently verified**.

The form uses a `ContributionIntentKey` object within a ref, one per mounted form. An unchanged retry after a network failure, 5xx, 429, 401 or validation answer keeps the same key. A changed amount, member or cycle starts a new key; success also advances to a new key. React StrictMode's double render does not create a payment because key generation happens only inside the submission callback. Reload mid-flight loses the form's key by design (a stretch challenge); the unique contribution index still stops a second record if the first landed, but the user may see 409 instead of a replay.

## Money in, money out

`parseRandsToCents('150.50') === 15050`, `'150.5' === 15050`, `'19.99' === 1999`; invalid `'150.505'` is rejected rather than rounded. Grouping (`1 500,50`) is **not** accepted. The reverse `centsToInput()` renders editable decimal text, and the round-trip test checks cents 0–100000. No floating-point multiplication is used for input amounts. `formatCents` accepts integer cents and formats a BigInt rand portion with `en-ZA` currency style.

## The form (7.4)

`RecordContributionForm` uses React Hook Form, a Zod resolver, TanStack `useMutation` and the typed `openapi-fetch` client. It displays only on Open cycles. A member records their own payment; the selector is shown only for a Treasurer or Admin **of that stokvel**. The backend remains the authorization authority. 400 errors are mapped to fields, 409/422 show problem detail, 429 shows the wait, and all API failures expose correlationId where available. The submit button remains disabled through API response and query invalidations.

The API's existing membership response only exposes member ids, not display names; the temporary UI shows abbreviated ids. Improve the authenticated membership projection if the reviewer expects names. The `CycleContributionPaths` type is a temporary local extension matching the patched controller, not a replacement for regenerating the canonical OpenAPI client.

## Idempotency proof

`src/contributions/recordContribution.test.tsx` contains six MSW 2 tests (network retry, next key, 429, edited amount, 401 retry, integer cents), with `onUnhandledRequest:'error'`. Tests have **not yet been executed** because the preparation environment cannot install packages. Run `npm test -- --run` after installing dependencies. For the deliberate-failure evidence, temporarily move the key generation inside each outgoing request, run and capture the failing MSW test, undo the bug, and capture the passing run. Do not commit the broken variant.

## Optimistic or not

Recording money is deliberately **not optimistic**: a 409 from the server could otherwise leave a false paid total on screen while the member or Treasurer acts on it. Only the server can know a contribution's durable id, whether the intent replayed, and the new collected amount; the word "Recorded" must mean the API confirmed the write, not merely that the browser sent a request.

## Known gaps

- The independent build, lint, npm audit, MSW suite and live API checks remain outstanding; do not claim a full pass.
- The existing package lock must be regenerated and pinned after `npm install`.
- `src/api/schema.d.ts` must be regenerated from the patched running backend; the temporary typed extension is not the final generated client.
- Complete browser screenshots and real 429, shared-phone and idempotency preflight demonstrations are still required for coursework submission.
- Multi-tab refresh coalescing in Vite is not implemented; Next's server-side session supersedes this Vite design in Week 8.
