# Assignments 7.3–8.3 integration workpack

**Source:** user-provided `RondiTrack-main.zip` (GitHub export dated 2026-10-22). This is an updated repository copy, not a GitHub push or a reviewed pull request.

## Code changes

- Kept `Domain/`, `Infrastructure/`, `RondiTrack.Api.Tests/`, and the original `web/` structure.
- Added `CycleContributionsController` for the new integer-cent contribution contract; protected by the same stokvel-scoped authorization as existing routes.
- Extended `StokvelContributionsController` with member/from/to filters and cursor fingerprints.
- Added a membership-filtered paged `ActivityController`.
- Made money+idempotency receipt atomic in `ContributionService`; only an Open cycle may take contributions.
- Added Vite Assignment 7.4 money helpers, form, payment-intent key logic and six MSW test cases. Updated `authFetch` to reuse a token that another caller already refreshed.
- Added `web-next/` with Next 16.4.1 configuration, encrypted session and refresh proxy, member and treasurer dashboards, stokvel detail route, registration, contribution action, history/nuqs filters, SAST conversion, BFF infinite feeds, and client-only pinned stokvels.

## IMPORTANT verification limit

This work was authored without the .NET 10 SDK and without npm registry access. The offline TypeScript parser/money/date/redirect/idempotency smoke checks passed, but **the entire project has not been compiled, linted, tested, audited, integrated against PostgreSQL or recorded in a browser**. Treat this as a branch-ready implementation candidate, not a proven completed submission. Please follow `web-next/README.md` and `web/README.md` to finish all required evidence.

## Prepare a pull request

If you already have a git clone of <https://github.com/ThatoGoodwill/RondiTrack>, unpack the updated project beside it and copy changed files into the clone, or use the accompanying `RondiTrack-assignments-7.3-to-8.3.patch` with `git apply --check` then `git apply`. Commit on a **new branch**, not main. Avoid adding `.env`, `.env.local`, user secrets or any real member data.

```sh
git checkout -b feature/ronditrack-assignments-7-3-to-8-3
# Apply the accompanying patch from the repository root:
git apply --check ../RondiTrack-assignments-7.3-to-8.3.patch
git apply ../RondiTrack-assignments-7.3-to-8.3.patch
cd web && npm install && npm run api:types && npm run lint && npm test -- --run && npm run build
cd ../web-next && npm install && npm run api:types && npm run typecheck && npm run lint && npm test && npm run build && npm audit
cd .. && dotnet test RondiTrack.slnx
# After correcting failures, verifying runtime, and committing generated lockfiles/types:
git add -A
git commit -m 'Implement RondiTrack assignments 7.3 to 8.3'
git push -u origin feature/ronditrack-assignments-7-3-to-8-3
```

Open the pull request on GitHub, include real command output, manual checks, and links to required recordings. Merge only after CI and the role/privacy/idempotency checks pass. The patch itself does **not** grant ChatGPT write access to your repository.
