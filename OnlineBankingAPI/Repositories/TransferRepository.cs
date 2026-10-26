using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Repositories;

/// <summary>Transfers, their quotes and idempotency records: always used together in the transfer flow.</summary>
public interface ITransferRepository
{
    void AddQuote(Quote quote);

    /// <summary>Locks the customer's quote (FOR UPDATE), or returns null. Needs a transaction.</summary>
    Task<Quote?> GetQuoteForUpdateAsync(Guid customerId, Guid quoteId, CancellationToken ct);

    /// <summary>Next value of the transfer reference sequence.</summary>
    Task<long> NextReferenceNumberAsync(CancellationToken ct);

    void AddTransfer(Transfer transfer);

    /// <summary>The customer's transfer with its accounts and payee, or null.</summary>
    Task<Transfer?> GetTransferAsync(Guid customerId, Guid transferId, CancellationToken ct);

    /// <summary>
    /// Up to <paramref name="take"/> of the customer's transfers, newest first, after the keyset position
    /// <paramref name="after"/>, with their accounts and payee.
    /// </summary>
    Task<List<Transfer>> GetTransfersPageAsync(Guid customerId, (DateTimeOffset CreatedAt, string Reference)? after,
        int take, CancellationToken ct);

    /// <summary>The unexpired record for this key, or null.</summary>
    Task<IdempotencyRecord?> FindIdempotencyRecordAsync(Guid customerId, Guid key, DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Claims the key inside the caller's transaction: clears an expired record, then inserts a pending one
    /// (status 0). A concurrent request with the same key waits on the primary key for at most
    /// <paramref name="wait"/>, then fails with a unique violation (the other request committed) or a lock
    /// timeout (still running).
    /// </summary>
    Task InsertIdempotencyRecordAsync(Guid customerId, Guid key, byte[] requestHash, DateTimeOffset now,
        DateTimeOffset expiresAt, TimeSpan wait, CancellationToken ct);

    /// <summary>Stores the final response on the pending record and returns the body as stored (jsonb).</summary>
    Task<string> CompleteIdempotencyRecordAsync(Guid customerId, Guid key, int status, string body, Guid transferId,
        CancellationToken ct);

    /// <summary>Deletes expired idempotency records. Set-based, applies immediately.</summary>
    Task<int> DeleteExpiredIdempotencyRecordsAsync(DateTimeOffset now, CancellationToken ct);

    /// <summary>Deletes expired quotes. Set-based, applies immediately.</summary>
    Task<int> DeleteExpiredQuotesAsync(DateTimeOffset now, CancellationToken ct);
}

public class TransferRepository(BankDbContext db) : ITransferRepository
{
    public void AddQuote(Quote quote) => db.Quotes.Add(quote);

    public async Task<Quote?> GetQuoteForUpdateAsync(Guid customerId, Guid quoteId, CancellationToken ct) =>
        (await db.Quotes
            .FromSql($"SELECT * FROM quotes WHERE id = {quoteId} AND customer_id = {customerId} FOR UPDATE")
            .ToListAsync(ct))
        .SingleOrDefault();

    public Task<long> NextReferenceNumberAsync(CancellationToken ct) =>
        db.ScalarAsync<long>($"SELECT nextval('{BankDbContext.TransferReferenceSequence}')", ct);

    public void AddTransfer(Transfer transfer) => db.Transfers.Add(transfer);

    public Task<Transfer?> GetTransferAsync(Guid customerId, Guid transferId, CancellationToken ct) =>
        WithParties(db.Transfers.Where(t => t.Id == transferId && t.CustomerId == customerId))
            .SingleOrDefaultAsync(ct);

    public Task<List<Transfer>> GetTransfersPageAsync(Guid customerId,
        (DateTimeOffset CreatedAt, string Reference)? after, int take, CancellationToken ct)
    {
        var query = db.Transfers.Where(t => t.CustomerId == customerId);
        if (after is var (at, reference))
            query = query.Where(t => t.CreatedAt < at
                                     || (t.CreatedAt == at && string.Compare(t.Reference, reference) < 0));
        return WithParties(query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Reference).Take(take))
            .ToListAsync(ct);
    }

    public Task<IdempotencyRecord?> FindIdempotencyRecordAsync(Guid customerId, Guid key, DateTimeOffset now,
        CancellationToken ct) =>
        db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            r => r.CustomerId == customerId && r.IdempotencyKey == key && r.ExpiresAt > now, ct);

    public async Task InsertIdempotencyRecordAsync(Guid customerId, Guid key, byte[] requestHash,
        DateTimeOffset now, DateTimeOffset expiresAt, TimeSpan wait, CancellationToken ct)
    {
        // SET LOCAL lock_timeout: bounds how long a duplicate request waits for the first one.
        var timeout = $"{(int)wait.TotalMilliseconds}ms";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('lock_timeout', {timeout}, true)", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM idempotency_records
             WHERE customer_id = {customerId} AND idempotency_key = {key} AND expires_at <= {now}
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO idempotency_records
                   (customer_id, idempotency_key, request_hash, response_status, response_body, created_at, expires_at)
            VALUES ({customerId}, {key}, {requestHash}, 0, CAST({"{}"} AS jsonb), {now}, {expiresAt})
            """, ct);
    }

    public async Task<string> CompleteIdempotencyRecordAsync(Guid customerId, Guid key, int status, string body,
        Guid transferId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE idempotency_records
               SET response_status = {status}, response_body = {body}::jsonb, transfer_id = {transferId}
             WHERE customer_id = {customerId} AND idempotency_key = {key}
            """, ct);
        return await db.IdempotencyRecords.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.IdempotencyKey == key)
            .Select(r => r.ResponseBody).SingleAsync(ct);
    }

    public Task<int> DeleteExpiredIdempotencyRecordsAsync(DateTimeOffset now, CancellationToken ct) =>
        db.IdempotencyRecords.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(ct);

    public Task<int> DeleteExpiredQuotesAsync(DateTimeOffset now, CancellationToken ct) =>
        db.Quotes.Where(q => q.ExpiresAt <= now).ExecuteDeleteAsync(ct);

    private static IQueryable<Transfer> WithParties(IQueryable<Transfer> query) =>
        query.AsNoTracking().Include(t => t.SourceAccount).Include(t => t.DestinationAccount).Include(t => t.Payee);
}
