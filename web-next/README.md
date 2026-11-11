# RondiTrack — Next.js (Assignments 8.1–8.3)

> **Verification status: code prepared, NOT verified end to end.** This project was created against the 2026-10-22 RondiTrack ZIP. The development environment used to prepare it had Node 22 but **no npm registry access and no .NET SDK**. Do **not** claim `npm audit`, `npm run lint`, `npm run build`, `npm test`, or the backend integration tests have passed. Generate `package-lock.json` and OpenAPI types on your machine and paste actual results into the pull request.

## Running it

1. Start the PostgreSQL database and the original ASP.NET Core API in the repository root, using the root README. Apply the migrations before opening the frontend.
2. Use Node.js 24 (see Vite `web/` requirement); Next.js also supports Node.js 22 on its own.
3. `cd web-next && npm install` (creates a **new** package-lock.json). Save and commit the lockfile. Run `npm audit` and address findings before submission. Do not change pinned versions without reviewing advisories and the assignment's >=two-weeks-old rule.
4. `cp .env.example .env.local`. Set `API_BASE_URL=http://localhost:8080` and generate `SESSION_SECRET` with `openssl rand -base64 32`. Do not commit `.env.local` or use `NEXT_PUBLIC_` for secrets.
5. After the API starts, run `npm run api:types` and commit the regenerated `src/api/schema.d.ts` (the version provided with the ZIP is an older snapshot).
6. `npm run typecheck && npm run lint && npm test && npm run build && npm run dev`.
7. Open <http://localhost:3000>. No browser request should go directly to port 8080; `/bff/*` requests are to Next.js itself.

## API check

Source inspection of the supplied ZIP, **not live curl verification**:

| Check | What the source implements | Still to test live |
| --- | --- | --- |
| `/me/home` roles | `HomeStokvelResponse.Role` gives a role per stokvel | Member-only and Treasurer+Member accounts |
| `/stokvels/{id}` for non-members | Global `StokvelScopeFilter` protects routes | Non-member, missing UUID, malformed UUID |
| Login refresh cookie | `AuthController` sends `refresh_token` in `Set-Cookie`, `HttpOnly`, `Secure`, `SameSite=Strict`, path `/api/v1/auth` | Verify with Node `headers.getSetCookie()` |
| Token lifetimes | Defaults: 10-minute access token and 7-day refresh | Check running `Jwt` configuration |
| Shared server IP for login | All Next logins originate from the server's IP | Verify rate limits with multiple users |
| Stokvel summary | `StokvelResponse` has `memberCount`; current cycle is from `/me/home` or `/cycles` | Verify against seeded users |
| Registration | `POST /api/v1/auth/register` already exists | Conflict response and 18+ validation |
| Activity/feed | New `GET /api/v1/activity` added | Authorization and cursor paging |
| Contribution filters | New `member`, `from`, `to` query filters added | Malformed dates and mismatched cursor |

### API changes this work adds

