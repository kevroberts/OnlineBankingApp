using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Assistant.Tools;

public class SearchTransactionsTool(AccountService accounts, IOptions<BankOptions> options) : IAssistantTool
{
    public const int MaxLimit = 100;

    public string Name => "search_transactions";

    public string Description =>
        "Returns one account's transactions, newest first. Dates are YYYY-MM-DD in the bank's time zone and both " +
        "ends are inclusive. type is debit (money out) or credit (money in). text matches the description or " +
        $"counterparty. limit is 1 to {MaxLimit} (default 25). When nextCursor is not null, pass it as cursor, " +
        "with the same filters, to get the next page. Amounts are positive; type says the direction.";

    public JsonElement InputSchema { get; } = ToolJson.Schema(new
    {
        type = "object",
        properties = new
        {
            accountId = new { type = "string", description = "An account id from list_accounts." },
            from = new { type = "string", description = "First day, YYYY-MM-DD." },
            to = new { type = "string", description = "Last day, YYYY-MM-DD." },
            type = new { type = "string", @enum = new[] { "debit", "credit" } },
            text = new { type = "string", description = "Words to find in the description or counterparty." },
            limit = new { type = "integer", description = $"1 to {MaxLimit}." },
            cursor = new { type = "string", description = "nextCursor from the previous page." },
        },
        required = new[] { "accountId" },
        additionalProperties = false,
    });

    public async Task<string> ExecuteAsync(Guid customerId, JsonElement input, CancellationToken ct)
    {
        if (!Guid.TryParse(ToolJson.OptionalString(input, "accountId"), out var accountId))
            throw new ToolInputException("accountId must be an account id from list_accounts.");

        var limit = 25;
        if (input.TryGetProperty("limit", out var limitValue) && limitValue.ValueKind != JsonValueKind.Null)
        {
            if (!limitValue.TryGetInt32(out limit) || limit is < 1 or > MaxLimit)
                throw new ToolInputException($"limit must be a whole number from 1 to {MaxLimit}.");
        }

        var page = await accounts.GetHistoryAsync(customerId, accountId,
            Date(input, "from"), Date(input, "to"), ToolJson.OptionalString(input, "type"),
            ToolJson.OptionalString(input, "text"), limit, ToolJson.OptionalString(input, "cursor"), ct);

        var tz = options.Value.GetTimeZone();
        return ToolJson.Serialize(new
        {
            accountId = page.AccountId,
            totalCount = page.TotalCount,
            items = page.Items.Select(t => new
            {
                date = TimeZoneInfo.ConvertTime(t.PostedAt, tz).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                description = t.Description,
                counterparty = t.Counterparty,
                type = t.Type,
                amount = ToolJson.Money(t.Amount),
                category = t.Category,
            }),
            nextCursor = page.Page.NextCursor,
        });
    }

    private static DateOnly? Date(JsonElement input, string name) =>
        ToolJson.OptionalString(input, name) is not { } value
            ? null
            : DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : throw new ToolInputException($"{name} must be a date like 2026-10-01.");
}
