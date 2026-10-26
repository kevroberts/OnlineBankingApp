# OnlineBankingAPI

ASP.NET Core Web API (.NET 10) for the online banking prototype: sign-in, accounts, transaction
history, payees and transfers, backed by PostgreSQL 17. The contract is `openapi.yaml` from the
design; the running app serves its own document at `/openapi/v1.json` and Scalar UI at `/scalar/v1`
(Development only).

## Run it

1. Start PostgreSQL 17: `docker compose up -d` (or point `ConnectionStrings:BankDb` in
   `appsettings.json` at your own server).
2. Run the `https` profile in Visual Studio, or `dotnet run --launch-profile https`.
   In Development (and the `Demo` environment) the app applies migrations and seeds an empty
   database at startup.
3. Use `OnlineBankingAPI.http` for a walkthrough, or Scalar in the browser.

Start over with fresh data: the `https (reseed)` profile, or `dotnet run -- --reseed`, drops and
recreates the database. In the `Demo` environment `POST /api/v1/demo/reset` does the same live.

## Demo users (demo only)

All share the password `Demo#2026`.

| Username | Accounts (closing balance) | Payees |
| --- | --- | --- |
| alice | Everyday Checking (2,650.75), High-Yield Savings (10,200.00) | Landlord, City Power, Mom, Gym |
| ben | Checking (42.10), Savings (1,500.00), Holiday Savings (300.00) | Phone Co, Water Utility, Sister |
| chloe | Checking (5,010.00), Savings (0.00, no history) | Daycare, Car Loan |

History covers the 180 days before the seed date. Ids for customers, accounts and payees are fixed
(`Data/SeedIds.cs`).

## How it is built

Three layers, each calling only the one below (see Layered architecture in the design doc):

- `Controllers/` HTTP only: binding, the customer id from the session, status codes and headers
  (`Location`, `ETag`, `Idempotent-Replayed`, the session cookie). They depend on services only.
- `Services/` business rules. `AuthService` (sign-in, lockout), `SessionService` (opaque 256-bit
  token stored as a SHA-256 hash; cookie `obk_session` or `Authorization: Bearer`; 15-minute sliding
  idle timeout, 8-hour cap), `AccountService` (accounts, history paging and filters), `PayeeService`,
  `TransferValidator`, and `TransferService`, which owns the transfer transaction: it claims the
  Idempotency-Key, locks the quote and both accounts (`FOR UPDATE`, ascending id), posts the debit and
  credit, marks the quote used and stores the response, then saves and commits once. Services use
  `BankDbContext` only for transactions and `SaveChangesAsync`.
- `Repositories/` all EF Core LINQ and raw SQL, behind intent-named interfaces (`IAccountRepository`,
  `ILedgerRepository`, `IPayeeRepository`, `ITransferRepository`, `ICustomerRepository`,
  `ISessionRepository`). Every customer-owned lookup takes the customer id, so someone else's id
  returns 404. They never return `IQueryable` and never call `SaveChangesAsync`.
  `ILedgerRepository.PostEntryAsync` is the only code that changes balances: one guarded
  `UPDATE accounts` that returns the new balance and sequence, plus an append-only ledger entry.
- `Data/` `BankDbContext` (the unit of work), EF Core with Npgsql and snake_case naming, migrations in
  `Data/Migrations`, and `DbSeeder`, which posts the history through the same ledger and transfer code
  and then verifies the books.
- `Infrastructure/BankServiceCollectionExtensions.AddBankServices()` registers everything as scoped.

## AI banking assistant

`POST /api/v1/assistant/messages` answers a signed-in customer's questions about their own accounts
("What is my checking balance?", "How much did I spend on groceries this month?", "Show my last five
transactions.") and streams the reply as server-sent events: `conversation`, then `status` and `text`,
then `done` or `error`. `DELETE /api/v1/assistant/conversations/{id}` starts a new chat.

It is read-only by construction. The model gets two tools, `list_accounts` and `search_transactions`
(`Assistant/Tools/`), which call `AccountService` with the customer id from the session; nothing under
`Assistant/` can reach transfers, payees, sessions, repositories or `BankDbContext`, and an architecture
test enforces that. `AssistantService` runs the tool loop and keeps each chat in memory per session
(30 minutes idle, dropped at sign-out).

To use Claude, set your Anthropic API key once with user secrets (it is never stored in the repo):

```
dotnet user-secrets set ANTHROPIC_API_KEY <your key>
```

Without a key the API logs a warning and uses `MockAssistantModel`, which recognizes the three kinds of
question above and answers them from the same tools and real data. Settings are under `Bank:Assistant`
in `appsettings.json`: `Provider` (`Claude` or `Mock`), `Model` (`claude-opus-5-5`), `Effort` (`low`),
`MaxTokens`, `MaxToolRounds` and `ConversationIdleMinutes`.

Add a migration: `dotnet tool restore` then `dotnet ef migrations add <Name> --output-dir Data/Migrations`.

## Tests

`OnlineBankingAPI.Tests` (xUnit) runs the API in memory against a throwaway database on the local
PostgreSQL server (set `OBK_TEST_DB` to another server connection string, without `Database=`).
They cover data isolation, logout, 20 parallel requests with one Idempotency-Key, a quote reused
with a second key, every transfer rule, paging while a transfer posts, and ledger integrity. `ArchitectureTests` fail the build if a controller depends on
`BankDbContext` or a repository, if a service or repository exposes `IQueryable`, or if anything under
`Assistant/` depends on a service or class that can write. `AssistantTests` run the mock model against
the seeded data and a scripted model for the tool loop; `ClaudeAssistantModelTests` check the Claude
request and streamed reply against a stub server. No test calls the real model.

## Open questions (placeholders in `appsettings.json` → `Bank`)

- Date filters use `America/New_York`.
- Payees are read-only.
- The design's INSERT/SELECT-only database role for the ledger is not set up; the app connects as
  the configured user.
