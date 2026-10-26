using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineBankingAPI.Data;

namespace OnlineBankingAPI.Tests;

[Collection(ApiCollection.Name)]
public class TransferTests(BankApiFactory factory)
{
    [Fact]
    public async Task Quote_then_execute_moves_money_once_and_keeps_the_books_balanced()
    {
        var client = await factory.SignInAsync("alice");
        var checkingBefore = await client.BalanceAsync(SeedIds.Alice.Checking);
        var savingsBefore = await client.BalanceAsync(SeedIds.Alice.Savings);

        var quoteResponse = await client.QuoteAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 200.00m, "Rainy day");
        Assert.Equal(HttpStatusCode.Created, quoteResponse.StatusCode);
        var quote = await quoteResponse.JsonAsync();
        Assert.Equal(checkingBefore - 200m, quote.GetProperty("sourceBalanceAfter").GetDecimal());
        Assert.Equal(checkingBefore, await client.BalanceAsync(SeedIds.Alice.Checking)); // a quote moves nothing

        var response = await client.ExecuteAsync(quote.GetProperty("quoteId").GetGuid(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = await response.JsonAsync();
        Assert.Equal($"/api/v1/transfers/{transfer.GetProperty("id").GetGuid()}", response.Headers.Location!.OriginalString);
        Assert.Equal("completed", transfer.GetProperty("status").GetString());
        Assert.Equal(checkingBefore - 200m, transfer.GetProperty("source").GetProperty("balanceAfter").GetDecimal());
        Assert.Equal(savingsBefore + 200m, transfer.GetProperty("destination").GetProperty("balanceAfter").GetDecimal());

        Assert.Equal(checkingBefore - 200m, await client.BalanceAsync(SeedIds.Alice.Checking));
        Assert.Equal(savingsBefore + 200m, await client.BalanceAsync(SeedIds.Alice.Savings));

        var fetched = await (await client.GetAsync($"/api/v1/transfers/{transfer.GetProperty("id").GetGuid()}")).JsonAsync();
        Assert.Equal(transfer.GetProperty("reference").GetString(), fetched.GetProperty("reference").GetString());

        await VerifyBooksAsync();
    }

    [Fact]
    public async Task Payee_payment_credits_the_clearing_account_and_hides_the_payee_balance()
    {
        var client = await factory.SignInAsync("alice");
        decimal clearingBefore = 0;
        await factory.WithDbAsync(async db => clearingBefore =
            await db.Accounts.Where(a => a.Id == SeedIds.PayeeClearingAccount).Select(a => a.Balance).SingleAsync());

        var quote = await client.QuoteIdAsync(SeedIds.Alice.Checking, "payee", SeedIds.Alice.Gym, 45m);
        var transfer = await (await client.ExecuteAsync(quote, Guid.NewGuid())).JsonAsync();

        Assert.False(transfer.GetProperty("destination").TryGetProperty("balanceAfter", out _));
        Assert.Equal("payee", transfer.GetProperty("destination").GetProperty("kind").GetString());
        await factory.WithDbAsync(async db => Assert.Equal(clearingBefore + 45m,
            await db.Accounts.Where(a => a.Id == SeedIds.PayeeClearingAccount).Select(a => a.Balance).SingleAsync()));
        await VerifyBooksAsync();
    }

    [Fact]
    public async Task Twenty_parallel_requests_with_one_key_create_exactly_one_transfer()
    {
        var client = await factory.SignInAsync("alice");
        var before = await client.BalanceAsync(SeedIds.Alice.Checking);
        var quote = await client.QuoteIdAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 10m);
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.ExecuteAsync(quote, key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        Assert.Equal(19, responses.Count(r => r.Headers.Contains("Idempotent-Replayed")));
        Assert.Equal(before - 10m, await client.BalanceAsync(SeedIds.Alice.Checking));

        var transferId = (await responses[0].JsonAsync()).GetProperty("id").GetGuid();
        await factory.WithDbAsync(async db =>
            Assert.Equal(2, await db.LedgerEntries.CountAsync(e => e.TransferId == transferId)));
    }

    [Fact]
    public async Task Same_quote_with_two_keys_creates_one_transfer_and_one_409()
    {
        var client = await factory.SignInAsync("alice");
        var quote = await client.QuoteIdAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 5m);

        var first = await client.ExecuteAsync(quote, Guid.NewGuid());
        var second = await client.ExecuteAsync(quote, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.JsonAsync();
        Assert.Equal("quote_already_used", problem.GetProperty("code").GetString());
        Assert.Equal((await first.JsonAsync()).GetProperty("id").GetGuid(), problem.GetProperty("transferId").GetGuid());
    }

    [Fact]
    public async Task Same_key_with_a_different_body_is_rejected()
    {
        var client = await factory.SignInAsync("alice");
        var key = Guid.NewGuid();
        var quoteA = await client.QuoteIdAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 1m);
        var quoteB = await client.QuoteIdAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 2m);

