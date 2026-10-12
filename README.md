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

#### ### ## # RondiTrack, Assignment 5.2: Relationships & Query Behavior

> Items marked **[FILL IN]** or **[CONFIRM]** are things I could not verify from the terminal output I was given. Replace them with your own real results before submitting. Everything else is based on the logs and decisions from this project.

---

## 1. StokvelMember's composite key

`StokvelMember` is a real entity, not an implicit many-to-many. A user can belong to many stokvels and a stokvel has many members, but the relationship carries its own data: `Role` and `JoinedAtUtc`. A hidden join table could hold neither, so the relationship needed to be an entity.

**Why a natural composite key `(StokvelId, UserId)` and not a surrogate `Guid Id`:**

- The pair already uniquely identifies a membership. A user is a member of a given stokvel at most once, which is exactly the rule `Stokvel.AddMember` enforces ("no duplicates").
- A surrogate `Id` would sit next to that pair and still need a unique index on `(StokvelId, UserId)` to stop duplicate memberships. That would be two identities for one fact: the surrogate Id nobody asked for and the pair that actually matters.
- With a surrogate, the database would allow two rows for the same user and stokvel unless I remembered to add the unique constraint. With the composite key, a duplicate is impossible by construction.

**The FK question: how does Contribution or Payout reference a specific membership?** [CONFIRM which option you implemented]

Options considered:

1. **Composite foreign key** `(StokvelId, UserId)` on `Contribution`/`Payout` pointing at `StokvelMember`'s composite key. `Contribution` already stores both `StokvelId` and `UserId`, so this needs no new columns and guarantees that a contribution can only be recorded by someone who is actually a member of that stokvel.
2. **Reference User and Stokvel separately** (two independent FKs, as it works today). Simple, but it does not guarantee the user is a member of that stokvel.
3. **Add a surrogate Id to StokvelMember just so others can point at it.** This gives up the key discipline that was the point of the exercise.

Chosen: **[FILL IN: option and reasoning]**. The cost of not choosing option 1 is that membership of the contributor in the stokvel is only enforced in application code, not in the database.

Currently `Contribution.UserId` has an FK to `Users` with `DeleteBehavior.Restrict`.

---

## 2. Replacing generic repository access for StokvelMember

`StokvelMember`'s composite key meant `GetByIdAsync(Guid id)` no longer had a single id to accept. I replaced generic `IRepository<T>` access with a dedicated `IStokvelMemberRepository`, whose methods take both key parts explicitly:

- `GetAsync(stokvelId, userId)` uses `FindAsync([stokvelId, userId])`, with the values in the same order the key was declared
- `ExistsAsync(stokvelId, userId)`
- `GetByStokvelAsync(stokvelId)` (read-only, `AsNoTracking`)
- `AddAsync(member)`
- `RemoveAsync(stokvelId, userId)`

The generic pattern could not stretch to cover it because its contract promises lookup by a single `Guid` id. Forcing a two-part key through that contract would mean either a fake id or an interface that lies about the entity's identity. The new interface's shape reflects the entity's real identity.

Registered in `Program.cs`: `AddScoped<IStokvelMemberRepository, SqlStokvelMemberRepository>()`.

---

## 3. The ALTER migration

Migration: `AddStokvelMemberCompositeKey` (committed).

What I checked on an ALTER that I would not need to check on a CREATE:

- **Is the old primary key dropped and replaced?** Expect `DropPrimaryKey` followed by `AddPrimaryKey` over two columns. **[FILL IN: what you saw]**
- **Is the old synthetic `Id` column dropped?** **[FILL IN: yes/no]**
- **Could a new NOT NULL column (`Role`) fail on existing rows?** A required column with no default would make the migration fail against a table that already has rows. **[FILL IN: whether you had existing rows, and how you handled it: re-seed or default value]**
- **Are the foreign keys correct?** FK from `StokvelMembers` to `Users` on `UserId`, and to `Stokvels` on `StokvelId`. **[FILL IN]**
- **Does EF drop and recreate a column instead of altering it in place?** That would destroy the data in it. **[FILL IN: what you saw]**

Additional check this session: after adding the `Contribution.User` navigation, I generated a test migration. Both `Up` and `Down` were empty (no new columns or FKs), so the navigation is model-only. I removed that migration.

---

## 4. Second relationship wired

**ContributionCycle → Contribution** (one-to-many).

I considered **Stokvel → ContributionCycle** instead. It was a fair alternative, but I picked ContributionCycle → Contribution because the N+1 endpoint (`GET /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions`) needs exactly this relationship to demonstrate and fix the problem realistically. Stokvel → ContributionCycle stays as it was: a `Guid` field, not yet a navigation. That is a stated decision, not an oversight.

In addition, `Contribution.User` was added as a real navigation (`HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(Restrict)`), which `Include` requires.

---

## 5. N+1, measured

Test data: one stokvel (`11111111-...`), one cycle (`22222222-...`), 10 users (`Test User1`–`Test User10`), 10 contributions of 500.00, seeded with SQL in psql. Verified with `SELECT COUNT(*)` = 10.

Queries were counted from the EF Core command log (`Microsoft.EntityFrameworkCore.Database.Command: Information`), one request at a time with a cleared terminal.

