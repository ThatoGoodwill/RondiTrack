# Assignment 11.1 — Real Data: implementation and verification

This is a working implementation branch, not evidence that the assignment has
passed all of its required checks. Mobile source lives in `mobile/` of the same
RondiTrack repository as the API. Original Week 10 fakes remain available for
tests by overriding `homeRepositoryProvider` and `payoutRepositoryProvider`.

## 1. The seven answers from **our own API**

Source: [API commit 805b5d483eaa90b408b1fcdc5740a028b79c6ba6](https://github.com/ThatoGoodwill/RondiTrack/commit/805b5d483eaa90b408b1fcdc5740a028b79c6ba6), including
`RondiTrack.Api/Services/Paging/PageToken.cs`,
`RondiTrack.Api/Contracts/HomeContracts.cs`,
`RondiTrack.Api/Contracts/PayoutOrderResponse.cs`,
`RondiTrack.Api/Auth/AuthService.cs`,
`RondiTrack.Api/Controllers/AuthController.cs`,
`RondiTrack.Api/Exceptions/RondiTrackExceptionHandler.cs`, and
`RondiTrack.Api/Frontend/RateLimiting.cs`.

| Question | Answer proven by the API source |
| --- | --- |
| Pagination | `nextPageToken` is null at the end. Reusing a cursor under a different sort/filter/user fingerprint causes 400 `paging.token_mismatch`, not a fresh first page. Treat null and empty as no further pages but never reuse a token across filters. |
| Stale writes | The payout GET returns the ETag (optimistic concurrency row version). PUT sends it in `If-Match` unchanged. A stale version maps to **409**, not 412. |
| Status spelling | Cycle: `Open`, `PaidOut`, `Closed`. Membership: `Member`, `Treasurer`, `Admin` (chairperson). The ContributionResponse currently **has no status field**; the mobile's `recorded` is presentation inference. Payout-order status: `Paid`, `Next`, `Waiting`. Unrecognised values are mapped to `unknown`. |
| Authentication | Cookie name `refresh_token`, HttpOnly, Secure, SameSite Strict, scoped to auth routes. Access JWT lasts 10 minutes by default; refresh token lasts 7 days. Refresh rotates tokens, and **reuse revokes the whole family** without a reuse grace period. Week 11.1 stores only the access token, in memory. Cookie refresh is for the next assignment. |
| Correlation | Problem+JSON includes `correlationId` and a matching `X-Correlation-Id` header. The previous OpenAPI copy did not declare that extension. The mapper reads the *raw* body and header. |
| Non-members and insufficient role | A non-member on a stokvel-scoped read gets 404 `Not found`, the same as an unknown stokvel. A genuine member attempting a Treasurer-only write gets 403 and remains signed in. |
| Limits | POST login has a fixed IP-based limit; POST contribution and payout use per-user token buckets. A 429 includes `Retry-After` in integer seconds, which we show exactly as returned. |

The source-backed, **selected** contract is
`mobile/contract/openapi/v1.json` (five endpoints the client currently calls).
It was reconciled by inspecting the C# DTOs and controller routes; **it is not yet
a fresh byte-for-byte copy of the live `/openapi/v1.json` endpoint**. Generate
that from a running API and perform an exact comparison before final submission.
The historical `mobile/contract/openapi-v1.json` includes more API endpoints
and must not be treated as an authoritative live snapshot without comparison.

## 2. The client, mappers and data boundary

Chosen alternative: **hand-written Freezed wire DTOs** rather than running
`swagger_parser` over the entire contract. The API has a changed payout endpoint
and a historically stale generated schema; explicitly modelling only the
client-called DTOs makes that discrepancy visible. Source definitions are in
`lib/core/network/data/wire_models.dart`; `build_runner` generates the Freezed
and JSON serialization output as part of CI.

API responses are parsed only under `data/`. These are the translations:

- `HomeStokvelWire` => `HomeEntry` and `Membership`, with role from the *stokvel* record.
- `HomeCycleWire` => `CurrentCycle`: exact string statuses to enums, int cents as `Cents`,
  `endsOn` preserved as SA calendar `YYYY-MM-DD`, and UTC timestamps parsed to UTC.
- `ContributionWire.amount` is decimal **rands**, converted once to integer cents;
  a value with fractional cents is rejected.
- `PayoutOrderWire` => payout-domain model, with the server's entry order and
  `nextMemberId` kept unchanged. No rotation maths or sorting in Flutter.
- `ActivityPageWire`: activity visible to the signed-in member; the first
  page is loaded in 11.1. Pagination is scheduled for Week 11 Day 4.

Network repository constructors are substituted behind the Week 10 interfaces.
No screen imports the Dio client or any wire type. Keep Week 10 fake tests on
`homeRepositoryProvider.overrideWithValue(FakeHomeRepository(...))`.

**Known tradeoff:** Week 10 interfaces return `Future<T>`. The Week 11 adapter
throws a *typed* `Failure`, which Riverpod stores as `AsyncError`. This preserves
all Week 10 widget contracts but does **not** yet implement the assignment's
requested sealed `Result<T>` return signature. A complete Result migration is
outstanding; it should be done as its own interface/refactor commit.

## 3. One network client per flavour

`core/network/client.dart` constructs a single Dio instance from `AppConfig`
and retains it in a provider. Nothing stores a URL in feature code. The
timeouts are connect **5 seconds**, send **10 seconds**, receive **20 seconds**:
short enough to tell the user a disconnected network has failed, while giving
our database time to answer. They are provisional choices. **Database-down
response time has not been measured in this environment** and must be measured
before declaring the timeout decision complete.

The interceptor logs only method, redacted path, status, duration, and
correlation reference. It never logs credentials, headers, cookies, response
bodies, query values, names or amounts. The access token is in memory and
attached as a bearer header to each request.

`tool/setup_android_flavors.py` adds INTERNET permission to main for release
builds, and a network security config to **dev only** permitting cleartext to
`10.0.2.2`. Staging and prod require HTTPS in `AppConfig`. A physical phone
needs its own secure dev-host arrangement, not a production cleartext exception.

## 4. Errors and retry policy

| API / transport | Failure | Person sees |
| --- | --- | --- |
| 400 | `ValidationFailure` | Server validation detail and available field messages |
| 401 | `UnauthorisedFailure` | Please sign in again |
| 403 | `ForbiddenFailure` | You do not have permission to do that (session stays) |
| 404 | `NotFoundFailure` | Not found |
| 409 (or unexpected 412) | `ConflictFailure` | Changed elsewhere; refresh and retry |
| 422 | `RuleFailure` | Rule explanation from API |
| 429 | `RateLimitedFailure` | Wait N seconds, read from Retry-After |
| 500/502/503 | `ServerFailure` | Apology; copyable correlation reference |
| No HTTP response | `OfflineFailure` | Offline or can't reach server, with Retry |
| Unexpected response shape | `UnexpectedFailure` | Couldn't read response |

Only `OfflineFailure` is retried by Riverpod (at most two retries). No 4xx,
500, or write retry is automatic. This avoids multiplying EF Core's server-side
database retries and stops expected 404s spinning.

## 5. Run against our API

On a Windows machine with Flutter, Android SDK and Docker (or in Codespaces):

1. Configure the API's PostgreSQL connection and signing key using
   **private** environment variables; start the API with the repository's
   Docker Compose configuration. Verify `GET /openapi/v1.json` with curl.
2. Run `cd mobile`, then
   `flutter create . --org za.co.thatogoodwill --project-name ronditrack_mobile --platforms android,ios`.
3. Run `python tool/setup_android_flavors.py`, `flutter pub get` and
   `dart run build_runner build`.
4. Start dev flavour with `flutter run --flavor dev --dart-define-from-file=config/dev.json`
   plus **runtime-only** `--dart-define=seedPassword=<secret>`,
   `--dart-define=leratoEmail=<seed email>`,
   `--dart-define=siphoEmail=<seed email>`,
   `--dart-define=nomsaEmail=<seed email>`.
5. Use the dev-only switcher to sign in as seeded members. The login request
   goes to the API. The bearer token is kept in memory and not committed.
   For a physical phone, replace the emulator host with a reachable secure host.

The API's login is rate-limited by IP, so rapidly switching six times is not
an appropriate load test; a 429 is expected when its window is exhausted.

## 6. Tests, CI, and reviewer evidence

Run:

```bash
cd mobile
flutter pub get
dart run build_runner build
python3 tool/check_contract.py
dart format --output=none --set-exit-if-changed lib test
dart analyze --fatal-infos
flutter test
```

- `test/failure_mapper_test.dart`: status-to-Failure, HTML error, offline,
  correlation reference and Retry-After.
- `test/api_repository_test.dart`: home, money, cycle status, non-member 404,
  and server-controlled payout ordering using mocked JSON responses.
- `test/auth_api_test.dart`: exact login payload and token parsing.
- `test/network_logging_test.dart`: PII and bearer token do not appear in logs.
- `test/config_network_test.dart`: HTTPS-only non-dev flavours.
- `test/architecture_test.dart`: feature screens cannot import data implementations.
- `test/live_api_test.dart`: opt-in fresh-seed test, excluded by default.
- `tool/check_contract.py`: two boundaries: selected OpenAPI schemas to
  committed wire model fields, and schema property names to C# response records.
  This detects additions/renames, but is **not a replacement** for generating
  and comparing the full live document from the API.

**Still needed before calling the PR complete:** run and capture live 403,
429 and server-reference screens, validate a fresh OpenAPI output, deliberately
make three tests fail then record their red runs, capture the two red contract
drift runs, measure timeouts with the DB unavailable, run the opted-in live test,
and add the requested mobile screenshots and screen recordings.

This document deliberately does not invent screenshots, response times,
passing tests or API logs.
