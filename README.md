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