| Version | 10 contributions | What the log showed |
|---|---|---|
| **Naive** (no Include, per-row user lookup) | **11 queries** (1 + 10) | 1 list query, then 10 × `SELECT u."Id", u."CreatedAt", u."DateOfBirth", u."Email", u."FirstName", u."LastName" FROM "Users" ... LIMIT 1` |
| **Fix A** (`Include` + `AsNoTracking`) | **1 query** | `INNER JOIN "Users"`, 12 columns: 6 from `Contributions` + 6 from `Users` |
| **Fix B** (`Select` projection) | **1 query** | `SELECT c."Id", c."Amount", u."FirstName" \|\| ' ' \|\| u."LastName" AS "UserName"`, 3 columns returned, 4 source columns read |

**Shipped: Fix B (projection).**

Reasoning:

- The endpoint is a read-only list with a fixed response shape (`Id`, `Amount`, `UserName`). That is what projection is for.
- Include fetched 12 columns per row (including `DateOfBirth`, `Email`, `CreatedAt` and four extra `Contribution` columns) and materialized `Contribution` and `User` entities that were then discarded. Projection fetched 3 columns and materialized no entities, so **9 fewer columns** per row.
- Scale: with 50 members instead of 5, both versions still run 1 query. But Include would pull 12 columns × 50 rows and build 100 entity objects, while projection pulls 3 columns × 50 rows and builds none. The gap grows with row count, and with every column later added to `User`, which Include would also drag along.
- Eager loading would be the better choice if the endpoint needed to hand a full tracked entity graph to code that would modify it. This one does not.

Note: during measurement the Fix B run also printed one Include-shaped query. That was from a leftover request/route using `.Include(c => c.User)`. **[FILL IN: where you found it and what you did]**

---

## 6. Loading strategy, named

No lazy loading anywhere, and no lazy-loading proxies package. That is a design decision, not a missing dependency.

Why I left lazy loading out: it hides queries. The naive endpoint did one extra query per row through a plain property access, and with lazy loading that would be invisible in the code. Each property access would silently become a database round trip, and the N+1 would be even harder to spot. I want every database trip visible in the query that makes it.

| Read path | Strategy | Why |
|---|---|---|
| `GET .../cycles/{cycleId}/contributions` | **Projection** (a `.Select()` that joins implicitly) | Fixed read-only response shape, fewest columns |
| **[FILL IN: another read path, e.g. `GET /api/stokvels/{id}/members`]** | **[Eager (`Include`) or explicit (`Entry(...).Collection(...).LoadAsync()`)]** | **[reason]** |

---

## 7. AsNoTracking audit

Rule: `AsNoTracking()` only where the loaded entity is never mutated and saved afterwards. Methods shared between a pure read and a write path need a split, not a blanket `AsNoTracking()`.

| Method | Used before a mutation? | Decision |
|---|---|---|
| `IUserRepository.GetAllAsync` | **[CONFIRM]** No | `AsNoTracking` |
| `IUserRepository.GetByIdAsync` | **[CONFIRM]** Yes (`UpdateUserAsync`) | Tracked **[FILL IN: or split / `track` parameter if GET also uses it]** |
| `IStokvelRepository.GetAllAsync` | **[CONFIRM]** No | `AsNoTracking` |
| `IStokvelRepository.GetByIdAsync` | **[CONFIRM]** Yes (`Replace`, `AddMemberAsync`) | Tracked **[FILL IN: or split]** |
| `IStokvelMemberRepository.GetByStokvelAsync` | No, pure listing | `AsNoTracking` |
| `IStokvelMemberRepository.GetAsync` | **[CONFIRM callers]** | `FindAsync` (tracked) |
| Contributions-by-cycle projection | No | Not an issue: `.Select()` into a DTO or anonymous type never tracks |

Shared-method trap: **[FILL IN: whether you found one, and how you resolved it (parameter with a default, or two methods)]**

---

## 8. Test suite, before/after

```
BEFORE today's changes:  [FILL IN: e.g. Passed! - Failed: 0, Passed: X, Total: X]
AFTER (first run):       [FILL IN: failures and what they referenced]
AFTER (fixed):           [FILL IN]
```

What it exposed: **[FILL IN: e.g. tests that referenced `Membership.JoinedAt`, or an `.Id` on StokvelMember]**.

If nothing broke, list which tests you checked and confirm they never touched `StokvelMember.Id` or the old generic repository.

---

## 9. Definition of Done, extended

| Endpoint | (5.1 columns) | Relationship = real navigation | N+1 measured and fixed |
|---|---|---|---|
| `POST /api/stokvels/{id}/members` | ... | Yes, StokvelMember composite key | N/A |
| `GET /api/stokvels/{id}/cycles/{id}/contributions` | ... | Yes, ContributionCycle → Contribution, Contribution → User | Yes, 11 queries → 1 (see §5) |
| `POST /api/users` | ... | N/A, no relationship | N/A |

---

## 10. Gaps not closed

- `Stokvel → ContributionCycle` remains a plain `Guid` field (not yet a navigation).
- `Payout` still references its recipient as before. **[CONFIRM: no change needed / what changed]**
- Contribution membership is enforced by **[FILL IN: composite FK / application code]** (see §1).
- During testing, `GET`s for the seeded stokvel `11111111-...` returned `404 stokvel.not_found` from a different endpoint while the stokvel existed in Postgres. **[FILL IN: cause, e.g. repository still in-memory or a missing field, or leave as an open issue]**

