# OnlineBankingApp
Online Banking Application for Coding Assignment.

# 01 - How to Run the App and the Tests

## OnlineBankingAPI
1. Start PostgreSQL 17: `docker compose up -d` (or point `ConnectionStrings:BankDb` in
   `appsettings.json` at your own server).
2. Run the `https` profile in Visual Studio, or `dotnet run --launch-profile https`.
   In Development (and the `Demo` environment) the app applies migrations and seeds an empty
   database at startup.
3. Use `OnlineBankingAPI.http` for a walkthrough, or Scalar in the browser.

Start over with fresh data: the `https (reseed)` profile, or `dotnet run -- --reseed`, drops and
recreates the database. In the `Demo` environment `POST /api/v1/demo/reset` does the same live.

## OnlineBankingAPI - SwaggerHub

https://app.swaggerhub.com/apis/kevrobertsorganizati/OnlineBankingAPI/1.0

## OnlineBankingAPI - Tests

`OnlineBankingAPI.Tests` (xUnit) runs the API in memory against a throwaway database on the local
PostgreSQL server (set `OBK_TEST_DB` to another server connection string, without `Database=`).
They cover data isolation, logout, 20 parallel requests with one Idempotency-Key, a quote reused
with a second key, every transfer rule, paging while a transfer posts, and ledger integrity. `ArchitectureTests` fail the build if a controller depends on
`BankDbContext` or a repository, or if a service or repository exposes `IQueryable`.

## OnlineBankingWeb Prerequisites

- Node.js 24 LTS (20.9 or later works), and npm run 10: `corepack enable` picks up the version in `package.json`.
- The OnlineBankingAPI running on its **https** profile (https://localhost:7294) with seeded data.
- The ASP.NET Core development certificate trusted: `dotnet dev-certs https --trust` (Visual Studio does this).

## OnlineBankingWeb - Run it

```powershell
npm install
npm run dev          # https://localhost:3000
```

- Sign in as `alice`, `ben` or `chloe` with the password `Demo#2026`.
- The AI Banking Assistant feature is included.

## OnlineBankingWeb - End-to-end tests

Start the API, then `npm run build` and `npm run test:e2e` (it starts `npm start` itself, or reuses a server on
port 3000). The first time, install the browser with `npx playwright install chromium`.

- The tests move real money between alice's accounts and check changes relative to the starting balances.
  If the API runs in the `Demo` environment, the suite resets the seeded data first.
- The suite signs in eight times (the assistant tests use their own session, because the API also limits
  requests per session). The API allows 10 sign-ins per minute per IP, so wait a minute between
  back-to-back runs.

# 02 - Key Design Decisions

## Microservice or API Architecture?

I went with an API architecture to centralize the functionality into an API as opposed to a collection of microservices.  This reduced complexity and development time which facilitated the timeline for submitting the prototype for the Coding Assignment.

## React Toolchain

I provided a short list of essential elements for the React toolchain like "React 19, Next.js, Redux and NPM" then let Claude Code complete the toolchain.

## Open Questions - OnlineBankingAPI
- Bank time zone for date filters: assumed US Eastern.
- Payee management: read-only now; add create and delete later?

## Open Questions - OnlineBankingWeb
- Login rate limit behind the proxy. The API limits sign-ins to 10 per minute per client IP, and through the proxy every browser shares the Next.js server's IP. That's fine for a local demo; a shared deployment needs forwarded-headers support in the API.

Recommended answer for Production: Use layers rather than one number, because a single per-IP limit is either too tight for shared IPs or too loose for attackers.  See OnlineBankingApp_Web_Front-End_Design.pdf for details.

# 03 - How you used AI Tools

I provided the details of the Coding Assignment as requirements to Claude Code.  I used Claude to create a design for each major application element, separated into Online Banking API (including database), Online Banking Web, and AI Banking Assistant.  I reviewed each design, asked questions, made adjustments before directing Claude to implement the design.  Each design is provided as a PDF under OnlineBankingApp\Documents.  Please review them.

Once the changes were implemented, I reviewed the code changes, built the app, ran the automated tests, then launched the app, looking for issues.  I was impressed with the quality of the deliverables produced by Claude.  Seeing the OnlineBankingApp function cleanly and successfully in the web browser the first time being launched after spending less than a day on design and implementation was really quite remarkable.

There was one case where a Transfer Limit feature of $25,000 appeared in the OnlineBankingAPI.  I scanned the Coding Assignment looking to see where this requirement came from, but found nothing.  I asked Claude where it came from, and he said he added it because it's something that banks do in the real-world.  Since there was no requirement for it, I asked Claude to remove all aspects of it from the REST API.
