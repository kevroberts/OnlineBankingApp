using System.Net.Http.Json;
using System.Text.Json;

namespace OnlineBankingAPI.Tests;

public static class Api
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string?> CodeAsync(this HttpResponseMessage response) =>
        (await response.JsonAsync()).GetProperty("code").GetString();

    public static Task<HttpResponseMessage> QuoteAsync(this HttpClient client, Guid source, string kind, Guid destination,
        decimal? amount, string? note = null) =>
        client.PostAsJsonAsync("/api/v1/transfers/quotes",
            new { sourceAccountId = source, destination = new { kind, id = destination }, amount, note });

    public static async Task<Guid> QuoteIdAsync(this HttpClient client, Guid source, string kind, Guid destination,
        decimal amount)
    {
        var response = await client.QuoteAsync(source, kind, destination, amount);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await response.JsonAsync()).GetProperty("quoteId").GetGuid();
    }

    public static Task<HttpResponseMessage> ExecuteAsync(this HttpClient client, Guid quoteId, Guid key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        {
            Content = JsonContent.Create(new { quoteId }),
        };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return client.SendAsync(request);
    }

    public static async Task<decimal> BalanceAsync(this HttpClient client, Guid accountId) =>
        (await (await client.GetAsync($"/api/v1/accounts/{accountId}")).JsonAsync()).GetProperty("balance").GetDecimal();
}