### # RondiTrack, Assignment 5.3: Optimizations, Concurrency & Database Defense

## 1. In-memory filtering audit
Command used: `Get-ChildItem -Recurse -Filter *.cs | Select-String "ToListAsync|IEnumerable"`

**[FILL IN: list every hit: file, line, what it does, and whether I moved it into the query or left it, with the reason.]**

The two paged endpoints (members, contributions) build an `IQueryable` and apply `Where`, `OrderBy` and `Take` before `ToListAsync`, so nothing is filtered or paged in C# after materialization.

SQL captured from the EF Core command log for the contributions endpoint:
```sql
-- [FILL IN: paste the real SELECT ... WHERE ... ORDER BY ... LIMIT from the console log]
```
WHERE, ORDER BY and LIMIT appear in the SQL itself, which is the evidence that the work happens in PostgreSQL.

## 2. Pagination contract
| Property | Behaviour |
|---|---|
| Page size | Optional, default 20, maximum 100. A value above 100 is reduced to 100, not rejected. A negative value returns 400. A value of 0 uses the default. |
| Page token | Opaque: base64 of a JSON object holding the last row's sort value, its Id, and a fingerprint of the sort and filter. Clients must treat it as a string. |
| Next page token | `null` exactly when there are no more rows. The query fetches `pageSize + 1` rows; if the extra row exists there is a next page, and the extra row is dropped from the response. |
| Token reuse | A token carries a fingerprint of the sort and filter that issued it. Reusing it with a different sort or filter returns 400. A malformed token also returns 400. |
| Total count | **Not returned.** A `COUNT(*)` over a filtered table is a scan whose cost grows with the table, and a client paging forward doesn't need it. |

**Keyset, not offset.** Suppose a client is on page 1 of contributions and a new contribution is inserted at the top while they browse. With offset paging (`Skip(20)`), every row shifts by one, so page 2 repeats a row the client already saw. With keyset paging the next page means "rows after this exact row", so an insert above it can't shift anything. With five contributions the two approaches behave the same in practice. With ten thousand, offset also gets slower with depth because PostgreSQL still walks and discards the skipped rows, while keyset seeks straight to the position through the index. The token is opaque, so I can change the implementation later without breaking clients.

## 3. Sortable and filterable fields
| Endpoint | Sort allow-list | Filter allow-list |
|---|---|---|
| Contributions | `RecordedAt`, `Amount`, each with `_asc` or `_desc` (default `RecordedAt_desc`) | `userId` |
| Members | `JoinedAtUtc` ascending only | none |

An unknown sort field or a malformed token returns a 400 problem response in the same shape as the rest of the API. Every sort ends with a unique tiebreaker (`Id` for contributions, `UserId` for members) so two rows with equal sort values can't swap order between page requests.

**Refused:** sorting by member name. A contribution stores only a `UserId`, so sorting by name needs a join to Users on every page request for display order only.

## 4. Seeded volume and EXPLAIN ANALYZE
**[FILL IN: either the real result, or the honest status below.]**

Status if not done: the volume seeder (`RondiTrack.SeedVolume`) is committed but did not build. Its own `Npgsql.EntityFrameworkCore.PostgreSQL` package reference pulled in EF Core 10.0.4, while `RondiTrack.Api` uses 10.0.12 (error CS1705). I did not capture the before and after plans and have not invented any numbers.

The paging index is `IX_Contributions_StokvelId_RecordedAt_Id` on `(StokvelId, RecordedAt, Id)`:
- `StokvelId` is first because it is the equality filter.
- `RecordedAt` is second because it is the sort column, so PostgreSQL can read rows already in order, with no Sort node.
- `Id` is third because it is the tiebreaker, so the index resolves ties itself.

Before plan: **[FILL IN]**  After plan: **[FILL IN]**

## 5. EXPLAIN ANALYZE on small development data
**[FILL IN: paste the plan.]** With a handful of rows, PostgreSQL's planner is expected to choose a Seq Scan even with the index present. Reading a few rows from one page is cheaper than consulting an index and then fetching the rows from the table. That is a correct cost decision, not a bug.

## 6. Composite unique constraint
| Constraint | Rule | Where the rule already lives in C# |
|---|---|---|
| `UX_Contributions_Stokvel_User_Cycle` on `(StokvelId, UserId, ContributionCycleId)` | A member contributes once per cycle | `ContributionService.cs` line 72 calls `ExistsAsync` and returns `contribution.already_recorded` |

Check run before applying the migration:
```sql
SELECT "StokvelId", "UserId", "ContributionCycleId", COUNT(*)
FROM "Contributions"
GROUP BY "StokvelId", "UserId", "ContributionCycleId"
HAVING COUNT(*) > 1;
```
Result: **[FILL IN, e.g. "0 rows"]**

Only one qualifying composite rule exists in my C#. I did not add a second constraint, because the brief forbids constraints for rules the code doesn't enforce (see section 13).

## 7. What I checked when reading the migration
Unlike 5.1 and 5.2, this migration contains only `CreateIndex` calls. I checked that there was no `AlterColumn`, `DropColumn` or `RenameColumn`, so it can't destroy data. The risks are different: a plain `CREATE UNIQUE INDEX` blocks writes to the table while it builds, and it fails if duplicates exist, which is why I queried first. On a large production table I would build the index with `CREATE INDEX CONCURRENTLY`, which EF's default migration does not do.