        Assert.Equal(HttpStatusCode.Created, (await client.ExecuteAsync(quoteA, key)).StatusCode);
        var reused = await client.ExecuteAsync(quoteB, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("idempotency_key_reused", await reused.CodeAsync());
    }

    [Fact]
    public async Task Missing_or_malformed_idempotency_key_is_400()
    {
        var client = await factory.SignInAsync("alice");
        var response = await client.PostAsync("/api/v1/transfers",
            System.Net.Http.Json.JsonContent.Create(new { quoteId = Guid.NewGuid() }));
        Assert.Equal("idempotency_key_required", await response.CodeAsync());
    }

    [Fact]
    public async Task Expired_quote_is_rejected()
    {
        var client = await factory.SignInAsync("alice");
        var quote = await client.QuoteIdAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 3m);
        factory.Clock.Advance(TimeSpan.FromMinutes(6));
        try
        {
            var response = await client.ExecuteAsync(quote, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("quote_expired", await response.CodeAsync());
        }
        finally
        {
            factory.Clock.Reset();
        }
    }

    [Theory]
    [InlineData(null, "amount_not_positive")]
    [InlineData("0", "amount_not_positive")]
    [InlineData("-5", "amount_not_positive")]
    [InlineData("1.005", "amount_invalid_precision")]
    [InlineData("1000000000", "insufficient_funds")] // no upper limit; a huge amount fails on funds
    public async Task Amount_rules(string? amount, string code)
    {
        var client = await factory.SignInAsync("alice");
        var response = await client.QuoteAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings,
            amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await response.CodeAsync());
    }

    [Fact]
    public async Task Destination_same_account_funds_note_and_status_rules()
    {
        var alice = await factory.SignInAsync("alice");
        Assert.Equal("destination_not_found",
            await (await alice.QuoteAsync(SeedIds.Alice.Checking, "account", Guid.NewGuid(), 1m)).CodeAsync());
        Assert.Equal("same_account",
            await (await alice.QuoteAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Checking, 1m)).CodeAsync());
        Assert.Equal("note_invalid",
            await (await alice.QuoteAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 1m, new string('x', 141))).CodeAsync());
        Assert.Equal("note_invalid",
            await (await alice.QuoteAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 1m, "tab\there")).CodeAsync());

        var ben = await factory.SignInAsync("ben");
        var poor = await ben.QuoteAsync(SeedIds.Ben.Checking, "payee", SeedIds.Ben.PhoneCo, 100m);
        Assert.Equal("insufficient_funds", await poor.CodeAsync());

        var several = await (await alice.QuoteAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Checking, 1.001m, "\u0001")).JsonAsync();
        Assert.Equal("validation_failed", several.GetProperty("code").GetString());
        Assert.Equal(3, several.GetProperty("errors").GetArrayLength());

        await factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == SeedIds.Ben.HolidaySavings)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.Status, "frozen")));
        try
        {
            var frozen = await ben.QuoteAsync(SeedIds.Ben.Savings, "account", SeedIds.Ben.HolidaySavings, 1m);
            Assert.Equal("account_not_active", await frozen.CodeAsync());
        }
        finally
        {
            await factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == SeedIds.Ben.HolidaySavings)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.Status, "active")));
        }
    }

    [Fact]
    public async Task Funds_are_rechecked_at_execution()
    {
        var ben = await factory.SignInAsync("ben");
        var first = await ben.QuoteIdAsync(SeedIds.Ben.Savings, "account", SeedIds.Ben.Checking, 1000m);
        var second = await ben.QuoteIdAsync(SeedIds.Ben.Savings, "account", SeedIds.Ben.HolidaySavings, 1000m);

        Assert.Equal(HttpStatusCode.Created, (await ben.ExecuteAsync(first, Guid.NewGuid())).StatusCode);
        var response = await ben.ExecuteAsync(second, Guid.NewGuid());

        Assert.Equal("insufficient_funds", await response.CodeAsync());

        // Put the money back for other tests.
        var back = await ben.QuoteIdAsync(SeedIds.Ben.Checking, "account", SeedIds.Ben.Savings, 1000m);
        Assert.Equal(HttpStatusCode.Created, (await ben.ExecuteAsync(back, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Transfers_list_pages_newest_first()
    {
        var client = await factory.SignInAsync("alice");
        var first = await (await client.GetAsync("/api/v1/transfers?pageSize=3")).JsonAsync();
        var cursor = first.GetProperty("page").GetProperty("nextCursor").GetString();
        var second = await (await client.GetAsync($"/api/v1/transfers?pageSize=3&cursor={cursor}")).JsonAsync();

        var dates = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray())
            .Select(t => t.GetProperty("createdAt").GetDateTimeOffset()).ToList();
        Assert.Equal(6, dates.Count);
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    private Task VerifyBooksAsync() => factory.WithDbAsync(async db =>
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().VerifyAsync(CancellationToken.None);
    });
}
