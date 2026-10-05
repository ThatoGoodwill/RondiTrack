# RondiTrack API (Assignment 4.1)
Backend foundation for RondiTrack, a stokvel tracker. Built with ASP.NET Core 10.
Users and Stokvels have full CRUD, and stokvels have members. Data is in-memory.
## 1. Controllers vs Minimal APIs
I chose **Controllers**. The brief asks for explicit attribute routing, including a
nested /stokvels/{id}/members route, and controllers make that natural: the route lives on
the class and the method. Controllers give one file per resource (Users, Stokvels,
StokvelMembers) so the structure is easy to navigate, and they are the foundation for the
filters, validation and authorization attributes coming later in the programme.
Minimal APIs would work but are better suited to small services.
## 2. Rules enforced, and why
### User (Domain/User.cs)- First/last name required, trimmed, max 100 - a person with no name is useless in a group.- Email valid and stored lowercase/trimmed - it is how people are identified and contacted.- Email unique (409) - two accounts per person would corrupt payouts.- Must be 18+ - only adults can enter a financial agreement (age of majority in SA is 18).- A user who belongs to a stokvel cannot be deleted (409) - it would orphan memberships.
### Stokvel (Domain/Stokvel.cs)- Contribution > 0, max 2 decimals, capped - money must be real and exact.- Frequency must be Weekly/Fortnightly/Monthly.- Max members 2-50, and it cannot be reduced below the current member count.- A user cannot join twice (409): they would receive the pot twice.- A stokvel cannot exceed its capacity (409): the rotation maths breaks.
### How they are protected
Constructors are private; the only way in is a factory (Create) that validates.
State changes (Update, AddMember) validate before mutating. The member list is private
and exposed as a read-only snapshot. Invalid objects cannot exist.
## Design notes- **Money** is `decimal` (base-10, exact) - `double` cannot represent 0.1 exactly.- **Async** all the way through repositories; CancellationToken is passed down.- **Result type** (hand-written) reports domain failures without exceptions or HTTP.- **Request records** (UserRequest, StokvelRequest, AddMemberRequest) are the shape of the
  incoming JSON only. They hold no logic or validation attributes.- **Status codes:** 200/201/204 success; 400 invalid values; 404 missing; 409 state
  conflicts; 422 body references a non-existent user.- **Known limitation:** check-then-add for email uniqueness is not atomic in memory;
  a database unique index will fix this in Week 5.
## 3. Running the project
Requires the .NET 10 SDK.
    git clone <repo-url>
    cd RondiTrack/RondiTrack.Api
    dotnet run
