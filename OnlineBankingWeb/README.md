# OnlineBankingWeb

The web front end for the online banking prototype: sign in, accounts overview, transaction history and
transfers, built with Next.js 16 (App Router, Turbopack), React 19 and TypeScript over the OnlineBankingAPI.
The browser only talks to this app's origin; Next.js forwards `/api/*` to the API, so the API needs no CORS
policy and its `HttpOnly` session cookie works as is.

## Prerequisites

- Node.js 24 LTS (20.9 or later works), and npm run 10: `corepack enable` picks up the version in `package.json`.
- The OnlineBankingAPI running on its **https** profile (https://localhost:7294) with seeded data.
- The ASP.NET Core development certificate trusted: `dotnet dev-certs https --trust` (Visual Studio does this).

## Run it

```powershell
npm install
npm run dev          # https://localhost:3000
```

Sign in as `alice`, `ben` or `chloe` with the password `Demo#2026`.

`npm run dev` runs `next dev --experimental-https`, so the API's `Secure` cookie is accepted in every browser.
The first run creates a local certificate for localhost and may ask you to trust it.

The dev and start scripts run Node with `--use-system-ca`, so the server trusts the ASP.NET Core
development certificate from your system store when it proxies to https://localhost:7294. If your Node
version does not support that flag, or the proxy reports a certificate error, export the certificate and
point Node at it in the shell that starts the app (Node reads this variable at startup, so it cannot go in
a `.env` file):

```powershell
dotnet dev-certs https --export-path ./certs/aspnet-dev.pem --format Pem --no-password
$env:NODE_EXTRA_CA_CERTS = "$PWD\certs\aspnet-dev.pem"
npm run dev
```

To point the proxy somewhere else, copy `.env.example` to `.env.development.local` and set `API_ORIGIN`.

### AI banking assistant

The **Ask** button in the header opens a chat panel where a signed-in customer asks about their own accounts
in plain language, such as "What is my checking balance?", "How much did I spend on groceries this month?"
or "Show my last five transactions." It is read-only: the API gives the model two read-only tools and
nothing that can move money or change data.

The model is called by the API, never by the browser. With no Anthropic API key set on the API, it gives
built-in demo answers from the customer's real data, and the panel shows a **Demo answers** label. To use
Claude, set the key on the API (see the API's README):

```
dotnet user-secrets set ANTHROPIC_API_KEY <your key>
```

### Offline demo mode

Set `NEXT_PUBLIC_API_MOCKING=enabled` (in `.env.development.local`) and the same MSW mocks that drive the
component tests answer `/api/*` in the browser, so the app runs without the API. Sign in as `alice`. Mock
data resets on every full page load. The mock assistant answers balance questions only.

## Scripts

| Script                                    | What it does                                                                           |
| ----------------------------------------- | -------------------------------------------------------------------------------------- |
| `npm run dev`                             | Dev server with HTTPS at https://localhost:3000                                        |
| `npm run build` / `npm start`             | Production build, and serve it at http://localhost:3000                                |
| `npm run lint` / `npm run typecheck`      | ESLint (Next.js, React Hooks, jsx-a11y rules) and `tsc --noEmit`                       |
| `npm run format` / `npm run format:check` | Prettier with sorted Tailwind classes                                                  |
| `npm test`                                | Vitest unit and component tests (jsdom, MSW)                                           |
| `npm run test:e2e`                        | Playwright against the production build and the real API, with axe accessibility scans |
| `npm run api:codegen`                     | Regenerates `src/api/bankApi.generated.ts` from the running API's `/openapi/v1.json`   |

### End-to-end tests

Start the API, then `npm run build` and `npm run test:e2e` (it starts `npm start` itself, or reuses a server on
port 3000). The first time, install the browser with `npx playwright install chromium`.

- The tests move real money between alice's accounts and check changes relative to the starting balances.
  If the API runs in the `Demo` environment, the suite resets the seeded data first.
- The suite signs in eight times (the assistant tests use their own session, because the API also limits
  requests per session). The API allows 10 sign-ins per minute per IP, so wait a minute between
  back-to-back runs.

## How it is built

- `app/` holds the routes. Everything except `/login` sits in the `(bank)` route group, whose layout holds
  the header and `SessionGate`.
- `proxy.ts` (Next.js 16's middleware) sends requests without an `obk_session` cookie to
  `/login?next=…` before a protected page renders. It cannot tell whether a session is still alive, so the
  API stays the real gate: any `401` dispatches `sessionEnded()`, a listener clears cached data and the
  transfer draft and calls `POST /api/v1/auth/logout` to clear the stale cookie, and `SessionGate` redirects.
- `src/api/`: RTK Query. `bankApi.generated.ts` comes from `npm run api:codegen` (committed; do not edit),
  `baseApi.ts` holds `bankBaseQuery` (Problem Details to a typed `ApiProblem`, the 401 rule, and retries with
  the same arguments for transfers), and `bankApi.ts` adds cache tags and the infinite queries for history.
- `src/store/`: one Redux store per tab (`StoreProvider`), the `session`, `transferDraft` and `assistant`
  slices, and the listeners. The transfer draft, including its `Idempotency-Key`, is kept in `sessionStorage`
  (`obk.transfer`), so a refresh on the confirmation step replays instead of moving money twice.
- `src/features/`: `auth`, `accounts`, `history` (filters in the URL through nuqs), `transfers`
  (form, confirmation, result, recent transfers) and `assistant`.
- `src/features/assistant/`: `POST /api/v1/assistant/messages` answers with Server-Sent Events, which RTK
  Query does not read, so `streamAssistant.ts` uses `fetch` with a small SSE parser and dispatches each
  event to the `assistant` slice (the same same-origin cookie, and a `401` still dispatches `sessionEnded()`).
  The chat lives only in memory: it is not persisted and is cleared on sign-out, and **New chat** tells the
  API to forget it. Answers use a small Markdown subset (paragraphs, lists, bold) rendered as React text, so
  nothing the model writes is ever inserted as HTML. Screen readers hear each finished answer once through a
  polite live region, not every streamed word.
- `src/components/ui/`: shadcn/ui-style components on Radix UI, Tailwind CSS 4.
- `tests/`: `mocks/` (MSW handlers over a small in-memory bank), `renderWithStore.tsx`, unit tests, and
  `e2e/` for Playwright.
