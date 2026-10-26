using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>
/// Opaque keyset cursor: base64url of the last item's (timestamp, tie-breaker) plus a hash of the filters,
/// so a cursor reused with different filters is rejected.
/// </summary>
public static class Cursor
{
    private record Payload(long T, string K, string F);

    public static string Encode(DateTimeOffset at, string tieBreaker, string filterHash) =>
        Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Payload(at.UtcTicks, tieBreaker, filterHash)));

    public static (DateTimeOffset At, string TieBreaker) Decode(string cursor, string filterHash)
    {
        try
        {
            var p = JsonSerializer.Deserialize<Payload>(FromBase64Url(cursor));
            if (p is null || p.F != filterHash) throw Invalid();
            return (new DateTimeOffset(p.T, TimeSpan.Zero), p.K);
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        {
            throw Invalid();
        }
    }

    public static string FilterHash(params object?[] parts)
    {
        var text = string.Join("|", parts.Select(p => p?.ToString() ?? ""));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 8);
    }

    private static ApiProblemException Invalid() =>
        new(StatusCodes.Status400BadRequest, ProblemCodes.InvalidCursor,
            "The cursor is not valid for this query. Start again without a cursor.");

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