## 8. Why the C# check and the constraint both stay
The C# check gives a specific, friendly error (`contribution.already_recorded`) in the normal case. The constraint makes the rule true in the uncommon case: two requests passing the check at the same instant, a script, or a manual data fix. Deleting the check would turn every ordinary duplicate into a generic database error. Deleting the constraint would leave the rule holding only as long as every writer goes through my C#.

## 9. xmin concurrency protection
| Entity | Protected? | Reason |
|---|---|---|
| Payout | Yes, `uint Version` mapped with `IsRowVersion()` | Edited after creation |
| Contribution | No | Never edited after creation |
| StokvelMember | No | Never edited after creation |
| User | **[DECISION NEEDED]** | Edited through PUT. Either add the same two lines or give a real reason. |
| Stokvel | **[DECISION NEEDED]** | Edited through PUT. Same. |

My first `xmin` migration generated an `AddColumn`, which would fail because `xmin` is a PostgreSQL system column. I removed it, corrected the mapping, and the regenerated migration is **[FILL IN: empty / hand-emptied]**.

## 10. How the token travels
`GET /api/payouts/{id}` returns the token as an `ETag` response header. `PUT /api/payouts/{id}` requires it back in an `If-Match` header, and a missing or invalid header returns 400. I chose the header approach because it is the standard HTTP mechanism, so any client or cache understands it. A stale token produces **409 Conflict** through the central exception handler, which maps `DbUpdateConcurrencyException`. I chose 409 because the API already uses 409 for "valid request, current state conflicts". 412 would also be defensible.

Unique violations (SQLSTATE 23505) are mapped to 409 in the same handler, not in a controller try/catch.

## 11. Tests
| Test | Proves |
|---|---|
| `Database_rejects_duplicate_contribution_even_when_service_check_is_bypassed` | The constraint holds without the service check |
| `Two_contexts_editing_the_same_payout_raise_DbUpdateConcurrencyException` | The raw EF mechanism. Two contexts are required because one context's identity map gives both readers the same object |
| `Stale_If_Match_token_returns_409_problem_response` | The token survives the HTTP round trip |

None use `Thread.Sleep`, `Task.Delay` or real parallelism. The order of operations is fixed in the test, so it is deterministic.

Suite before: **[FILL IN: passed / failed]**  After: **[FILL IN]**

What each failure exposed: **[FILL IN. Typical cases: a test expecting a plain array (the test was wrong, since "returns everything" was never promised); a PUT without `If-Match` (the change is right, update the test); leftover data colliding with the unique index (name it, or say none did).]**

## 12. Definition of Done (new columns)
| Endpoint | Paged in DB | Backed by constraint | Concurrency-protected |
|---|---|---|---|
| GET contributions | Yes | N/A (read) | N/A (read) |
| POST contributions | N/A | Yes | N/A (never edited) |
| GET members | Yes | N/A (read) | N/A (read) |
| GET payout | N/A (single item) | N/A | Yes (returns ETag) |
| PUT payout | N/A | N/A | Yes (xmin + If-Match) |

## 13. Gaps not closed
- One payout per cycle: no C# check exists, so no constraint was added.
- One cycle label per stokvel: no C# check exists, so no constraint was added.
- Unique email is a single-column check-then-act with no database constraint, so two simultaneous requests could both pass.
- `Payout.Amount` has no update method, so the PUT sets it by reflection for demonstration. A proper `AdjustAmount` method is the fix.
- 10,000-row volume and EXPLAIN ANALYZE plans: **[FILL IN: done / not done and why]**.

#### ## Assignment 5.4: Layered projects and real-database tests

### 1. Project structure

```
RondiTrack.slnx
  Domain/                    entities, Result, exceptions, repository interfaces. References nothing.
  Infrastructure/            RondiTrackDbContext, EF migrations, in-memory and SQL repositories, SeedData.
                             References Domain.
  RondiTrack.Api/            controllers, DTOs, validators, services, exception handler, Program.cs.
                             References Domain and Infrastructure.
  RondiTrack.SeedVolume/     manual tool that inserts 12,000 contributions. Never run by the API.
  RondiTrack.Api.Tests/      xUnit tests on a real PostgreSQL started by Testcontainers.
```

Dependency direction: **Api -> Infrastructure -> Domain**. Nothing points the other way.
The Domain project has no `ProjectReference` and no `PackageReference`. Only `Program.cs`
names concrete repository classes (`InMemory*Repository`, `SqlStokvelMemberRepository`);
every other class depends on the interfaces in `RondiTrack.Domain.Repositories`.

The folders are named `Domain` and `Infrastructure`, but the namespaces are
`RondiTrack.Domain` and `RondiTrack.Infrastructure`, so the code keeps one consistent
`RondiTrack.*` naming.

### 2. What moved, and why

