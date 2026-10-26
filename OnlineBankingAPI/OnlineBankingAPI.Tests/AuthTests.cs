using System.Net;
using System.Net.Http.Json;
using OnlineBankingAPI.Data;

namespace OnlineBankingAPI.Tests;

[Collection(ApiCollection.Name)]
public class AuthTests(BankApiFactory factory)
{
    [Fact]
    public async Task Login_sets_an_HttpOnly_cookie_that_authenticates_later_requests()
    {
        var client = factory.Anonymous();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "ALICE", password = DbSeeder.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("obk_session=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"(?i)path=/(;|$)", cookie);

        var me = await (await client.GetAsync("/api/v1/auth/me")).JsonAsync();
        Assert.Equal("alice", me.GetProperty("username").GetString());
    }

    [Theory]
    [InlineData("alice", "wrong")]
    [InlineData("nobody", "Demo#2026")]
    public async Task Bad_credentials_get_the_same_401(string username, string password)
    {
        var response = await factory.Anonymous().PostAsJsonAsync("/api/v1/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal("invalid_credentials", body.GetProperty("code").GetString());
        Assert.Equal("The username or password is incorrect.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Five_failures_lock_the_user()
    {
        // Uses chloe so the lock does not affect other tests.
        var client = factory.Anonymous();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "chloe", password = "wrong" });

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "chloe", password = DbSeeder.DemoPassword });

        Assert.Equal((HttpStatusCode)423, response.StatusCode);
        Assert.Equal("account_locked", await response.CodeAsync());
    }

    [Fact]
    public async Task After_logout_the_token_gets_401_on_every_protected_endpoint()
    {
        var client = await factory.SignInAsync("ben");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/accounts")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);

        string[] gets =
        [
            "/api/v1/auth/me", "/api/v1/accounts", $"/api/v1/accounts/{SeedIds.Ben.Checking}",
            $"/api/v1/accounts/{SeedIds.Ben.Checking}/transactions", "/api/v1/payees", "/api/v1/transfers",
        ];
        foreach (var url in gets)
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("unauthenticated", await response.CodeAsync());
        }

        var quote = await client.QuoteAsync(SeedIds.Ben.Checking, "account", SeedIds.Ben.Savings, 1m);
        Assert.Equal(HttpStatusCode.Unauthorized, quote.StatusCode);
    }

    [Fact]
    public async Task No_session_gets_401()
    {
        var response = await factory.Anonymous().GetAsync("/api/v1/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
