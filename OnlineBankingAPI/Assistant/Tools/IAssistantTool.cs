using System.Text.Json;

namespace OnlineBankingAPI.Assistant.Tools;

/// <summary>
/// A read-only tool the model may call. The customer id comes from the session, never from the input,
/// so a tool can only see the signed-in customer's data.
/// </summary>
public interface IAssistantTool
{
    string Name { get; }
    string Description { get; }
    JsonElement InputSchema { get; }

    /// <summary>Returns the result as JSON. Throws <see cref="ToolInputException"/> or ApiProblemException on bad input.</summary>
    Task<string> ExecuteAsync(Guid customerId, JsonElement input, CancellationToken ct);
}

/// <summary>The model sent arguments the tool cannot use; the message goes back to it as an error result.</summary>
public class ToolInputException(string message) : Exception(message);

internal static class ToolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);

    public static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema, Options);

    public static string Money(decimal amount) => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public static string? OptionalString(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => value.GetString() is { Length: > 0 } s ? s : null,
                _ => throw new ToolInputException($"{name} must be a string."),
            }
            : null;
}