Then open the URL printed in the console followed by /scalar/v1
(e.g. http://localhost:5080/scalar/v1). The raw OpenAPI document is at /openapi/v1.json.
Data resets on each restart; six users and three stokvels are seeded

## ## Assignment 4.2: Requests, Responses & the Service Layer
### DTOs and mapping
Every endpoint now returns a dedicated response record (UserResponse, StokvelResponse,
MembershipResponse, ContributionResponse), never a domain entity directly. Each has a static
FromEntity method - the one place that decides what that entity looks like on the wire.
StokvelResponse exposes MemberCount rather than the full member list, since a caller usually
just needs the count; the full list is still available via GET /stokvels/{id}/members.
Mapping is hand-written rather than via a library: for a project that touches money, an
explicit, compiler-checked line per field is safer than reflection-based automatic matching
that can silently succeed on a coincidental name match after a rename.
### Service layer
Three services hold real decisions: UserService (duplicate email on create/update; "still a
member" check on delete - both need to see data beyond a single User), MembershipService
(the 4.1 relationship rule, now callable independent of any one controller), and
ContributionService (idempotency + the duplicate-cycle rule). Plain Stokvel CRUD deliberately
has no service - none of its operations need to look beyond one Stokvel's own fields.
### Idempotency
POST /stokvels/{id}/contributions requires an Idempotency-Key header. The service checks a
key BEFORE touching any business data: unseen key -> do the work, then store the key with a
hash of the request and the exact response given. Same key + same body afterwards -> the
stored response is returned unchanged, no new contribution is created. Same key + different
body -> 409, since that's a reused key, not a genuine retry.
### 400 vs 422
A contribution amount of 0 is 400: no state of the world could make it valid, so it's rejected
on its own terms. A userId that doesn't belong to a member of the target stokvel is 422: the
JSON is well-formed and understood, but the thing it refers to doesn't hold up once checked
against the rest of the system.
### RFC 9457 everywhere
Every failure, including every 4.1 endpoint, now returns a full Problem Details body via
ToProblem or NotFoundProblem on ApiControllerBase. No endpoint returns a bare, empty 404
or 400 anymore

## ## Assignment 4.3: Validation & Centralized Error Handling

### Exception hierarchy and reasoning
`RondiTrackException` (abstract, carries a `Code`) is the base. Its children:
- `NotFoundException` -> 404: the resource the URL is about does not exist.
- `ConflictException` -> 409: valid request, but current state forbids it (duplicate member, full stokvel, email taken, contribution already recorded).
- `UnprocessableException` -> 422: well-formed request that points at something missing (unknown user id in the body, user not a member, cycle not found).
- `ValidationFailedException` -> 400: a safety net behind FluentValidation (entity rejections, unreadable/missing bodies).
- `IdempotencyKeyConflictException` extends `ConflictException` -> 409.

**How I classified the idempotency-key conflict:** a duplicate contribution and a reused Idempotency-Key are different kinds of failure. A duplicate contribution is a business-state conflict ("already paid"); a reused key is a protocol misuse (the key belongs to a different request). Both resolve to 409, so I made the idempotency case a *subtype* of `ConflictException`: the handler's single `ConflictException` case maps it to 409 automatically, while the distinction stays visible in code.

### Validation vs exception
- **Validation (FluentValidation, shape only):** blank names, non-positive or over-precise amounts, out-of-range member counts, invalid enum values, empty GUIDs, malformed email. No validator touches a repository.
- **Exception (thrown from the service/controller):** anything that needs to ask a repository first: does the stokvel/user/cycle exist, is the email already taken, is the user already a member, is the stokvel full, has this cycle already been paid, was this key already used with a different body.
- Rule of thumb: validation answers "is this well-formed?"; exceptions answer "is this allowed right now?".

### ContributionCycle: did it need a service?
No. Creating a cycle needs exactly one existence check (does the parent stokvel exist), a single repository lookup with no decision attached. It is not a multi-step, cross-entity judgement call like adding a member or recording a contribution. The controller talks straight to `IContributionCycleRepository`, protected by validation, the entity's own rules and a thrown `NotFoundException`. `Contribution` now references a real `ContributionCycleId`, which also closes the 4.2 gap where "2026-09" and "2026-9" were treated as different cycles.

### Correlation ID walkthrough
Response body (`POST /api/users`, blank first name):
```json
<paste the real response body here, with its correlationId>
```
Matching log line from the terminal:
```
<paste the real log line here, with the same CorrelationId>
```

### Live demonstrations (request + response bodies)
<paste the 400, 404 and 409 request/response bodies from Scalar here>

### Retrofit
Every endpoint from 4.1, 4.2 and 4.3 now throws instead of building a response. `ToProblem` and `NotFoundProblem` were deleted; `RondiTrackExceptionHandler` is the only place a `ProblemDetails` is created. Unknown routes are covered by `UseStatusCodePages` + `AddProblemDetails`, and unreadable bodies by `ValidationFilter`, so no endpoint keeps a bespoke error response.

### Tests
`RondiTrack.Api.Tests/NegativePathTests.cs` uses `WebApplicationFactory<Program>` to prove, over real HTTP, that a malformed request (400), a not-found (404) and business-rule violations (409, 422) each return the right status code and `application/problem+json`.

## Assignment 5.1: EF Core & Database Foundations

### 1. PostgreSQL setup (reproducible from a clean machine)

**Choice: native install on Windows, not Docker.** My machine cannot run Docker, so I installed PostgreSQL directly.

Steps (a teammate with nothing installed can follow these):

1. Download the Windows installer from postgresql.org (EDB installer, version **[FILL IN, e.g. 17]**). Keep port `5432`, set a password for the `postgres` superuser, and tick "Command Line Tools" so `psql` is installed.
2. Add the `bin` folder to PATH for the session:
```powershell
   $env:Path += ";C:\Program Files\PostgreSQL\[VERSION]\bin"
   psql --version
```
3. Create a dedicated login and database for RondiTrack:
```powershell
   psql -U postgres -h localhost
```
```sql
   CREATE ROLE ronditrack_app LOGIN PASSWORD '<choose-a-password>';
   CREATE DATABASE ronditrack OWNER ronditrack_app;
   \q
```
   **[FILL IN: if you used the postgres user instead of a dedicated role, change the above to match what you actually did.]**
4. Prove connectivity independently of the API:
```powershell
   psql -h localhost -U ronditrack_app -d ronditrack -c "select current_database(), version();"
```
   Result: **[FILL IN: paste the output or add a screenshot]**

### 2. Secret management

The connection string is never stored in a tracked file. `appsettings.json` and `appsettings.Development.json` contain no connection string. I use .NET User Secrets, stored in `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`, outside the repo. Only the `UserSecretsId` (a meaningless GUID) is committed in the `.csproj`.

A teammate gets running with:

```powershell
cd RondiTrack.Api
dotnet user-secrets set "ConnectionStrings:RondiTrack" "Host=localhost;Port=5432;Database=ronditrack;Username=<their-user>;Password=<their-password>"
```

**[FILL IN: confirm you checked `git log -p --all -S"Password="` shows nothing.]**

### 3. The mapping problem

When I ran `dotnet ef migrations add InitialCreate`, EF Core refused to create the DbContext:

> No suitable constructor was found for the type 'Contribution'. ... Cannot bind 'stokvelId', 'userId', 'contributionCycleId', 'recordedAt' ...

The same error then appeared for `ContributionCycle` (`stokvelId`, `createdAt`).

**Why:** my entities use private constructors and get-only properties (a design from Assignment 4.1). EF Core only maps properties with a setter by convention, and it can only bind constructor parameters to *mapped* properties. So the properties I hadn't configured weren't mapped and the constructor couldn't be bound.

**Decision:** I configured each property explicitly in `OnModelCreating` (`entity.Property(c => c.StokvelId).IsRequired()` and so on) instead of adding public setters. That keeps the entities' encapsulation unchanged. I accepted a longer `OnModelCreating` as the cost.

**[FILL IN: Stokvel's private `_members` list, if it caused a second problem for you, and what you did about it.]**

### 4. First migration review

**[FILL IN once the migration generates. State what you checked, for example: all six tables are created with the columns I expect; money columns are `numeric(18,2)`; `Users.Email` has a unique index; foreign keys exist where expected; there are no `DropColumn`/`AddColumn` pairs.]**

Why I check for drop-and-add: a migration generator cannot tell a renamed column from a deleted one plus a new one. Applied to real data, that drops the column and loses its contents. On a first migration there is no data, but I read every later migration for this.

### 5. Npgsql retry configuration

Configured: `EnableRetryOnFailure(maxRetryCount: [FILL IN], maxRetryDelay: [FILL IN])`.

**[FILL IN: your reasoning for the numbers.]** Retry: a momentary connection drop or a transient network error. Do not retry: a unique-constraint violation on `Users.Email`, because it will fail the same way every time.

### 6. Repository swapped

**[FILL IN: which repository, why you picked it, and the DI lifetime change from Singleton to Scoped.]**

Why the lifetime had to flip: a `DbContext` is not thread-safe and holds a connection and a change tracker. As a Singleton it would be shared by all concurrent requests, so one request's pending changes would leak into another's and two requests using it at once would throw. Scoped gives one context per request.

### 7. Test suite before and after

BEFORE (in-memory): **[FILL IN: paste the `dotnet test` summary line]**

AFTER (real PostgreSQL): **[FILL IN: paste the summary line, and note anything that went red and what it exposed]**

The suite runs against my local PostgreSQL instance. Testcontainers arrives on Day 4.

### 8. Payout rule and transaction

**[FILL IN: your rotation rule, what the endpoint is, which two writes the explicit transaction protects, and the rollback test result. If not done, say so in section 10.]**

### 9. Definition of Done (extended)

**[FILL IN: your 4.4 table plus the two new columns: "Persisted via EF Core" and "Explicit transaction tested". Use an honest "No" where it applies.]**

### 10. Gaps I chose not to close yet

**[FILL IN honestly. Examples if true: repositories other than the one swapped are still in-memory by design; the Payout endpoint has no database-level unique constraint to stop two concurrent payouts; the migration was blocked by mapping errors and was not applied before submission.]**
