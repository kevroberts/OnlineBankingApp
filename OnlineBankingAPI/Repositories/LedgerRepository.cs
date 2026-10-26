using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Repositories;

public record LedgerPosting(
    Guid AccountId,
    char Direction,
    decimal Amount,
    DateTimeOffset PostedAt,
    string Description,
    string? Counterparty,
    string Category,
    string? Note = null,
    Guid? TransferId = null);

/// <summary>
/// Filters for one page of history. <see cref="PostedFrom"/> is inclusive and <see cref="PostedBefore"/>
/// exclusive; <see cref="After"/> is the keyset position of the previous page's last entry.
/// </summary>
public record HistoryQuery(
    DateTimeOffset? PostedFrom,
    DateTimeOffset? PostedBefore,
    char? Direction,
    string? Search,
    (DateTimeOffset PostedAt, long Sequence)? After,
    int PageSize);

/// <summary>One page of entries, newest first; <see cref="TotalCount"/> ignores paging.</summary>
public record HistoryPage(List<LedgerEntry> Items, bool HasMore, int TotalCount);

public interface ILedgerRepository
{
    Task<HistoryPage> GetHistoryPageAsync(Guid accountId, HistoryQuery query, CancellationToken ct);

    Task<LedgerEntry?> GetEntryAsync(Guid accountId, Guid entryId, CancellationToken ct);

    /// <summary>The debit and credit legs of each transfer, keyed by transfer id.</summary>
    Task<ILookup<Guid, LedgerEntry>> GetLegsForTransfersAsync(IReadOnlyCollection<Guid> transferIds,
        CancellationToken ct);

    /// <summary>
    /// The only code that changes balances. Updates the account row (balance and the next sequence
    /// number) with one guarded UPDATE and stages an append-only entry whose BalanceAfter is the balance
    /// that UPDATE returned. Runs inside the caller's transaction; the entry is saved with the caller's
    /// SaveChanges.
    /// </summary>
    /// <returns>The new entry, or null when a debit would overdraw a customer account.</returns>
    Task<LedgerEntry?> PostEntryAsync(LedgerPosting posting, CancellationToken ct);
}

public class LedgerRepository(BankDbContext db) : ILedgerRepository
{
    public async Task<HistoryPage> GetHistoryPageAsync(Guid accountId, HistoryQuery q, CancellationToken ct)
    {
        var query = db.LedgerEntries.AsNoTracking().Where(e => e.AccountId == accountId);
        if (q.PostedFrom is { } start) query = query.Where(e => e.PostedAt >= start);
        if (q.PostedBefore is { } end) query = query.Where(e => e.PostedAt < end);
        if (q.Direction is { } direction) query = query.Where(e => e.Direction == direction);
        if (q.Search is { } search)
        {
            var pattern = "%" + search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            query = query.Where(e => EF.Functions.ILike(e.Description, pattern)
                                     || EF.Functions.ILike(e.Counterparty!, pattern)
                                     || EF.Functions.ILike(e.Note!, pattern));
        }

        var totalCount = await query.CountAsync(ct);

        if (q.After is var (at, sequence))
            query = query.Where(e => e.PostedAt < at || (e.PostedAt == at && e.Sequence < sequence));

        var items = await query.OrderByDescending(e => e.PostedAt).ThenByDescending(e => e.Sequence)
            .Take(q.PageSize + 1).ToListAsync(ct);
        var hasMore = items.Count > q.PageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new HistoryPage(items, hasMore, totalCount);
    }

    public Task<LedgerEntry?> GetEntryAsync(Guid accountId, Guid entryId, CancellationToken ct) =>
        db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId && e.AccountId == accountId, ct);

    public async Task<ILookup<Guid, LedgerEntry>> GetLegsForTransfersAsync(IReadOnlyCollection<Guid> transferIds,
        CancellationToken ct) =>
        (await db.LedgerEntries.AsNoTracking()
            .Where(e => e.TransferId != null && transferIds.Contains(e.TransferId.Value))
            .ToListAsync(ct))
        .ToLookup(e => e.TransferId!.Value);

    public async Task<LedgerEntry?> PostEntryAsync(LedgerPosting p, CancellationToken ct)
    {
        if (p.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(p), "Amount must be positive.");
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Ledger postings must run inside a transaction.");

        var signed = p.Direction == Directions.Debit ? -p.Amount : p.Amount;

        // Guarded update: a debit applies only when funds suffice (the clearing account may go negative).
        const string sql = """
            UPDATE accounts
               SET balance = balance + @signed, next_sequence = next_sequence + 1
             WHERE id = @id AND (@signed > 0 OR kind = 'clearing' OR balance >= -@signed)
            RETURNING balance, next_sequence - 1
            """;

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var cmd = new NpgsqlCommand(sql, connection,
            (NpgsqlTransaction)db.Database.CurrentTransaction.GetDbTransaction());
        cmd.Parameters.AddWithValue("signed", signed);
        cmd.Parameters.AddWithValue("id", p.AccountId);

        decimal balanceAfter;
        long sequence;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            balanceAfter = reader.GetDecimal(0);
            sequence = reader.GetInt64(1);
        }

        var entry = new LedgerEntry
        {
            Id = Guid.CreateVersion7(),
            AccountId = p.AccountId,
            Sequence = sequence,
            TransferId = p.TransferId,
            PostedAt = p.PostedAt,
            Direction = p.Direction,
            Amount = p.Amount,
            BalanceAfter = balanceAfter,
            Description = p.Description,
            Counterparty = p.Counterparty,
            Category = p.Category,
            Note = p.Note,
        };
        db.LedgerEntries.Add(entry);
        return entry;
    }
}
