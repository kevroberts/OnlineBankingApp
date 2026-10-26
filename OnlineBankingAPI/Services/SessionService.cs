using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Repositories;

namespace OnlineBankingAPI.Services;

/// <summary>Opaque server-side sessions: 256-bit random token, only its SHA-256 hash is stored.</summary>
public class SessionService(ISessionRepository sessions, BankDbContext db, TimeProvider clock,
    IOptions<BankOptions> options)
{
    public const string CookieName = "obk_session";
    /// <summary>Site-wide so the web front end's proxy.ts can see the cookie on page requests.</summary>
    public const string CookiePath = "/";

    private readonly BankOptions _options = options.Value;

    public async Task<(string Token, Session Session)> CreateAsync(Customer customer, string? userAgent,
        CancellationToken ct)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = clock.UtcNow();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TokenHash = Hash(token),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now + _options.SessionIdleTimeout,
            UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent,
        };
        sessions.Add(session);
        await db.SaveChangesAsync(ct);
        return (token, session);
    }

    /// <summary>Loads a live session and slides its idle timeout, capped by the absolute lifetime.</summary>
    public async Task<Session?> ValidateAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var now = clock.UtcNow();
        var session = await sessions.GetActiveByTokenHashAsync(hash, now, ct);
        if (session is null) return null;

        var cap = session.CreatedAt + _options.SessionAbsoluteLifetime;
        var sliding = now + _options.SessionIdleTimeout;
        session.LastSeenAt = now;
        session.ExpiresAt = sliding < cap ? sliding : cap;
        await db.SaveChangesAsync(ct);
        return session;
    }

    /// <summary>Revokes the session for this token if it is still live. Safe to call repeatedly.</summary>
    public Task RevokeAsync(string token, CancellationToken ct) =>
        sessions.RevokeAsync(Hash(token), clock.UtcNow(), ct);

    /// <summary>Reads the token from the obk_session cookie, or from Authorization: Bearer.</summary>
    public static string? ReadToken(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(CookieName, out var cookie) && !string.IsNullOrWhiteSpace(cookie))
            return cookie;

        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && header.Length > prefix.Length
            ? header[prefix.Length..].Trim()
            : null;
    }

    public static CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        IsEssential = true,
    };

    public static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
