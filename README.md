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