| Moved | From -> To | Reason |
|---|---|---|
| Entities, `Result`, `ResultExtensions` | `RondiTrack.Api/Domain` -> `Domain` | The business rules must not know about HTTP or a database |
| `NotFoundException`, `ConflictException`, `UnprocessableException`, `ValidationFailedException`, `IdempotencyKeyConflictException`, `RondiTrackException` | `RondiTrack.Api/Exceptions` -> `Domain/Exceptions` | `ResultExtensions.ThrowIfFailure()` in Domain throws them. Leaving them in the API would have made Domain depend on the API |
| Repository interfaces, `IdempotencyRecord` | `RondiTrack.Api/Data` -> `Domain/Repositories` | The contract belongs to the code that uses it, not to the code that implements it |
| `RondiTrackDbContext`, `InMemory*` repositories, `SqlStokvelMemberRepository`, `SeedData` | `RondiTrack.Api/Data` -> `Infrastructure/Data` | Storage details |
| Migrations | `RondiTrack.Api/Migrations` -> `Infrastructure/Migrations` | Migrations follow the DbContext |

`RondiTrackExceptionHandler` **stayed in the API**: it speaks HTTP (`ProblemDetails`,
status codes) and handles Npgsql/EF exceptions, so it cannot live in Domain.
During the move I had also moved it into `Domain/Exceptions` by accident (I moved the whole
folder). I caught it because it uses ASP.NET and Npgsql, and moved it back.

I also fixed a filename typo while moving: `SqlStokvelMemebrRepository.cs` is now
`SqlStokvelMemberRepository.cs`.

`ResultExtensions.cs` originally had `using RondiTrack.Api.Exceptions;`, which meant Domain
depended on the API. Moving the exceptions into Domain removed that dependency.

### 3. EF Core and the split

- **Migrations assembly.** `RondiTrackDbContext` lives in Infrastructure, and EF uses the
  DbContext's assembly for migrations by default, so `Program.cs` needs no `MigrationsAssembly`.
- **`dotnet ef` needs a startup project.** `Microsoft.EntityFrameworkCore.Design` stays in
  `RondiTrack.Api` (with `PrivateAssets=all`) because the API supplies the configuration.
  Commands:
  ```powershell
  dotnet ef migrations list --no-connect -p Infrastructure -s RondiTrack.Api
  dotnet ef migrations has-pending-model-changes -p Infrastructure -s RondiTrack.Api
  ```
- **Package versions are pinned to match.** `Microsoft.EntityFrameworkCore` and
  `Microsoft.EntityFrameworkCore.Relational` are `10.0.12` in both Infrastructure and Api, next
  to `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3`. Without the explicit pins, a split
  can produce error CS1705 (an assembly built against a newer EF than the one referenced).
- **The model snapshot stores entity names as strings** (for example
  `"RondiTrack.Api.Domain.Stokvel"`). When the namespace changed I rewrote those strings to
  `RondiTrack.Domain.*` in the snapshot and in every `*.Designer.cs`. If one had been missed,
  EF would believe every entity was new and the next migration would drop and recreate the tables.
- **Migration safety check (empty-migration test).** After the split I ran
  `dotnet ef migrations add SplitCheck -p Infrastructure -s RondiTrack.Api`, opened the file,
  and removed it with `migrations remove`.
  **[CONFIRM: write what you saw. It must say that both `Up` and `Down` were empty, and that
  `has-pending-model-changes` reported no changes. If either was not empty, describe what was in it.]**
