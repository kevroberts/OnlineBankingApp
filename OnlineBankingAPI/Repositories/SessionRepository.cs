using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Repositories;

public interface ISessionRepository
{
    void Add(Session session);

    /// <summary>The live session with this token hash, tracked and with its customer, or null.</summary>
    Task<Session?> GetActiveByTokenHashAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct);

    /// <summary>Marks the session revoked. Set-based, so it applies immediately without SaveChanges.</summary>
    Task RevokeAsync(byte[] tokenHash, DateTimeOffset at, CancellationToken ct);

    /// <summary>Deletes revoked and expired sessions. Set-based, applies immediately.</summary>
    Task<int> DeleteEndedAsync(DateTimeOffset now, CancellationToken ct);
}

public class SessionRepository(BankDbContext db) : ISessionRepository
{
    public void Add(Session session) => db.Sessions.Add(session);

    public Task<Session?> GetActiveByTokenHashAsync(byte[] tokenHash, DateTimeOffset now, CancellationToken ct) =>
        db.Sessions.Include(s => s.Customer)
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash && s.RevokedAt == null && s.ExpiresAt > now, ct);

    public Task RevokeAsync(byte[] tokenHash, DateTimeOffset at, CancellationToken ct) =>
        db.Sessions.Where(s => s.TokenHash == tokenHash && s.RevokedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, at), ct);

    public Task<int> DeleteEndedAsync(DateTimeOffset now, CancellationToken ct) =>
        db.Sessions.Where(s => s.RevokedAt != null || s.ExpiresAt <= now).ExecuteDeleteAsync(ct);
}