- `POST /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions` takes `{userId, amountInCents}` and the typed `Idempotency-Key` header. Existing old decimal-rand POST remains for backwards compatibility. Only Open cycles accept contributions. Success is `201`, replay is `200` with `Idempotency-Replayed: true` (the API's existing behavior).
- `GET /api/v1/stokvels/{stokvelId}/contributions` accepts `member`, `from` and `to` (UTC instants, inclusive lower bound and **exclusive** upper bound). The cursor fingerprint contains all filters, so reusing a cursor with other filters is `400`.
- `GET /api/v1/activity?pageSize=10&pageToken=...` is a member-scoped, newest-first feed of contributions, payouts and joined members. It is calculated only from the caller's stokvel memberships. The server does not cache these queries.
- The backend now saves each new contribution and idempotency receipt within one database transaction; verify the original integration tests before merging.

## Routes

| Route | Data source (Next server -> API) | Empty / unauthorized behavior |
| --- | --- | --- |
| `/` | Encrypted session | Member dashboard or sign in |
| `/login`, `/register` | Server Actions | Inline error, correlation reference |
| `/dashboard/member` | `/me/home` | Helpful empty state |
| `/dashboard/treasurer` | `/me/home` filtered on each stokvel's `role === 'Treasurer'` | Empty state, never a global 403 |
| `/stokvels` | `/me/home` | Empty state |
| `/stokvels/[id]` | Detail, cycles, members and home in parallel; contributions streamed | 404 => non-disclosing not-found screen |
| `/stokvels/[id]/contributions` | API filters, server page 1; BFF later pages | No matching contributions -> empty state |
| `/activity` | Server initial page; BFF later pages | Empty state |

## The treasurer dashboard

The account has **no global Treasurer role**. The filter is `stokvelsWhereTreasurer(home.stokvels)` and a member who is Treasurer nowhere sees an empty dashboard, not a 403. The API remains the authorization authority on every write. The UI allows Admin users to select a paying member too, matching the backend's authorization policy; verify this against the curriculum's Treasurer-only wording before submitting.

## The client boundary

Only interactive components use `'use client'`: `LoginForm`, `RegisterForm`, `ClearOldPins`, `PinMenu`, `SignOutForm`, `ContributionForm`, `HistoryBrowser`, `ActivityBrowser`, `AppQueryProvider`, and `store/pins.tsx`. No client component imports the server's session object or API token. The session, API and history server modules import `server-only`. Inspect the rendered HTML for token prefixes and verify the negative build test before claiming the guard is proven.

## Server-only proof

**Not captured:** after dependencies are installed, intentionally import `getSession` into a `'use client'` component, run `npm run build`, record the build error, undo the import, then capture the passing build. Never leave the intentional bad import committed.

## Cache Components

`cacheComponents: true` and `partialPrefetching: true` are configured in `next.config.ts`. Data-dependent route content is below `<Suspense>`; no user-specific API result has a `use cache` directive. **Build output and Next.js Insight traces are pending.**

## Parallel and streamed

`/stokvels/[id]` starts its recent-contributions promise, requests detail, cycles, member page and home via `Promise.all`, then streams recent contributions behind another Suspense. **The sequential-versus-parallel timing numbers have not been measured**; collect both from a running API and paste them here.

## Contributions

One payment intent = one key. The Server Action remembers the intent fingerprint between failed submissions and reuses the same server-generated key for an unchanged retry after a dropped connection, 5xx, 429, or validation response. Changing amount/member/cycle issues a new key; success issues a new key. It parses rands with a no-floating-point decimal-to-integer function, sends only `amountInCents`, returns field/HTTP errors with correlation IDs, and calls Next's `refresh()` after confirmed success. A double-submit button is disabled while pending, while the backend's database unique constraint and idempotency receipt protect against duplicate money writes. The Vite assignment retains its own React Hook Form and `useMutation` implementation.

The key is a hidden form field initially generated by the Next server. A forced request as a Member for another member must still produce a backend 403; the page does not sign the person out for a 403. **Capture this test and screenshot.**

## What we cache

| Data | Cache location | Why |
| --- | --- | --- |
| Member's owed balance | **Not cached on server** | It belongs to one caller |
| Stokvel detail, cycle total and recent contributions | **Not cached on server** | Protected membership data with changing money |
| Contribution history and activity pages | Browser TanStack Query, keyed by full filter / initial data | User sees later pages, cleared on navigation to login; never a server-global cache |
| Stokvel pins | Zustand local persistence, ids and names only | Client preference, removed on sign-out or sign-in page |

No access token, email, personal balance or other personal data appears in a cache key or cache tag. There are no `use cache`, `use cache: private` or `cacheTag` functions.

## Sessions and refresh

One encrypted `session` cookie (`jose` direct/A256GCM, HttpOnly, Secure in production, SameSite Lax, path `/`) holds the access token, rotated refresh token and display identity. The browser cannot read them. Token expiry uses the API's explicit `expiresAt` value, not a guessed interval. `proxy.ts` refreshes near access-token expiry (default 60 seconds) and writes the new encrypted cookie to both the **incoming request** and the **response**. Both Server Components and Server Actions check their own session.

For sign-in redirects, `safeNextPath` rejects protocol-relative URLs, backslashes, embedded controls and absolute external URLs. Registration succeeds by redirecting to sign-in, not by silently logging in. Password managers and paste are supported.

Sign-out calls API logout and deletes the session even when the API call fails. `/bff` unauthenticated responses are JSON 401, not HTML redirects.

## The race

The API revokes the full refresh family when it sees a spent token a second time. `refreshOnce` coalesces in-process refresh requests and remembers one rotation for 15 seconds so stale requests in a single server instance can reuse it. **Limitations:** this is process-local; it does not protect multiple independent Next.js instances/isolates or a very late request arriving after the 15-second grace window. A distributed lock or a narrowly bounded, audited API reuse grace period is needed for multi-instance production. This is **not** a completed production proof. Capture 3 parallel tabs, the `Promise.all` console test, API logs, and a 20-minute active session when the API is running; do not state those tests passed until they have.

## Treasurer actions

The paying-member selector appears only if the member's role **in this stokvel** is Treasurer or Admin. Role is read from the `/me/home` entry for the given id, never a token or session-wide role. Members always submit their own id. The backend protects the route with the per-stokvel filter and write policy. Validate the required forced 403 test on the real API.

## Where each piece of state lives

| State | Location | Why |
| --- | --- | --- |
| Authentication | Encrypted HttpOnly session cookie | Browser cannot read tokens |
| Current contribution filters | URL query via nuqs | Shareable and back-button friendly |
| Current page results | Server page 1 / TanStack later pages | SSR first paint and infinite scroll |
| Pinned stokvel ids and names | Zustand client store with persist | Multi-page UI preference |
| Account balance/role/history | API | Canonical, authorization-checked server state |
| Form amount and pending state | Client form / Server Action state | Keep editable values and retries |
| Idempotency key | Server Action state and hidden form field | Same payment intent across unknown outcome |

## Filters

`nuqs` is pinned to 2.10.2. Parsers: `member` = UUID allowlist or empty; `from` / `to` = strictly valid `YYYY-MM-DD` or empty; `sort` = newest/oldest allowlist default newest. Invalid values fall back to defaults. Defaults disappear from the URL with `clearOnDefault`. Navigation uses `history: 'push'` so Back restores prior filter choices. Changes use `shallow: false` to get a new page one from the server.

## Dates

SAST is UTC+2 without DST. One pure function `southAfricanDaysToUtcRange` converts calendar dates to a half-open UTC interval. For `2026-10-01` through `2026-10-31`, this is `2026-09-30T22:00:00.000Z` included to `2026-10-31T22:00:00.000Z` excluded. The parser rejects `2026-02-30`. A from-only or to-only filter stays open-ended; an inverted range drops the end. Display uses `Africa/Johannesburg` timezone. Run and paste both `TZ=UTC npm test` and `TZ=Africa/Johannesburg npm test` outputs before submission; the offline pure smoke test already checked the October boundaries.

## Paging

The first history and activity pages come from the Next server. Later pages use `useInfiniteQuery` via same-origin `/bff` handlers, with `pageSize=10`, opaque `pageToken`, `initialPageParam: null` and `getNextPageParam` returning undefined for empty tokens. History keys include stokvel id, member, from, to and sort, so old pages cannot appear under a new filter. On contribution success `refresh()` updates server page one and a browser event invalidates the history and activity queries. The API fingerprint rejects a token reused with different filters.

## Pinned stokvels

A Zustand vanilla `createStore` is created per provider, not in a global module variable. Persist holds **only id and name**, uses `skipHydration` and manually calls `rehydrate()` after mount. Pins for stokvels no longer in the current `/me/home` membership list are removed. Sign-out clears pins in the client before submitting the server form, and the login/register pages clear the storage after expiry/unexpected sign-out on a shared phone. Verify browser hydration logs and sign-out/sign-in with two separate accounts.

## Known gaps and reviewer evidence

- Cannot run .NET or npm installation/build/audit in the provided archive-processing environment. No passing CI logs or screenshots are claimed. Recheck all scripts and behavior using actual Docker/PostgreSQL before merging.
- This frontend was authored in `web-next/` rather than created by running `create-next-app`; compare generated scaffolding against 16.4.1 when npm is available.
- Next `package-lock.json` is not yet generated; `web/package-lock.json` also needs updating for newly added pinned RHF/Zod/MSW packages.
- `src/api/schema.d.ts` is a copy of the downloaded source snapshot, and must be regenerated from the patched live API.
- For historical activity, the feed combines bounded recent contributions, payouts and joined memberships into a deterministic cursor; test equal timestamps and cursor paging at real database scale.
- Member labels display abbreviated ids because the existing membership contract does not include names. Consider a secure API projection if names are required for the user experience.
- Current API's refresh-reuse protection is strict; process-local Next coalescing alone is insufficient for horizontally scaled production.
- Original submission videos/screenshots, deliberate-failure evidence, exact API contract responses, and timing logs require the developer's running app.

Before the PR, run:

```sh
cd web-next
npm install
npm run api:types
npm run typecheck
npm run lint
TZ=UTC npm test
TZ=Africa/Johannesburg npm test
npm run build
npm audit
```

Please **paste actual results** into PR description, rather than reusing offline smoke output as if it were an npm/Vitest build.

## Assignment 9.2 — tests, performance, accessibility and privacy

**Evidence note:** The following records which code paths are implemented and which require real-browser evidence. Do not record fabricated Lighthouse results or claim an unrun Playwright suite has passed.

### Plan

1. Exact rand-to-cent conversion and South African date boundaries are pure helpers in `src/lib/`. Role rules belong to the API. The Server Action separately validates all inputs.
2. Vitest handles pure rules and the contribution-form events; Playwright runs whole-page/session/permission flows; NVDA, keyboard-only use and Lighthouse must be checked manually.
3. Form integration edges are the imported Server Action, the render state, navigation and API. Tests fake the Server Action, **not** the component's React state. Playwright exercises the real app.
4. API login permits ten attempts per minute per IP by default. E2E should authenticate once per persona, save browser state in `e2e/.auth/` (never commit it), and reuse it across tests. Each CI run needs its own test DB and generated test-only secrets.
5. The landing/sign-in route can be indexable; `/join?code=` and all signed-in routes must be `noindex`. `/bff/` replies must be private and set `X-Robots-Tag: noindex`.
6. On an entry-level phone, the home page's largest content is the first populated stokvel card; the API's personal home query and streamed data determine its render. Measure with Lighthouse before claiming improvement.
7. No drag action is required in the current implementation. Invite code inputs support paste and sign-in supports `autocomplete` with password managers.

### Running the tests

With Node 24 and the API plus a private seeded database running:
```sh
npm ci
npm run typecheck
npm run lint
TZ=UTC npm test
TZ=Africa/Johannesburg npm test
npm run build
E2E_SEED_PASSWORD='<password for seeded demo account>' npx playwright install chromium
E2E_SEED_PASSWORD='<password for seeded demo account>' npx playwright test
npm audit --omit=dev
```
The E2E tests require `API_BASE_URL`, `SESSION_SECRET`, a seeded test database and a production `npm run start` server. `playwright.config.ts` starts that server on port 3000. Never commit the auth state or credentials. On Windows PowerShell, set `$env:E2E_SEED_PASSWORD` rather than using POSIX assignment.

### What's checked where

| Behaviour | Test or manual procedure | Tool |
| --- | --- | --- |
| Whole-cents parser, ambiguous/negative inputs and round trips | `src/lib/money.test.ts` | Vitest |
| SAST half-open date ranges | `src/lib/southAfricanDates.test.ts` | Vitest under both TZ settings |
| Member does not see treasurer picker; failed payment retains amount and Idempotency-Key | `src/app/(app)/stokvels/[id]/ContributionForm.test.tsx` | RTL + Vitest |
| Signed-out BFF returns JSON 401 | `e2e/privacy-and-contributions.spec.ts` | Playwright |
| Signed-in routes noindex and accessible landmark/labels | `e2e/privacy-and-contributions.spec.ts` | Playwright + axe |
| Keyboard close confirmation and return focus | `e2e/privacy-and-contributions.spec.ts` | Playwright, manual NVDA |
| Contribution increases cycle total by exact cents | Needs seeded, open cycle with no prior payment | E2E/manual |
| Private proof upload, invalid file bytes and denied download | API integration + a manual browser scenario | Pending full coverage |

### Accessibility — WCAG 2.2

| Criterion | Flow | Current mitigation | Verification still needed |
| --- | --- | --- | --- |
| 2.4.11 focus not obscured | Contribution history / sticky layouts | The page uses focusable labels and ordinary flow | Test at 320 CSS px and with keyboard |
| 2.5.7 dragging | Payout order | No drag-only interaction exists | Check future reorder UI offers buttons |
| 2.5.8 targets | File downloads and icon actions | Use native buttons/links | Verify minimum target sizes |
| 3.3.7 redundant entry | Join and payment retries | URL carries invite code; failed contribution retains value | Keyboard + screen reader |
| 3.3.8 accessible auth | Sign in | Username/password autocomplete and paste are supported | Test with password manager |
| Error announcement | Field refusals / success | `role=alert`, `role=status`, labelled form fields | Listen with NVDA |

### Performance: before, after, and why

Do not assume passing a build establishes LCP/CLS/INP. Run the Next production build on a throttled mobile Lighthouse profile three times, using one test stokvel containing 40 members and a year of contributions. Record median LCP (goal below 2.5 s), CLS (below 0.1) and an interactive INP timespan (below 200 ms), both before and after any optimization. No measured baseline or improved metric is claimed yet. Never optimize private payment proofs through a shared image cache.

### Metadata and indexing

| Route | Indexable? | Bot-visible metadata |
| --- | --- | --- |
| `/login`, `/register` | Public (at product owner's discretion) | Generic RondiTrack brand |
| `/join?code=...` | No | Generic stokvel invitation without names, balance or amount |
| `/dashboard/*`, `/stokvels/*`, `/activity` | No | No personal data |
| `/bff/*`, proof downloads | No | Auth required; private, no-store; should set `X-Robots-Tag` |

The join page is server-rendered with generic metadata; it does not place a private invite's stokvel name or amount into Open Graph tags. A signed-out preview bot can see a generic invitation only. Verify by requesting the invite URL with `User-Agent: WhatsApp/2` and checking the returned `<head>`.

### CI and known gaps

The `frontend-ci` workflow runs `npm ci`, typecheck/lint, Vitest under both UTC and SAST, builds and production dependency audit. GitHub's .NET backend workflow runs separately. Playwright is configured but **not yet wired into a seeded Compose E2E CI job**. The full suite still needs ten repeated Vitest passes, three repeated Playwright runs, HTML reports, accessibility/keyboard and screen-reader video, and mobile Lighthouse measurements. Red-to-green CI evidence must be collected from actual PR runs, not invented.
