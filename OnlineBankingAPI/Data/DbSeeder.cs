using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Repositories;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Data;

/// <summary>
/// Seeds three demo customers with 180 days of deterministic history. Every entry is posted in time
/// order through the same ledger and transfer code the API uses, so sequences, running balances and
/// account balances are computed, never hand-typed. A final adjusting entry lands each account on its
/// target closing balance, and a verification step fails loudly if the books do not balance.
/// </summary>
public class DbSeeder(
    BankDbContext db,
    IAccountRepository accounts,
    ILedgerRepository ledger,
    TransferService transfers,
    IPasswordHasher<Customer> hasher,
    TimeProvider clock,
    ILogger<DbSeeder> logger)
{
    public const string DemoPassword = "Demo#2026";
    private const int HistoryDays = 180;

    private static readonly string[] Merchants =
    [
        "Bean There Coffee", "Corner Coffee Co", "Daily Grind Coffee", "FreshMart Grocer", "Green Valley Grocer",
        "Harbor Grocer", "QuickFuel", "Sunrise Fuel", "Metro Fuel Stop", "Pixel Books", "Northside Pharmacy",
        "City Cinema", "Noodle House", "Taco Palace", "Pizza Planet", "The Burger Joint", "Urban Outfitters Co",
        "HomeGoods Depot", "TechZone Electronics", "Pet Pantry", "Bloom Florist", "Metro Transit", "RideShare",
        "StreamFlix", "Tunely Music", "Cloud Storage Plus", "Bakery Lane", "Sushi Garden", "Hardware Hub",
        "Fit Foods Market",
    ];

    private record Profile(
        Customer Customer,
        Account Checking,
        Account[] Savings,
        decimal[] SavingsOpening,
        Payee[] Payees,
        decimal OpeningDeposit,
        decimal Salary,
        (int Min, int Max) CardsPerWeek,
        (decimal Min, decimal Max) CardAmount,
        decimal[] MonthlyPayments,
        decimal[] Targets);

    private record SeedEvent(DateTimeOffset At, int Order, Func<Task> Post);

    /// <summary>Applies migrations and seeds an empty database. With <paramref name="reseed"/>, drops it first.</summary>
    public async Task MigrateAndSeedAsync(bool reseed, CancellationToken ct)
    {
        if (reseed)
        {
            logger.LogWarning("--reseed: dropping and recreating the database");
            await db.Database.EnsureDeletedAsync(ct);
        }

        await db.Database.MigrateAsync(ct);
        if (!await db.Customers.AnyAsync(ct)) await SeedAsync(ct);
    }

    /// <summary>Empties every table and seeds again, without dropping the database (for live demos).</summary>
    public async Task ResetAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE idempotency_records, quotes, ledger_entries, transfers, payees, sessions, accounts, customers;
            ALTER SEQUENCE transfer_reference_seq RESTART;
            """, ct);
        db.ChangeTracker.Clear();
        await SeedAsync(ct);
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var random = new Random(20261023);
        var today = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var start = today.AddDays(-HistoryDays);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The bank's payee clearing account comes first.
        db.Accounts.Add(new Account
        {
            Id = SeedIds.PayeeClearingAccount, CustomerId = null, Kind = AccountKinds.Clearing,
            Name = "Payee clearing", AccountNumber = "9000000001",
        });

        var profiles = BuildProfiles(start);
        foreach (var p in profiles)
        {
            p.Customer.PasswordHash = hasher.HashPassword(p.Customer, DemoPassword);
            db.Customers.Add(p.Customer);
            db.Accounts.Add(p.Checking);
            db.Accounts.AddRange(p.Savings);
            db.Payees.AddRange(p.Payees);
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        foreach (var p in profiles)
        {
            var events = BuildEvents(p, start, random, ct);
            foreach (var e in events.OrderBy(e => e.At).ThenBy(e => e.Order))
            {
                await e.Post();
                if (db.ChangeTracker.Entries().Count() >= 200) { await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); }
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            // Land each account on its target closing balance.
            var accounts = new[] { p.Checking }.Concat(p.Savings).ToArray();
            var adjustAt = today.AddHours(-1);
            for (var i = 0; i < accounts.Length; i++)
            {
                var balance = await db.Accounts.Where(a => a.Id == accounts[i].Id).Select(a => a.Balance).SingleAsync(ct);
                var diff = p.Targets[i] - balance;
                if (diff > 0)
                    await ledger.PostEntryAsync(new LedgerPosting(accounts[i].Id, Directions.Credit, diff, adjustAt,
                        "Mobile check deposit", null, Categories.Deposit), ct);
                else if (diff < 0)
                    await ledger.PostEntryAsync(new LedgerPosting(accounts[i].Id, Directions.Debit, -diff, adjustAt,
                        "ATM withdrawal", "Main St ATM", Categories.Card), ct);
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        await VerifyAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Seeded demo data: {Entries} ledger entries",
            await db.LedgerEntries.CountAsync(ct));
    }

    /// <summary>Asserts the ledger invariants. Throws if any account or transfer is inconsistent.</summary>
    public async Task VerifyAsync(CancellationToken ct)
    {
        var balanceMismatches = await db.ScalarAsync<long>("""
            SELECT count(*) FROM accounts a
             WHERE a.kind <> 'clearing'
               AND a.balance <> COALESCE((SELECT e.balance_after FROM ledger_entries e
                                           WHERE e.account_id = a.id ORDER BY e.sequence DESC LIMIT 1), 0)
            """, ct);
        var chainBreaks = await db.ScalarAsync<long>("""
            SELECT count(*) FROM (
              SELECT balance_after,
                     COALESCE(LAG(balance_after) OVER (PARTITION BY account_id ORDER BY sequence), 0)
                       + CASE direction WHEN 'D' THEN -amount ELSE amount END AS expected,
                     sequence,
                     ROW_NUMBER() OVER (PARTITION BY account_id ORDER BY sequence) AS n
                FROM ledger_entries) x
             WHERE x.balance_after <> x.expected OR x.sequence <> x.n
            """, ct);
        var unbalancedTransfers = await db.ScalarAsync<long>("""
            SELECT count(*) FROM transfers t
             WHERE (SELECT count(*) FROM ledger_entries e WHERE e.transfer_id = t.id AND e.direction = 'D' AND e.amount = t.amount) <> 1
                OR (SELECT count(*) FROM ledger_entries e WHERE e.transfer_id = t.id AND e.direction = 'C' AND e.amount = t.amount) <> 1
                OR (SELECT count(*) FROM ledger_entries e WHERE e.transfer_id = t.id) <> 2
            """, ct);

        if (balanceMismatches + chainBreaks + unbalancedTransfers > 0)
            throw new InvalidOperationException(
                $"Seed verification failed: {balanceMismatches} balance mismatches, {chainBreaks} running-balance breaks, " +
                $"{unbalancedTransfers} unbalanced transfers.");
    }

    private List<SeedEvent> BuildEvents(Profile p, DateTimeOffset start, Random random, CancellationToken ct)
    {
        var events = new List<SeedEvent>();
        var order = 0;
        void Add(DateTimeOffset at, Func<Task> post) => events.Add(new SeedEvent(at, order++, post));
        DateTimeOffset At(int day, int fromHour, int toHour) =>
            start.AddDays(day).AddHours(random.Next(fromHour, toHour)).AddMinutes(random.Next(0, 60));
        decimal Amount(decimal min, decimal max) =>
            Math.Round(min + (decimal)random.NextDouble() * (max - min), 2);

        Task Credit(Account a, decimal amount, DateTimeOffset at, string description, string? counterparty,
            string category) =>
            ledger.PostEntryAsync(new LedgerPosting(a.Id, Directions.Credit, amount, at, description, counterparty, category), ct);

        // Opening deposits.
        var openAt = start.AddHours(9);
        Add(openAt, () => Credit(p.Checking, p.OpeningDeposit, openAt, "Opening deposit", null, Categories.Deposit));
        for (var i = 0; i < p.Savings.Length; i++)
        {
            var (account, amount) = (p.Savings[i], p.SavingsOpening[i]);
            if (amount > 0)
                Add(openAt.AddMinutes(30), () => Credit(account, amount, openAt.AddMinutes(30),
                    "Opening deposit", null, Categories.Deposit));
        }

        for (var day = 1; day < HistoryDays; day++)
        {
            var date = start.AddDays(day);

            // Salary on the 1st and 15th; monthly payee payments after it.
            if (date.Day is 1 or 15)
            {
                var at = At(day, 6, 8);
                Add(at, () => Credit(p.Checking, p.Salary, at, "Payroll deposit", "Northwind Traders", Categories.Deposit));
            }

            for (var i = 0; i < p.Payees.Length; i++)
            {
                var payee = p.Payees[i];
                var payDay = 1 + (i * 7 + 2) % 27;
                if (date.Day != payDay) continue;
                var amount = i == 0 ? p.MonthlyPayments[0] : Amount(p.MonthlyPayments[i] * 0.85m, p.MonthlyPayments[i] * 1.15m);
                var at = At(day, 9, 12);
                Add(at, () => Pay(p, payee, amount, at, ct));
            }

            // Monthly interest on savings with a balance.
            if (date.Day == 28)
            {
                foreach (var account in p.Savings)
                {
                    var at = At(day, 1, 3);
                    Add(at, async () =>
                    {
                        var balance = await db.Accounts.Where(a => a.Id == account.Id).Select(a => a.Balance).SingleAsync(ct);
                        var interest = Math.Round(balance * 0.04m / 12m, 2);
                        if (interest > 0)
                            await Credit(account, interest, at, "Interest paid", null, Categories.Interest);
                    });
                }
            }

            // Card purchases, spread through each week.
            if (day % 7 == 1)
            {
                var count = random.Next(p.CardsPerWeek.Min, p.CardsPerWeek.Max + 1);
                for (var c = 0; c < count; c++)
                {
                    var merchant = Merchants[random.Next(Merchants.Length)];
                    var amount = Amount(p.CardAmount.Min, p.CardAmount.Max);
                    var at = At(Math.Min(day + random.Next(0, 7), HistoryDays - 1), 7, 22);
                    Add(at, () => ledger.PostEntryAsync(new LedgerPosting(p.Checking.Id, Directions.Debit, amount, at,
                        $"Card purchase {merchant}", merchant, Categories.Card), ct));
                }
            }
        }

        // Two or three own-account transfers from checking into each savings account that has history.
        for (var i = 0; i < p.Savings.Length; i++)
        {
            if (p.SavingsOpening[i] == 0) continue;
            var savings = p.Savings[i];
            var count = random.Next(2, 4);
            for (var t = 0; t < count; t++)
            {
                var day = random.Next(10, HistoryDays - 5);
                var amount = Math.Round(Amount(50m, 400m) / 10m) * 10m;
                var at = At(day, 18, 22);
                Add(at, () => Move(p, savings, amount, at, ct));
            }
        }

        return events;
    }

    private async Task Pay(Profile p, Payee payee, decimal amount, DateTimeOffset at, CancellationToken ct)
    {
        var locked = await accounts.LockAccountsAsync([p.Checking.Id, SeedIds.PayeeClearingAccount], ct);
        var source = locked.Single(a => a.Id == p.Checking.Id);
        if (source.Balance < amount) return;
        await transfers.PostTransferAsync(p.Customer.Id, source, ResolvedDestination.From(payee),
            SeedIds.PayeeClearingAccount, amount, null, at, ct);
    }

    private async Task Move(Profile p, Account savings, decimal amount, DateTimeOffset at, CancellationToken ct)
    {
        var locked = await accounts.LockAccountsAsync([p.Checking.Id, savings.Id], ct);
        var source = locked.Single(a => a.Id == p.Checking.Id);
        if (source.Balance < amount) return;
        await transfers.PostTransferAsync(p.Customer.Id, source, ResolvedDestination.From(savings),
            savings.Id, amount, "Monthly savings", at, ct);
    }

    private static List<Profile> BuildProfiles(DateTimeOffset createdAt)
    {
        Customer Customer(Guid id, string username, string name) => new()
        {
            Id = id, Username = username, DisplayName = name, Email = $"{username}@example.com", CreatedAt = createdAt,
        };
        Account Account(Guid id, Guid owner, string kind, string name, string number) => new()
        {
            Id = id, CustomerId = owner, Kind = kind, Name = name, AccountNumber = number,
        };
        Payee Payee(Guid id, Guid owner, string nickname, string name, string bank, string number, string category) => new()
        {
            Id = id, CustomerId = owner, Nickname = nickname, Name = name, BankName = bank, AccountNumber = number,
            RoutingNumber = "021000021", Category = category,
        };

        var a = SeedIds.Alice.Customer;
        var b = SeedIds.Ben.Customer;
        var c = SeedIds.Chloe.Customer;

        return
        [
            new Profile(
                Customer(a, "alice", "Alice Morgan"),
                Account(SeedIds.Alice.Checking, a, AccountKinds.Checking, "Everyday Checking", "1004824821"),
                [Account(SeedIds.Alice.Savings, a, AccountKinds.Savings, "High-Yield Savings", "2004829137")],
                [8000m],
                [
                    Payee(SeedIds.Alice.Landlord, a, "Landlord", "Parkview Properties LLC", "First Federal Bank", "551200187731", "rent"),
                    Payee(SeedIds.Alice.CityPower, a, "City Power", "City Power & Light", "Commerce Bank", "880014456", "utilities"),
                    Payee(SeedIds.Alice.Mom, a, "Mom", "Helen Morgan", "Lakeside Credit Union", "30077120945", "family"),
                    Payee(SeedIds.Alice.Gym, a, "Gym", "IronWorks Fitness", "Commerce Bank", "880019902", "health"),
                ],
                OpeningDeposit: 3500m, Salary: 1950m, CardsPerWeek: (9, 13), CardAmount: (3.50m, 85m),
                MonthlyPayments: [1450m, 95m, 150m, 45m],
                Targets: [2650.75m, 10200.00m]),
            new Profile(
                Customer(b, "ben", "Ben Okafor"),
                Account(SeedIds.Ben.Checking, b, AccountKinds.Checking, "Checking", "1007731042"),
                [
                    Account(SeedIds.Ben.Savings, b, AccountKinds.Savings, "Savings", "2007735518"),
                    Account(SeedIds.Ben.HolidaySavings, b, AccountKinds.Savings, "Holiday Savings", "2007739964"),
                ],
                [1200m, 100m],
                [
                    Payee(SeedIds.Ben.PhoneCo, b, "Phone Co", "Horizon Mobile", "National Trust Bank", "77001234598", "utilities"),
                    Payee(SeedIds.Ben.WaterUtility, b, "Water Utility", "Metro Water Services", "Commerce Bank", "880033417", "utilities"),
                    Payee(SeedIds.Ben.Sister, b, "Sister", "Ada Okafor", "Lakeside Credit Union", "30088451207", "family"),
                ],
                OpeningDeposit: 800m, Salary: 520m, CardsPerWeek: (3, 6), CardAmount: (4m, 70m),
                MonthlyPayments: [65m, 40m, 100m],
                Targets: [42.10m, 1500.00m, 300.00m]),
            new Profile(
                Customer(c, "chloe", "Chloe Tan"),
                Account(SeedIds.Chloe.Checking, c, AccountKinds.Checking, "Checking", "1009915560"),
                [Account(SeedIds.Chloe.Savings, c, AccountKinds.Savings, "Savings", "2009918823")],
                [0m],
                [
                    Payee(SeedIds.Chloe.Daycare, c, "Daycare", "Little Sprouts Daycare", "First Federal Bank", "551200990214", "childcare"),
                    Payee(SeedIds.Chloe.CarLoan, c, "Car Loan", "AutoFinance Direct", "National Trust Bank", "77004458120", "loan"),
                ],
                OpeningDeposit: 4000m, Salary: 1150m, CardsPerWeek: (3, 6), CardAmount: (5m, 95m),
                MonthlyPayments: [1200m, 385m],
                Targets: [5010.00m, 0.00m]),
        ];
    }
}
