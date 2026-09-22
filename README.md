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
