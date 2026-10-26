using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Infrastructure;

namespace OnlineBankingAPI.Assistant.Models;

/// <summary>
/// Answers without a language model, for running with no API key. It recognizes three kinds of question
/// (balance, recent transactions, spending in a period) and calls the same read-only tools Claude uses,
/// so its answers come from the customer's real data.
/// </summary>
public partial class MockAssistantModel(TimeProvider clock, IOptions<BankOptions> bank) : IAssistantModel
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Spending words and the merchant-name keywords that count for them.</summary>
    private static readonly (string[] Words, string Label, string[] Merchants)[] Categories =
    [
        (["grocery", "groceries"], "groceries", ["grocer", "market", "supermarket"]),
        (["coffee", "cafe", "café"], "coffee", ["coffee", "cafe", "grind", "bean"]),
        (["fuel", "gas", "petrol"], "fuel", ["fuel", "gas"]),
    ];

    public string Mode => "mock";

    public async IAsyncEnumerable<ModelStreamItem> StreamAsync(ModelRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (question, results) = CurrentTurn(request.History);
        var step = Decide(question, results);

        if (step.Call is { } call)
        {
            yield return new ModelReply([call], "tool_use");
            yield break;
        }

        // Stream the answer a few words at a time, as a model would.
        foreach (Match chunk in Chunks().Matches(step.Answer!))
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            yield return new TextDelta(chunk.Value);
        }
        yield return new ModelReply([new TextPart(step.Answer!)], "end_turn");
    }

    private record Step(ToolCallPart? Call, string? Answer);

    private record Account(string Id, string Name, string MaskedNumber, decimal Balance);

    private record Transaction(string Date, string Description, string? Counterparty, string Type, decimal Amount);

    private Step Decide(string question, IReadOnlyList<(string Tool, JsonElement Input, JsonElement Result)> results)
    {
        if (MoveMoney().IsMatch(question.ToLowerInvariant()))
            return Answer("I can't move money or make payments; I can only look at your accounts. " +
                          "To send money, use the **Transfers** page.");

        if (results.Count == 0 || results[0].Tool != "list_accounts")
            return Call("list_accounts", new { });

        var accounts = results[0].Result.GetProperty("accounts").EnumerateArray()
            .Select(a => new Account(a.GetProperty("id").GetString()!, a.GetProperty("name").GetString()!,
                a.GetProperty("maskedNumber").GetString()!, Amount(a.GetProperty("balance"))))
            .ToList();
        var text = question.ToLowerInvariant();
        var named = NamedAccount(text, accounts);
        var account = named ?? accounts.FirstOrDefault(a => a.Name.Contains("checking", StringComparison.OrdinalIgnoreCase))
                      ?? accounts.FirstOrDefault();
        if (account is null) return Answer("I don't see any accounts for you.");

        var searches = results.Skip(1).Where(r => r.Tool == "search_transactions").ToList();

        if (Spending().IsMatch(text))
        {
            var (from, to, periodLabel) = Period(text);
            var cursor = searches.Count == 0 ? null : NextCursor(searches[^1].Result);
            if (searches.Count == 0 || (cursor is not null && searches.Count < 10))
            {
                return Call("search_transactions", new
                {
                    accountId = account.Id,
                    from = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    to = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    type = "debit",
                    limit = 100,
                    cursor,
                });
            }
            return Answer(SpendingAnswer(text, account, periodLabel, searches.SelectMany(s => Items(s.Result)).ToList()));
        }

        if (Transactions().IsMatch(text))
        {
            var count = Math.Clamp(Count(text) ?? 5, 1, 100);
            if (searches.Count == 0)
                return Call("search_transactions", new { accountId = account.Id, limit = count });
            var items = Items(searches[0].Result).ToList();
            if (items.Count == 0) return Answer($"I don't see any transactions in {account.Name} yet.");
            var list = new StringBuilder($"Here {(items.Count == 1 ? "is your last transaction" : $"are your last {items.Count} transactions")} in {account.Name}:\n\n");
            foreach (var t in items)
                list.Append($"- {Day(t.Date)}: {t.Description}, {(t.Type == "debit" ? "-" : "+")}{Money(t.Amount)}\n");
            return Answer(list.ToString().TrimEnd());
        }

        if (Balance().IsMatch(text))
        {
            if (named is not null)
                return Answer($"Your {named.Name} ({named.MaskedNumber}) balance is **{Money(named.Balance)}**.");
            var all = new StringBuilder("Here are your balances:\n\n");
            foreach (var a in accounts) all.Append($"- {a.Name} ({a.MaskedNumber}): {Money(a.Balance)}\n");
            all.Append($"\nTotal: **{Money(Amount(results[0].Result.GetProperty("totalBalance")))}**");
            return Answer(all.ToString());
        }

        return Answer("In demo mode I can answer three kinds of question: your balances (\"What is my checking " +
                      "balance?\"), recent transactions (\"Show my last five transactions.\") and spending " +
                      "(\"How much did I spend on groceries this month?\").");
    }

    private static string SpendingAnswer(string text, Account account, string periodLabel, List<Transaction> debits)
    {
        var category = Categories.FirstOrDefault(c => c.Words.Any(text.Contains));
        var matched = category.Merchants is null
            ? debits
            : debits.Where(t => category.Merchants.Any(m =>
                (t.Counterparty ?? t.Description).Contains(m, StringComparison.OrdinalIgnoreCase))).ToList();
        var label = category.Label ?? "in total";
        var on = category.Label is null ? "" : $" on {label}";

        if (matched.Count == 0)
            return $"I don't see any {(category.Label is null ? "spending" : label + " purchases")} in {account.Name} {periodLabel}.";

        var merchants = matched.Select(t => t.Counterparty ?? t.Description).Distinct().Order().ToList();
        var purchases = matched.Count == 1 ? "1 purchase" : $"{matched.Count} purchases";
        var where = category.Label is null ? "" : $" at {string.Join(", ", merchants)}";
        return $"You spent **{Money(matched.Sum(t => t.Amount))}**{on} in {account.Name} {periodLabel}, " +
               $"across {purchases}{where}.";
    }

    private (DateOnly From, DateOnly To, string Label) Period(string text)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), bank.Value.GetTimeZone()).DateTime);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        if (text.Contains("last month"))
        {
            var start = thisMonth.AddMonths(-1);
            return (start, thisMonth.AddDays(-1), $"in {start.ToString("MMMM yyyy", Us)}");
        }
        if (text.Contains("today")) return (today, today, "today");
        return (thisMonth, today, $"this month ({Day(thisMonth)} to {Day(today)})");
    }

    /// <summary>The account whose name shares the most words with the question ("checking" finds Everyday Checking).</summary>
    private static Account? NamedAccount(string text, List<Account> accounts) =>
        accounts
            .Select(a => (Account: a, Words: a.Name.ToLowerInvariant().Split(' ')
                .Count(w => Regex.IsMatch(text, $@"\b{Regex.Escape(w)}\b"))))
            .Where(x => x.Words > 0)
            .OrderByDescending(x => x.Words).ThenBy(x => x.Account.Name.Length)
            .Select(x => x.Account)
            .FirstOrDefault();

    private static int? Count(string text)
    {
        if (Digits().Match(text) is { Success: true } m) return int.Parse(m.Value, CultureInfo.InvariantCulture);
        string[] words = ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];
        var index = Array.FindIndex(words, w => Regex.IsMatch(text, $@"\b{w}\b"));
        return index >= 0 ? index + 1 : null;
    }

    /// <summary>The latest user question, and the tool calls made for it so far with their results.</summary>
    private static (string Question, List<(string, JsonElement, JsonElement)> Results) CurrentTurn(
        IReadOnlyList<ChatMessage> history)
    {
        var start = history.Count - 1;
        while (start >= 0 && !(history[start].Role == ChatRole.User && history[start].Parts.Any(p => p is TextPart)))
            start--;
        var question = start >= 0 ? string.Concat(history[start].Parts.OfType<TextPart>().Select(p => p.Text)) : "";

        var calls = new Dictionary<string, ToolCallPart>();
        var results = new List<(string, JsonElement, JsonElement)>();
        foreach (var part in history.Skip(start + 1).SelectMany(m => m.Parts))
        {
            if (part is ToolCallPart call) calls[call.Id] = call;
            else if (part is ToolResultPart { IsError: false } result && calls.TryGetValue(result.ToolCallId, out var c))
                results.Add((c.Name, c.Input, JsonDocument.Parse(result.Json).RootElement.Clone()));
        }
        return (question, results);
    }

    private static Step Call(string tool, object input) =>
        new(new ToolCallPart($"mock_{Guid.NewGuid():N}", tool, JsonSerializer.SerializeToElement(input)), null);

    private static Step Answer(string text) => new(null, text);

    private static IEnumerable<Transaction> Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(t => new Transaction(
            t.GetProperty("date").GetString()!, t.GetProperty("description").GetString()!,
            t.GetProperty("counterparty").GetString(), t.GetProperty("type").GetString()!, Amount(t.GetProperty("amount"))));

    private static string? NextCursor(JsonElement page) =>
        page.TryGetProperty("nextCursor", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

    private static decimal Amount(JsonElement value) => decimal.Parse(value.GetString()!, CultureInfo.InvariantCulture);

    private static string Money(decimal amount) => amount.ToString("C", Us);

    private static string Day(string isoDate) => Day(DateOnly.ParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static string Day(DateOnly date) => date.ToString("MMM d", Us);

    [GeneratedRegex(@"\bspen[dt]|\bspending\b|\bcost me\b")]
    private static partial Regex Spending();

    [GeneratedRegex(@"\btransactions?\b|\bactivity\b|\bpurchases\b|\bpayments\b|\brecent\b")]
    private static partial Regex Transactions();

    [GeneratedRegex(@"\bbalances?\b|\bhow much (do i have|is in|money)\b")]
    private static partial Regex Balance();

    [GeneratedRegex(@"^\s*(please\s+)?(send|transfer|pay|move|wire|withdraw|deposit|close|open|change|cancel)\b|\b(send|transfer|pay|move) \$?\d")]
    private static partial Regex MoveMoney();

    [GeneratedRegex(@"\b\d{1,3}\b")]
    private static partial Regex Digits();

    [GeneratedRegex(@"\S+\s*")]
    private static partial Regex Chunks();
}
