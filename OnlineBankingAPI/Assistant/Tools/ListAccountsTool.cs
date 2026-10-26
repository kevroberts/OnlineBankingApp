using System.Text.Json;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Assistant.Tools;

public class ListAccountsTool(AccountService accounts) : IAssistantTool
{
    public string Name => "list_accounts";

    public string Description =>
        "Lists the signed-in customer's accounts: id, name, type, masked number and current balance in USD. " +
        "Call it to answer balance questions and to find the account id that search_transactions needs.";

    public JsonElement InputSchema { get; } = ToolJson.Schema(new
    {
        type = "object",
        properties = new { },
        additionalProperties = false,
    });

    public async Task<string> ExecuteAsync(Guid customerId, JsonElement input, CancellationToken ct)
    {
        var list = await accounts.GetAccountsAsync(customerId, ct);
        return ToolJson.Serialize(new
        {
            accounts = list.Items.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                type = a.Type,
                maskedNumber = a.MaskedNumber,
                balance = ToolJson.Money(a.Balance),
                currency = a.Currency,
            }),
            totalBalance = ToolJson.Money(list.TotalBalance),
        });
    }
}
