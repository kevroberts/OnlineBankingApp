using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Writes money as a JSON number with exactly 2 decimals (stored as numeric(19,4)).</summary>
public class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(Math.Round(value, 2, MidpointRounding.ToEven).ToString("0.00", CultureInfo.InvariantCulture));
}
