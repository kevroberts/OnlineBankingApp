using System.Net;
using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Data;

namespace OnlineBankingAPI.Tests;

[Collection(ApiCollection.Name)]
public class AccountTests(BankApiFactory factory)
{
    [Fact]
    public async Task Accounts_list_checking_first_with_total()
    {
        var client = await factory.SignInAsync("ben");
        var body = await (await client.GetAsync("/api/v1/accounts")).JsonAsync();

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Checking", "Holiday Savings", "Savings"], items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal(items.Sum(i => i.GetProperty("balance").GetDecimal()), body.GetProperty("totalBalance").GetDecimal());
        Assert.Equal("****1042", items[0].GetProperty("maskedNumber").GetString());
    }

    [Fact]
    public async Task Another_customers_resources_return_404()
    {
        var alice = await factory.SignInAsync("alice");
        var benEntry = Guid.Empty;
        await factory.WithDbAsync(async db => benEntry =
            (await db.LedgerEntries.FirstAsync(e => e.AccountId == SeedIds.Ben.Checking)).Id);

        string[] urls =
        [
            $"/api/v1/accounts/{SeedIds.Ben.Checking}",
            $"/api/v1/accounts/{SeedIds.Ben.Checking}/transactions",
            $"/api/v1/accounts/{SeedIds.Ben.Checking}/transactions/{benEntry}",
            $"/api/v1/accounts/{SeedIds.Alice.Checking}/transactions/{benEntry}",
            $"/api/v1/accounts/{SeedIds.PayeeClearingAccount}",
        ];
        foreach (var url in urls)
        {
            var response = await alice.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not_found", await response.CodeAsync());
        }

        var fromBen = await alice.QuoteAsync(SeedIds.Ben.Checking, "account", SeedIds.Alice.Savings, 1m);
        Assert.Equal(HttpStatusCode.NotFound, fromBen.StatusCode);

        var toBenPayee = await alice.QuoteAsync(SeedIds.Alice.Checking, "payee", SeedIds.Ben.Sister, 1m);
        Assert.Equal("destination_not_found", await toBenPayee.CodeAsync());

        var payees = await (await alice.GetAsync("/api/v1/payees")).JsonAsync();
        Assert.Equal(["City Power", "Gym", "Landlord", "Mom"],
            payees.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("nickname").GetString()));
    }

    [Fact]
    public async Task Account_etag_returns_304_when_unchanged()
    {
        var client = await factory.SignInAsync("alice");
        var first = await client.GetAsync($"/api/v1/accounts/{SeedIds.Alice.Savings}");
        var etag = first.Headers.ETag!.Tag;

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/accounts/{SeedIds.Alice.Savings}");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task History_paging_returns_every_entry_once_while_a_transfer_posts_mid_scroll()
    {
        var client = await factory.SignInAsync("alice");
        var url = $"/api/v1/accounts/{SeedIds.Alice.Checking}/transactions?pageSize=37";
        var seen = new List<Guid>();
        var totalAtStart = -1;
        string? cursor = null;
        var page = 0;
        do
        {
            var body = await (await client.GetAsync(cursor is null ? url : $"{url}&cursor={cursor}")).JsonAsync();
            if (totalAtStart < 0) totalAtStart = body.GetProperty("totalCount").GetInt32();
            seen.AddRange(body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
            cursor = body.GetProperty("page").GetProperty("nextCursor").GetString();

            if (++page == 2)
            {
                var quote = await client.QuoteIdAsync(SeedIds.Alice.Checking, "account", SeedIds.Alice.Savings, 1m);
                Assert.Equal(HttpStatusCode.Created, (await client.ExecuteAsync(quote, Guid.NewGuid())).StatusCode);
            }
        } while (cursor is not null);

        Assert.True(totalAtStart >= 300, $"expected about 320 entries, got {totalAtStart}");
        Assert.Equal(totalAtStart, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task Running_balance_follows_from_the_previous_entry()
    {
        var client = await factory.SignInAsync("alice");
        var body = await (await client.GetAsync($"/api/v1/accounts/{SeedIds.Alice.Checking}/transactions?pageSize=100")).JsonAsync();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        for (var i = 0; i < items.Count - 1; i++)
            Assert.Equal(items[i + 1].GetProperty("runningBalance").GetDecimal() + items[i].GetProperty("signedAmount").GetDecimal(),
                items[i].GetProperty("runningBalance").GetDecimal());
    }

    [Fact]
    public async Task Filters_search_and_bad_queries()
    {
        var client = await factory.SignInAsync("alice");
        var baseUrl = $"/api/v1/accounts/{SeedIds.Alice.Checking}/transactions";

        var coffee = await (await client.GetAsync($"{baseUrl}?q=COFFEE&type=debit&pageSize=100")).JsonAsync();
        var items = coffee.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i =>
        {
            Assert.Contains("coffee", i.GetProperty("description").GetString()!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("debit", i.GetProperty("type").GetString());
        });

        var range = await client.GetAsync($"{baseUrl}?from=2026-09-10&to=2026-09-01");
        Assert.Equal("invalid_date_range", await range.CodeAsync());

        var first = await (await client.GetAsync($"{baseUrl}?pageSize=5")).JsonAsync();
        var cursor = first.GetProperty("page").GetProperty("nextCursor").GetString();
        var reused = await client.GetAsync($"{baseUrl}?pageSize=5&type=credit&cursor={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal("invalid_cursor", await reused.CodeAsync());

        Assert.Equal("bad_request", await (await client.GetAsync($"{baseUrl}?pageSize=500")).CodeAsync());
        Assert.Equal("bad_request", await (await client.GetAsync($"{baseUrl}?q=a")).CodeAsync());
    }

    [Fact]
    public Task Chloe_savings_has_no_history() =>
        // Read through the database: chloe may be locked out by the lockout test.
        factory.WithDbAsync(async db =>
            Assert.False(await db.LedgerEntries.AnyAsync(e => e.AccountId == SeedIds.Chloe.Savings)));
}
