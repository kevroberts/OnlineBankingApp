using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineBankingAPI.Assistant;
using OnlineBankingAPI.Assistant.Models;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Tests;

[Collection(ApiCollection.Name)]
public class AssistantTests(BankApiFactory factory)
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public async Task Balance_question_is_answered_from_the_customers_own_data()
    {
        var alice = await factory.SignInAsync("alice");
        var balance = await alice.BalanceAsync(SeedIds.Alice.Checking);

        var reply = await alice.AskAsync("What is my checking balance?");

        Assert.Equal(["conversation", "status", "text"], reply.Events.Select(e => e.Name).Distinct().Take(3));
        Assert.Equal("mock", reply.First("conversation").GetProperty("mode").GetString());
        Assert.Equal("end_turn", reply.First("done").GetProperty("stopReason").GetString());
        Assert.Equal(["list_accounts"], reply.Tools);
        Assert.Equal($"Your Everyday Checking (****{Last4(reply.Text)}) balance is **{balance.ToString("C", Us)}**.", reply.Text);
    }

    [Fact]
    public async Task Ben_gets_his_own_savings_not_his_holiday_savings()
    {
        var ben = await factory.SignInAsync("ben");
        var savings = await ben.BalanceAsync(SeedIds.Ben.Savings);
        var holiday = await ben.BalanceAsync(SeedIds.Ben.HolidaySavings);

        var reply = await ben.AskAsync("What's my savings balance?");
        Assert.StartsWith("Your Savings (****", reply.Text);
        Assert.Contains(savings.ToString("C", Us), reply.Text);
        Assert.Contains(holiday.ToString("C", Us), (await ben.AskAsync("Holiday savings balance please")).Text);
    }

    [Fact]
    public async Task Last_five_transactions_are_listed_newest_first()
    {
        var alice = await factory.SignInAsync("alice");
        var page = await (await alice.GetAsync($"/api/v1/accounts/{SeedIds.Alice.Checking}/transactions?pageSize=5")).JsonAsync();
        var descriptions = page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("description").GetString()!).ToList();

        var reply = await alice.AskAsync("Show my last five transactions.");

        Assert.Equal(["list_accounts", "search_transactions"], reply.Tools);
        var lines = reply.Text.Split('\n').Where(l => l.StartsWith("- ")).ToList();
        Assert.Equal(5, lines.Count);
        for (var i = 0; i < 5; i++) Assert.Contains(descriptions[i], lines[i]);
    }

    [Fact]
    public async Task Grocery_spending_this_month_matches_the_ledger()
    {
        var alice = await factory.SignInAsync("alice");
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(factory.Clock.GetUtcNow(), tz).DateTime);
        var from = new DateOnly(today.Year, today.Month, 1);

        var expected = 0m;
        var count = 0;
        string? cursor = null;
        do
        {
            var url = $"/api/v1/accounts/{SeedIds.Alice.Checking}/transactions?type=debit&pageSize=100" +
                      $"&from={from:yyyy-MM-dd}&to={today:yyyy-MM-dd}" + (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var page = await (await alice.GetAsync(url)).JsonAsync();
            foreach (var t in page.GetProperty("items").EnumerateArray())
            {
                var merchant = t.GetProperty("counterparty").GetString() ?? t.GetProperty("description").GetString()!;
                if (new[] { "grocer", "market", "supermarket" }.Any(k => merchant.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    expected += t.GetProperty("amount").GetDecimal();
                    count++;
                }
            }
            cursor = page.GetProperty("page").GetProperty("nextCursor").GetString();
        } while (cursor is not null);

        var reply = await alice.AskAsync("How much did I spend on groceries this month?");

        Assert.Equal("list_accounts", reply.Tools[0]);
        Assert.All(reply.Tools.Skip(1), t => Assert.Equal("search_transactions", t));
        if (count == 0)
            Assert.Contains("don't see any groceries purchases", reply.Text);
        else
            Assert.Contains($"You spent **{expected.ToString("C", Us)}** on groceries in Everyday Checking this month", reply.Text);
    }

    [Fact]
    public async Task Asking_changes_nothing_in_the_database()
    {
        var alice = await factory.SignInAsync("alice");
        var before = await CountsAsync();

        await alice.AskAsync("What is my checking balance?");
        await alice.AskAsync("Show my last 3 transactions");
        await alice.AskAsync("How much did I spend on groceries this month?");
        var transfer = await alice.AskAsync("Send $500 to Mom");

        Assert.Equal(before, await CountsAsync());
        Assert.Contains("can't move money", transfer.Text);
        Assert.Empty(transfer.Tools);
    }

    [Fact]
    public async Task A_tool_call_for_another_customers_account_returns_not_found_to_the_model()
    {
        var fake = new ScriptedModel(
            _ => [new ToolCallPart("call_1", "search_transactions", Json(new { accountId = SeedIds.Ben.Checking }))],
            _ => [new TextPart("I can't see that account.")]);
        var alice = await SignInWithAsync(fake, "alice");

        var reply = await alice.AskAsync("Show Ben's transactions");

        var result = Assert.IsType<ToolResultPart>(Assert.Single(fake.Requests[1].History[^1].Parts));
        Assert.True(result.IsError);
        Assert.Equal("not_found", JsonDocument.Parse(result.Json).RootElement.GetProperty("error").GetString());
        Assert.Equal("I can't see that account.", reply.Text);
    }

    [Fact]
    public async Task Tool_input_errors_go_back_to_the_model()
    {
        var fake = new ScriptedModel(
            _ => [new ToolCallPart("call_1", "search_transactions", Json(new { accountId = SeedIds.Alice.Checking, limit = 500 }))],
            _ => [new TextPart("Sorry.")]);
        var alice = await SignInWithAsync(fake, "alice");

        await alice.AskAsync("Show everything");

        var result = Assert.IsType<ToolResultPart>(Assert.Single(fake.Requests[1].History[^1].Parts));
        Assert.True(result.IsError);
        Assert.Equal("bad_input", JsonDocument.Parse(result.Json).RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_follow_up_question_continues_the_same_conversation()
    {
        var fake = new ScriptedModel(_ => [new TextPart("First answer.")], _ => [new TextPart("Second answer.")]);
        var alice = await SignInWithAsync(fake, "alice");

        var first = await alice.AskAsync("Hello");
        var id = first.First("conversation").GetProperty("conversationId").GetGuid();
        var second = await alice.AskAsync("And then?", id);

        Assert.Equal(id, second.First("conversation").GetProperty("conversationId").GetGuid());
        Assert.Equal(3, fake.Requests[1].History.Count); // question, answer, follow-up
        Assert.Contains("Today is", fake.Requests[1].Context);
    }

    [Fact]
    public async Task Unknown_or_ended_conversations_report_expired()
    {
        var alice = await factory.SignInAsync("alice");
        var first = await alice.AskAsync("What is my checking balance?");
        var id = first.First("conversation").GetProperty("conversationId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/v1/assistant/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.DeleteAsync($"/api/v1/assistant/conversations/{id}")).StatusCode);

        var reply = await alice.AskAsync("And savings?", id);
        Assert.Equal("conversation_expired", reply.First("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Another_session_cannot_continue_a_conversation()
    {
        var alice = await factory.SignInAsync("alice");
        var id = (await alice.AskAsync("What is my checking balance?")).First("conversation").GetProperty("conversationId").GetGuid();

        var ben = await factory.SignInAsync("ben");
        var otherAlice = await factory.SignInAsync("alice");

        Assert.Equal("conversation_expired", (await ben.AskAsync("Hi", id)).First("error").GetProperty("code").GetString());
        Assert.Equal("conversation_expired", (await otherAlice.AskAsync("Hi", id)).First("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Signing_out_drops_the_sessions_conversations()
    {
        var alice = await factory.SignInAsync("alice");
        var id = (await alice.AskAsync("What is my checking balance?")).First("conversation").GetProperty("conversationId").GetGuid();
        var sessionId = await SessionIdAsync(alice);
        var store = factory.Services.GetRequiredService<AssistantConversationStore>();
        Assert.NotNull(store.Find(sessionId, SeedIds.Alice.Customer, id));

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync("/api/v1/auth/logout", null)).StatusCode);

        Assert.Null(store.Find(sessionId, SeedIds.Alice.Customer, id));
    }

    [Fact]
    public async Task Requests_without_a_session_or_text_are_rejected_before_streaming()
    {
        var anonymous = await factory.Anonymous().PostAsJsonAsync("/api/v1/assistant/messages", new { text = "Hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var alice = await factory.SignInAsync("alice");
        foreach (var text in new[] { "", "   " })
        {
            var response = await alice.PostAsJsonAsync("/api/v1/assistant/messages", new { text });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("bad_request", await response.CodeAsync());
        }
    }

    [Fact]
    public async Task A_model_failure_ends_with_an_error_and_leaves_no_partial_turn()
    {
        var fake = new ScriptedModel(_ => throw new HttpRequestException("model down"), _ => [new TextPart("Back again.")]);
        var alice = await SignInWithAsync(fake, "alice");

        var failed = await alice.AskAsync("Hello");
        Assert.Equal("assistant_unavailable", failed.First("error").GetProperty("code").GetString());

        var id = failed.First("conversation").GetProperty("conversationId").GetGuid();
        var retried = await alice.AskAsync("Hello again", id);
        Assert.Equal("Back again.", retried.Text);
        Assert.Single(fake.Requests[1].History); // only the new question
    }

    [Fact]
    public async Task A_turn_that_keeps_calling_tools_stops_at_the_round_limit()
    {
        var fake = new ScriptedModel(Enumerable.Repeat<Func<ModelRequest, IReadOnlyList<ChatPart>>>(
            _ => [new ToolCallPart($"call_{Guid.NewGuid():N}", "list_accounts", Json(new { }))], 10).ToArray());
        var alice = await SignInWithAsync(fake, "alice");

        var reply = await alice.AskAsync("Loop forever");

        Assert.Equal(6, fake.Requests.Count);
        Assert.Equal("max_tool_rounds", reply.First("done").GetProperty("stopReason").GetString());
        Assert.Contains("couldn't finish", reply.Text);
    }

    private async Task<HttpClient> SignInWithAsync(IAssistantModel model, string username)
    {
        var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAssistantModel>();
            s.AddSingleton(model);
        }));
        var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = DbSeeder.DemoPassword });
        var token = (await login.JsonAsync()).GetProperty("sessionToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SessionIdAsync(HttpClient client)
    {
        var hash = SessionService.Hash(client.DefaultRequestHeaders.Authorization!.Parameter!);
        var id = Guid.Empty;
        await factory.WithDbAsync(async db => id = (await db.Sessions.SingleAsync(s => s.TokenHash == hash)).Id);
        return id;
    }

    private async Task<(int, int, int, int)> CountsAsync()
    {
        var counts = (0, 0, 0, 0);
        await factory.WithDbAsync(async db => counts = (
            await db.LedgerEntries.CountAsync(), await db.Transfers.CountAsync(),
            await db.Quotes.CountAsync(), await db.Payees.CountAsync()));
        return counts;
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static string Last4(string text) => System.Text.RegularExpressions.Regex.Match(text, @"\*{4}(\d{4})").Groups[1].Value;

    /// <summary>Returns a scripted reply per model call and records every request it gets.</summary>
    private class ScriptedModel(params Func<ModelRequest, IReadOnlyList<ChatPart>>[] replies) : IAssistantModel
    {
        public ConcurrentQueue<ModelRequest> Calls { get; } = new();
        public List<ModelRequest> Requests => Calls.ToList();
        public string Mode => "claude";

        public async IAsyncEnumerable<ModelStreamItem> StreamAsync(ModelRequest request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            // Copy the history: the service keeps appending to the same list.
            var index = Calls.Count;
            Calls.Enqueue(request with { History = request.History.ToList() });
            await Task.Yield();
            var parts = replies[index](request);
            foreach (var text in parts.OfType<TextPart>()) yield return new TextDelta(text.Text);
            yield return new ModelReply(parts, parts.Any(p => p is ToolCallPart) ? "tool_use" : "end_turn");
        }
    }
}

public record AssistantReply(IReadOnlyList<(string Name, JsonElement Data)> Events)
{
    public string Text => string.Concat(Events.Where(e => e.Name == "text").Select(e => e.Data.GetProperty("delta").GetString()));

    public IReadOnlyList<string> Tools =>
        Events.Where(e => e.Name == "status").Select(e => e.Data.GetProperty("tool").GetString()!).ToList();

    public JsonElement First(string name) => Events.First(e => e.Name == name).Data;
}

public static class AssistantApi
{
    public static async Task<AssistantReply> AskAsync(this HttpClient client, string text, Guid? conversationId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assistant/messages", new { conversationId, text });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var events = new List<(string, JsonElement)>();
        foreach (var block in (await response.Content.ReadAsStringAsync()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n');
            var name = lines.Single(l => l.StartsWith("event: "))["event: ".Length..];
            var data = lines.Single(l => l.StartsWith("data: "))["data: ".Length..];
            events.Add((name, JsonDocument.Parse(data).RootElement.Clone()));
        }
        return new AssistantReply(events);
    }
}
