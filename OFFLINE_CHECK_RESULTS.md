# Offline verification and outstanding integration checks

Updated 10 November 2026. Checks were performed on the uploaded `RondiTrack-main.zip` source copy, **not** against a live API instance or GitHub remote.

## Passed offline checks

- 95 TypeScript/TSX files passed TypeScript syntax/transpilation parsing.
- 100,001 integer-cent values (0–100,000) passed input-format and parse roundtrip checks; invalid fractional money is rejected.
- South African October UTC half-open range, nonexistent dates and safe internal redirects passed isolated logic checks.
- Idempotency key reuse for uncertain retries, and replacement for successful/changed intents passed isolated tests.

## Not yet verified

- No .NET SDK is available here, so `dotnet test`/`dotnet build` could not be executed.
- npm registry access failed; updated `web/package-lock.json` cannot yet be generated, and `web-next/package-lock.json` cannot yet be created. `npm ci`, `npm test`, `npm run lint`, `npm run build`, `npm audit` and Playwright/real browser checks were not run.
- No running database/API is available; migrations (if required), API integration contracts and 3-tab refresh behavior were not verified.
- The Next.js refresh coalescer works only inside one Node server process; a multi-instance/distributed deployment may require a server-side shared lock or replay grace to prevent cross-instance rotation races.
- Assignment recording deliverables (screenshots, screen recordings, real response captures) require a running application and your actual users.
- The original ZIP included `old.txt` with a refresh-cookie value plus `cookies.txt`. Both were removed from the updated package and are excluded by the patch. Any exposed live refresh tokens should be revoked.

## Run before opening a PR

```bash
dotnet test RondiTrack.slnx
cd web && npm install && npm run lint && npm run build && npm test && npm audit && cd ..
cd web-next && npm install && npm run lint && npm run build && npm test && npm audit && cd ..
```

Commit the resulting `package-lock.json` files after checking resolved packages and fixing any build/test/audit failures. The next Next.js app additionally needs `web-next/.env.local` with `API_BASE_URL` and a generated 32-byte `SESSION_SECRET`. Do not commit the `.env.local` file.