- **SeedData in a class library.** Because `SeedData` now lives in Infrastructure (a class
  library, which does not get the web SDK's implicit `using` lines), it has an explicit
  `using Microsoft.Extensions.DependencyInjection;` for `GetRequiredService`.

How the namespaces were fixed: one PowerShell script did literal text replacements
(`namespace RondiTrack.Api.Domain;` -> `namespace RondiTrack.Domain;` and so on) on every moved
file, so no file was edited by hand. I then checked three things: no file still mentions
`RondiTrack.Api.Domain|Data|Migrations`; Domain contains no reference to the API,
Infrastructure, EF Core, ASP.NET or Npgsql; and `RondiTrack.Api.Exceptions` appears only in
`Program.cs` and the exception handler.

### 4. Build result

```
dotnet build RondiTrack.slnx
  Domain                 net10.0 succeeded
  Infrastructure         net10.0 succeeded
  RondiTrack.Api         net10.0 succeeded
  RondiTrack.SeedVolume  net10.0 succeeded
  RondiTrack.Api.Tests   net10.0 succeeded
Build succeeded
```

### 5. Repository lifetimes

| Registration | Lifetime | Reason |
|---|---|---|
| `IUserRepository`, `IStokvelRepository`, `IContributionRepository`, `IContributionCycleRepository` (all `InMemory*`) | Singleton | The data lives inside the object; a shorter lifetime would forget everything after each request |
| `IIdempotencyStore` (`InMemoryIdempotencyStore`) | Singleton | Same reason; backed by a `ConcurrentDictionary`, safe across requests |
| `IUserService`, `IMembershipService`, `IContributionService` | Singleton | They depend only on the Singletons above, so there is no captive dependency |
| `RondiTrackDbContext` (`AddDbContext`) | Scoped (default) | Not thread-safe and holds a connection and change tracker; one per request |
| `IStokvelMemberRepository` (`SqlStokvelMemberRepository`) | Scoped | It holds a `RondiTrackDbContext` |
| FluentValidation validators | Scoped (library default) | Stateless, no conflict |
| `RondiTrackExceptionHandler` | Singleton (framework default) | Its only dependency is `ILogger` |

Rule applied: anything that holds or depends on a `DbContext` is **Scoped**; a **Singleton**
must only depend on Singletons. A Singleton that received a Scoped service would keep it
alive for the life of the app (a "captive dependency"), and ASP.NET Core rejects that in
Development.

I audited `Program.cs` against this rule and found **no violation, so no registration changed**.
`MembershipService`, `UserService` and `ContributionService` take only the in-memory
repositories. `IStokvelMemberRepository` is registered but no class asks for it yet.
When `MembershipService` is switched to it, `MembershipService` must become Scoped.

A test (`Every_registered_dependency_resolves_without_lifetime_errors`) resolves every
registered dependency from a scope in the Development environment, so a future captive
dependency fails the test run.

### 6. Tests on a real PostgreSQL (Testcontainers)

Design (implemented in `RondiTrack.Api.Tests`):

- `PostgresApiFactory` extends `WebApplicationFactory<Program>` and implements `IAsyncLifetime`.
  It starts a `postgres:17-alpine` container with `Testcontainers.PostgreSql`, applies the real
  EF migrations with `Database.MigrateAsync()`, and overrides the connection string with
  `builder.UseSetting("ConnectionStrings:RondiTrack", ...)`.
- `UseSetting` is used, not `ConfigureAppConfiguration`, because `Program.cs` reads the
  connection string while it is still building services, before `ConfigureAppConfiguration`
  callbacks run.
- RondiTrack's `Program.cs` does not migrate on startup, so the factory does it.
- One container is shared by every test class through a collection fixture
  (`[Collection("Postgres collection")]`), because starting a container takes seconds.
- **No test uses `UseInMemoryDatabase`.** In-memory EF does not enforce foreign keys or
  composite keys, so it would hide exactly what 5.2 was about.

Tests:

| Test | What it proves |
|---|---|
| `App_uses_the_container_database_and_all_migrations_are_applied` | The app's connection string is the container's, and all six migrations ran |
| `Every_registered_dependency_resolves_without_lifetime_errors` | The lifetime table in section 5 is true |
| `Unknown_payout_returns_404_problem_json_from_the_database` | A real database query, with the RFC 9457 error shape |
| `Registering_a_user_returns_201_and_the_user_can_be_read_back` | A normal request end to end |
| `Duplicate_membership_is_rejected_by_the_composite_key_in_real_PostgreSQL` | The `(StokvelId, UserId)` key from 5.2 is enforced by the database, using two DbContexts so the second knows nothing about the first row |

#### Test results, honestly

**The tests could not run on my machine.** Docker Desktop on this computer reports
`Docker Desktop is unable to start` (the Docker engine does not start, which on Windows
usually means WSL 2 or virtualization is not available). This is the same reason my 5.1
README says I use a native PostgreSQL install instead of Docker.

What I do have:

```
dotnet build RondiTrack.slnx   -> Build succeeded (all 5 projects, including the test project)
dotnet test  RondiTrack.slnx   -> Test summary: total: 5, failed: 5, succeeded: 0
```

All five failures have the same single cause, thrown from the `PostgresApiFactory`
constructor before any test body ran:

```
DotNet.Testcontainers.Builders.DockerUnavailableException:
  Docker API responded with status code='ServiceUnavailable',
  response='Docker Desktop is unable to start'
```

So the failures say nothing about the code under test; they show that Docker is unavailable
here. I have **not** verified that the five tests pass, and I have not run the "development
database switched off" proof. I did not replace the container with an in-memory database to
make the run go green, because that would defeat the purpose of the assignment.

To verify on a machine where Docker works (a classmate's laptop, a CI runner, or GitHub
Codespaces):

```powershell
git clone <repo-url>
cd RondiTrack
docker version           # needs both Client and Server sections
dotnet test RondiTrack.slnx
```

Expected: `Passed! - Failed: 0, Passed: 5`. To prove the tests do not touch the development
database, stop the local PostgreSQL service and run `dotnet test` again; the result must not change.

```
BEFORE the split:                 [CONFIRM: your earlier test summary, or "no tests existed in the repository"]
AFTER, on a Docker-capable host:  [FILL IN if you manage to run it; otherwise delete this line]
```

### 7. Definition of Done (extended for 5.4)

| Item | Status |
|---|---|
| Domain, Infrastructure and Api are separate projects with the dependency direction above | Yes (build succeeded) |
| Domain has no project or package references | Yes |
| Only `Program.cs` names concrete repositories | Yes |
| Migrations moved with the DbContext, snapshot strings rewritten | Yes |
| Empty-migration check after the split | **[CONFIRM]** |
| Repository lifetimes audited and documented | Yes |
| Tests use a real PostgreSQL through Testcontainers | Written; **not run** (Docker unavailable on my machine) |
| Tests proven independent of the development database | **Not done** (same reason) |

### 8. Gaps I chose not to close

- **Tests not run.** See section 6. This is the main gap, and it is an environment problem,
  not a code problem.
- **Two data stores.** Users, stokvels, contributions and cycles are still in-memory Singletons.
  Only `StokvelMember` has a PostgreSQL repository, and nothing calls it yet. This is why a
  stokvel that exists only in memory returns 404 from an endpoint that queries PostgreSQL
  (for example the paged members list), even though a row exists in psql. 5.4 deliberately
  does not change behaviour, so I left it. It needs fixing before authorization work (6.1).
- **`IPayoutService` implementation.** **[CONFIRM: if your `PayoutService` class exists, say so and
  say it is registered Scoped. If not, keep this sentence:]** `IPayoutService` has no
  implementation and is not registered, so `POST /api/stokvels/{id}/cycles/{id}/payout` fails at
  runtime until a Scoped `PayoutService` is added.
- **Contributions controller.** **[CONFIRM: say that `StokvelContributionsController` contains the real
  contributions endpoints again. In the zip I received it contained the members code.]**
- **Secrets.** **[CONFIRM: say that the connection string is now in user-secrets and that the old
  database password was changed. If not, keep this sentence:]** `appsettings.json` still contains a
  database connection string with a password, which must move to user-secrets.
- **Membership is not enforced by a database foreign key.** `Contribution` has a foreign key to
  `Users` but not a composite foreign key `(StokvelId, UserId)` to `StokvelMember`, so the rule that a
  contributor must be a member is enforced in application code only (carried over from 5.2).

  # Assignment 6.1: Authentication & Stokvel-Scoped Authorization

## 1. Tokens: lifetimes, ClockSkew, claims

| Setting | Value | Why |
|---|---|---|
| Access token lifetime | **10 minutes** (`Jwt:AccessTokenMinutes`; startup rejects anything above 15) | Short enough that a stolen token dies quickly, long enough that the frontend is not refreshing every minute. |
| Refresh token lifetime | **7 days**, rotated on every use | A returning user does not retype a password every day, but each token works only once. |
| `ClockSkew` | **30 seconds** | The default is 5 minutes, which would quietly turn a 10-minute token into a 15-minute one. 30 s still absorbs ordinary clock drift between servers. |
| Signing algorithm | HMAC-SHA256 only (`ValidAlgorithms`) | The token's own `alg` header never chooses how it is verified. |

**Claims in the access token, and why each is safe to be up to 10 minutes stale:**

| Claim | Meaning | Why staleness is harmless |
|---|---|---|
| `sub` | the user's id | A user id never changes. |
| `jti` | unique token id | Random; carries no authority. |
| `role` | platform role: `User` or `PlatformAdmin` | Changes rarely. If a platform admin is demoted, the worst case is 10 more minutes of access, and the token cannot be refreshed past that because refresh re-reads the user. |
| `iss`, `aud`, `iat`, `nbf`, `exp` | standard validation fields | Fixed at issue time. |

No email, no name and **no stokvel role** is in the token. I decoded the payload of a real login token and it contains exactly `aud`, `iss`, `exp`, `iat`, `nbf`, `sub`, `jti` and `role`, with `exp` ten minutes after `iat`.

## 2. Refresh cookie flags and frontend advice

| Flag | Value | Reason |
|---|---|---|
| `HttpOnly` | on | JavaScript cannot read the cookie, so an XSS bug cannot steal the refresh token. |
| `Secure` | on | The cookie is only ever sent over HTTPS. (Run the `https` launch profile locally.) |
| `SameSite` | `Strict` | The browser never sends it on a request another site triggers, which blocks CSRF against `/api/auth/refresh`. |
| `Path` | `/api/auth` | Sent only to register/login/refresh/logout, never with ordinary API calls, so it travels far less often. |
| `Expires` | the token's expiry | Matches the server-side expiry. |

Observed on a real login response: `Set-Cookie: refresh_token=<value>; expires=<7 days later>; path=/api/auth; secure; samesite=strict; httponly`.

**Advice to the frontend team:** keep the **access token in memory only** (a JavaScript variable). When the page reloads, call `POST /api/auth/refresh`: the browser sends the cookie by itself and you get a new access token. Never put either token in `localStorage`. Anything in `localStorage` is readable by every script on the page, so one XSS bug (a compromised npm package, an injected ad) lets an attacker copy a 7-day refresh token and use it from their own machine, long after the user closes the tab. With the `HttpOnly` cookie, the same bug can still make requests from the user's browser while the page is open, but it cannot carry the token away. If the access token were in `localStorage` the loss would be smaller (10 minutes), but it is the same mistake, and in-memory costs the frontend only one refresh call on page load.

## 3. Reuse detection

Every login starts a **family** of refresh tokens. Each refresh spends the presented token (`RevokedAt` is set) and issues a successor in the same family. If a token that is already spent or revoked is presented again, the server cannot tell whether the real user's app replayed it or an attacker stole an old copy, so it **revokes every token in the family** (one `UPDATE ... WHERE FamilyId = ... AND RevokedAt IS NULL`) and returns 401.

**What the legitimate user experiences:** their next refresh fails with 401 (`auth.refresh_reused`), the frontend sends them to the sign-in page, and they log in again, which starts a fresh family. The attacker's copy, and the user's own newer token, are both dead. The cost is one extra login; the benefit is that a stolen token can be used at most once before it is noticed.

Two simultaneous refreshes of the same token are guarded by PostgreSQL's `xmin`: only one `UPDATE` can win, the other receives 409.

I checked this by hand (section 9): replaying the old cookie returned 401 `auth.refresh_reused`, and the newer cookie issued by the first refresh was then rejected as well.

## 4. Authorization matrix

Cells are the HTTP status returned. "Treasurer/chair" shows Treasurer first, then Chairperson (stored as role `Admin`) where they differ. Unauthenticated callers get **401** on everything except register, login, refresh, logout and the docs.

| Endpoint | Non-member | Member | Treasurer / Chair | Platform admin |
|---|---|---|---|---|
| `POST /api/auth/register`, `login`, `refresh`, `logout` | anonymous | anonymous | anonymous | anonymous |
| `GET /api/stokvels` (list all) | 403 | 403 | 403 | 200 |
| `POST /api/stokvels` | 201 (becomes chair) | 201 | 201 | 201 |
| `GET /api/stokvels/{id}` | 404 | 200 | 200 | 200 |
| `PUT` / `DELETE /api/stokvels/{id}` | 404 | 403 | 403 / 200 or 204 | 200 / 204 |
| `GET .../members`, `GET .../members/{userId}` | 404 | 200 | 200 | 200 |
| `POST` / `DELETE .../members` | 404 | 403 | 403 / 201 or 204 | 201 / 204 |
| `GET .../cycles`, `GET .../cycles/{id}` | 404 | 200 | 200 | 200 |
| `POST` / `PUT` / `DELETE .../cycles` | 404 | 403 | 201, 200, 204 | 201, 200, 204 |
| `GET .../contributions` | 404 | 200 | 200 | 200 |
| `POST .../contributions` (own) | 404 | 201 | 201 | 201 |
| `POST .../contributions` (someone else's) | 404 | 403 | 201 | 201 |
| `POST .../cycles/{cycleId}/payout` | 404 | 403 | 201 (409 if the business rules refuse) | 201 |
| `GET /api/payouts/{id}` | 404 | 200 | 200 | 200 |
| `PUT /api/payouts/{id}` | 404 | 403 | 200 | 200 |
| `GET/POST/PUT/DELETE /api/users...` | 403 | 403 | 403 | 200 |

The matrix comes from the attributes and handlers in the code and is exercised by `StokvelAuthorizationTests`. By hand I have verified only the unauthenticated 401 (section 9). **[CONFIRM: after running the 404/403/201 checks in section 9, state here that they matched, or correct the cells that did not.]**

## 5. Why 404 for non-members and 403 for members without the role

A **non-member** asking about a stokvel is asking a question they have no right to ask: "does this stokvel exist?" Answering 403 would say "yes, it exists, you just can't touch it", which leaks that a private savings group with that id exists. So the answer is the same 404 (same `type`, `title` and message) that a stokvel that never existed gets, and a test checks they match.

A **member without the role** already knows the stokvel exists, because they belong to it. A 404 would hide nothing and would only confuse them ("the page I was just looking at is gone?"). 403 tells them precisely what is true: you are in, but your role does not allow this.

The same rule is applied to payouts: `GET /api/payouts/{id}` returns `payout.not_found` to a non-member of that payout's stokvel, never a 403.

Login follows the same principle: an unknown email and a wrong password return the identical 401 and take the same time (a dummy hash is verified when the email is unknown). The one place the API still reveals whether an email has an account is registration (409 on a duplicate), because the assignment requires it; rate limiting is listed under gaps.

## 6. Why the per-stokvel role is not in the token

A token is a **snapshot**: whatever it says stays true, as far as the API is concerned, until it expires. A stokvel role is a fact about a person **in one stokvel**, and it changes while the token is alive: if the chairperson removes the treasurer at 10:00, a token issued at 09:58 would still say "Treasurer" until 10:08, and that person could still process a payout at 10:04. Money cannot wait for a token to expire.

It also cannot fit: Lerato is Treasurer of her burial society and a plain Member of her work club, with one login. A single `role` claim cannot say both, and a list of roles per stokvel would be stale in every entry.

So the token says only who you are (and your platform role). Every stokvel request reads the caller's `StokvelMember` row from PostgreSQL in `StokvelRoleHandler`. Cost: one indexed primary-key lookup per request. Benefit: a role change takes effect on the very next request. A test proves it: a treasurer's token creates a cycle (201), the role is changed to Member in the database, and the **same token** gets 403.

## 7. Role-based vs policy-based, using this code

**Role check (used exactly once):** `[Authorize(Roles = PlatformRoles.PlatformAdmin)]` on `GET /api/stokvels` in `StokvelsController`.
- *If the rule changed* ("support staff may list stokvels too"): edit that attribute to name a second role, and every other endpoint that needs the same rule would need the same edit. With one use that is fine; with ten it is ten places and easy to miss one.
- Good for: a single, simple, platform-level rule that will not grow.

**Policy-based (everywhere else):** controllers say `[StokvelAccess(Policies.StokvelTreasurer)]` or `[Authorize(Policy = Policies.PlatformAdmin)]`; the rule lives in `Program.cs` and the handlers.
- *If the rule changed* ("a Treasurer may no longer process payouts, only the chairperson"): change `[StokvelAccess(Policies.StokvelTreasurer)]` to `StokvelChairperson` on one action, or change what a policy means in one place in `Program.cs`, and every endpoint using it follows. No controller contains a role name.
- Also the only option for rules that need data: "Treasurer **of this stokvel**" needs a database lookup per stokvel, which a role string in an attribute cannot express.

## 8. Log excerpt (no secrets)

**dotnet run --project RondiTrack.Api --lauofile https\thato\OneDrive\Desktop\RondiTrack\RondiTrack> 
Using launch settings from RondiTrack.Api\Properties\launchSettings.json...
Building...
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (109ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT "MigrationId", "ProductVersion"
      FROM "__EFMigrationsHistory"
      ORDER BY "MigrationId";
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (61ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT 1
info: Microsoft.EntityFrameworkCore.Database.Command[20101]**