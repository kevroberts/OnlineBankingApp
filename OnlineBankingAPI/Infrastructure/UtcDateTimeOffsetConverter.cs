using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Writes timestamps as ISO 8601 UTC with a Z suffix (2026-10-21T14:05:00Z).</summary>
public class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFF'Z'", CultureInfo.InvariantCulture));
}
