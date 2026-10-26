using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Repositories;

namespace OnlineBankingAPI.Services;

/// <summary>The response to a POST /transfers, either fresh or replayed from the idempotency record.</summary>
public record ExecuteOutcome(int Status, string BodyJson, Guid TransferId, bool Replayed);

/// <summary>The money-moving part of a transfer: one Transfers row with its debit and credit entries.</summary>
public record PostedTransfer(Transfer Transfer, LedgerEntry Debit, LedgerEntry Credit);

/// <summary>
/// Quotes and executes transfers. Owns the database transaction, the lock order and the single
/// SaveChanges/commit per operation; all queries go through the repositories.
/// </summary>
public class TransferService(
    BankDbContext db,
    IAccountRepository accounts,
    IPayeeRepository payees,
    ILedgerRepository ledger,
    ITransferRepository transfers,
    TransferValidator validator,
    TimeProvider clock,
    IOptions<BankOptions> options,
    IOptions<JsonOptions> json)
{
    private const string PostgresUniqueViolation = "23505";
    private const string PostgresLockNotAvailable = "55P03";

    private readonly BankOptions _options = options.Value;
    private readonly JsonSerializerOptions _json = json.Value.JsonSerializerOptions;

    // ---- Step 1: quote ------------------------------------------------------------------------

    public async Task<TransferQuoteDto> CreateQuoteAsync(Guid customerId, TransferQuoteRequest request,
        CancellationToken ct)
    {
        var source = await accounts.GetOwnedAccountAsync(customerId, request.SourceAccountId!.Value, ct)
            ?? throw ApiProblemException.NotFound("The source account was not found.");

        var destination = await ResolveDestinationAsync(customerId, request.Destination!, ct);
        var note = string.IsNullOrEmpty(request.Note) ? null : request.Note;

        var errors = validator.Validate(source, destination, request.Amount, note);
        if (errors.Count > 0) throw ApiProblemException.Validation(errors);

        var now = clock.UtcNow();
        var amount = request.Amount!.Value;
        var quote = new Quote
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            SourceAccountId = source.Id,
            DestinationAccountId = destination!.Account?.Id,
            PayeeId = destination.Payee?.Id,
            Amount = amount,
            Note = note,
            CreatedAt = now,
            ExpiresAt = now + _options.QuoteLifetime,
        };
        transfers.AddQuote(quote);
        await db.SaveChangesAsync(ct);

        return new TransferQuoteDto(
            quote.Id,
            new QuoteSourceDto(source.Id, source.Name, source.MaskedNumber, source.Balance),
            new DestinationRefDto(destination.Kind, destination.Id, destination.Name, destination.MaskedNumber),
            amount,
            Fee: 0m,
            note,
            source.Balance - amount,
            quote.ExpiresAt);
    }

    // ---- Step 2: execute ----------------------------------------------------------------------

    /// <summary>
    /// Executes a quote exactly once. The idempotency row is inserted first, inside the same transaction
    /// as the transfer: a concurrent request with the same key waits on the primary key until this one
    /// commits (then replays it) or rolls back (then runs fresh). Any failure rolls everything back, so a
    /// failed attempt leaves the key free.
    /// </summary>
    public async Task<ExecuteOutcome> ExecuteAsync(Guid customerId, Guid idempotencyKey,
        ExecuteTransferRequest request, CancellationToken ct)
    {
        var quoteId = request.QuoteId!.Value;
        var requestHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{{\"quoteId\":\"{quoteId:D}\"}}"));
        var now = clock.UtcNow();

        var existing = await transfers.FindIdempotencyRecordAsync(customerId, idempotencyKey, now, ct);
        if (existing is { ResponseStatus: > 0 }) return Replay(existing, requestHash);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await transfers.InsertIdempotencyRecordAsync(customerId, idempotencyKey, requestHash, now,
                now + _options.IdempotencyRetention, _options.IdempotencyWait, ct);
        }
        catch (Exception e) when (e.AsPostgres()?.SqlState is PostgresUniqueViolation or PostgresLockNotAvailable)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var committed = e.AsPostgres()!.SqlState == PostgresUniqueViolation
                ? await transfers.FindIdempotencyRecordAsync(customerId, idempotencyKey, now, ct)
                : null;
            if (committed is { ResponseStatus: > 0 }) return Replay(committed, requestHash);
            throw new ApiProblemException(StatusCodes.Status409Conflict, ProblemCodes.RequestInProgress,
                "A request with this Idempotency-Key is still being processed. Retry shortly.")
            { RetryAfterSeconds = 1 };
        }

        var quote = await transfers.GetQuoteForUpdateAsync(customerId, quoteId, ct) ?? throw ApiProblemException.NotFound("The quote was not found.");

        if (quote.TransferId is { } usedBy)
            throw new ApiProblemException(StatusCodes.Status409Conflict, ProblemCodes.QuoteAlreadyUsed,
                "This quote has already been executed.") { TransferId = usedBy };

        if (quote.ExpiresAt <= now)
            throw ApiProblemException.Validation(
                [new("quoteId", ProblemCodes.QuoteExpired, "The quote has expired. Create a new one.")]);

        // Lock both account rows in ascending id order so opposite transfers cannot deadlock.
        var creditAccountId = quote.DestinationAccountId ?? await accounts.GetClearingAccountIdAsync(ct);
        var locked = await accounts.LockAccountsAsync([quote.SourceAccountId, creditAccountId], ct);
        var source = locked.Single(a => a.Id == quote.SourceAccountId);

        ResolvedDestination? destination = quote.DestinationAccountId is not null
            ? locked.Where(a => a.Id == quote.DestinationAccountId && a.CustomerId == customerId)
                .Select(ResolvedDestination.From).SingleOrDefault()
            : await payees.GetOwnedPayeeAsync(customerId, quote.PayeeId!.Value, ct) is { } payee
                ? ResolvedDestination.From(payee)
                : null;

        // Balances may have changed since the quote: re-check ownership, status and funds under the locks.
        if (source.CustomerId != customerId) throw ApiProblemException.NotFound("The quote was not found.");
        var errors = validator.Validate(source, destination, quote.Amount, quote.Note);
        if (errors.Count > 0) throw ApiProblemException.Validation(errors);

        var posted = await PostTransferAsync(customerId, source, destination!, creditAccountId, quote.Amount,
                         quote.Note, now, ct)
                     ?? throw ApiProblemException.Validation([new("amount", ProblemCodes.InsufficientFunds,
                         "The amount is more than the available balance.")]);

        quote.TransferId = posted.Transfer.Id;
        await db.SaveChangesAsync(ct);

        var body = JsonSerializer.Serialize(ToDto(posted, source, destination!), _json);
        // Return the stored form, so the first response and every replay are byte-for-byte the same.
        var stored = await transfers.CompleteIdempotencyRecordAsync(customerId, idempotencyKey,
            StatusCodes.Status201Created, body, posted.Transfer.Id, ct);

        await tx.CommitAsync(ct);
        return new ExecuteOutcome(StatusCodes.Status201Created, stored, posted.Transfer.Id, Replayed: false);
    }

    /// <summary>
    /// Writes the Transfers row and its two ledger legs. A payee payment credits the bank's clearing
    /// account. Must run in a transaction with both account rows already locked. Also used by the seeder.
    /// </summary>
    /// <returns>Null when the source has insufficient funds; nothing is written in that case.</returns>
    public async Task<PostedTransfer?> PostTransferAsync(Guid customerId, Account source,
        ResolvedDestination destination, Guid creditAccountId, decimal amount, string? note, DateTimeOffset at,
        CancellationToken ct)
    {
        var number = await transfers.NextReferenceNumberAsync(ct);
        var transfer = new Transfer
        {
            Id = Guid.CreateVersion7(),
            Reference = $"TRF-{at.UtcDateTime:yyyyMMdd}-{number:D6}",
            CustomerId = customerId,
            SourceAccountId = source.Id,
            DestinationAccountId = destination.Account?.Id,
            PayeeId = destination.Payee?.Id,
            Amount = amount,
            Note = note,
            Status = TransferStatuses.Completed,
            CreatedAt = at,
        };

        var isPayment = destination.Kind == ResolvedDestination.PayeeKind;
        var category = isPayment ? Categories.Payment : Categories.Transfer;

        var debit = await ledger.PostEntryAsync(new LedgerPosting(source.Id, Directions.Debit, amount, at,
            isPayment ? $"Payment to {destination.Name}" : $"Transfer to {destination.Name}",
            isPayment ? destination.Payee!.Name : destination.Name, category, note, transfer.Id), ct);
        if (debit is null) return null;

        var credit = await ledger.PostEntryAsync(new LedgerPosting(creditAccountId, Directions.Credit, amount, at,
            isPayment ? $"Payee payment {transfer.Reference}" : $"Transfer from {source.Name}",
            isPayment ? destination.Payee!.Name : source.Name, category, note, transfer.Id), ct)
            ?? throw new InvalidOperationException("A credit posting cannot fail.");

        transfers.AddTransfer(transfer);
        return new PostedTransfer(transfer, debit, credit);
    }

    // ---- Reads --------------------------------------------------------------------------------

    public async Task<TransferDto?> GetAsync(Guid customerId, Guid transferId, CancellationToken ct)
    {
        var transfer = await transfers.GetTransferAsync(customerId, transferId, ct);
        return transfer is null ? null : (await ToDtosAsync([transfer], ct)).Single();
    }

    public async Task<TransferPageDto> ListAsync(Guid customerId, int pageSize, string? cursor, CancellationToken ct)
    {
        if (pageSize is < 1 or > 100)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, ProblemCodes.BadRequest,
                "pageSize must be between 1 and 100.");

        var filterHash = Cursor.FilterHash("transfers", customerId);
        (DateTimeOffset, string)? after = cursor is null ? null : Cursor.Decode(cursor, filterHash);

        var page = await transfers.GetTransfersPageAsync(customerId, after, pageSize + 1, ct);
        var hasMore = page.Count > pageSize;
        if (hasMore) page.RemoveAt(page.Count - 1);
        var items = await ToDtosAsync(page, ct);
        var next = hasMore ? Cursor.Encode(items[^1].CreatedAt, items[^1].Reference, filterHash) : null;
        return new TransferPageDto(items, new PageInfo(pageSize, next, hasMore));
    }

    private async Task<List<TransferDto>> ToDtosAsync(List<Transfer> list, CancellationToken ct)
    {
        var legs = await ledger.GetLegsForTransfersAsync(list.Select(t => t.Id).ToList(), ct);
        return list.Select(t =>
        {
            var destination = t.DestinationAccount is { } a
                ? ResolvedDestination.From(a)
                : ResolvedDestination.From(t.Payee!);
            var posted = new PostedTransfer(t,
                legs[t.Id].Single(e => e.Direction == Directions.Debit),
                legs[t.Id].Single(e => e.Direction == Directions.Credit));
            return ToDto(posted, t.SourceAccount, destination);
        }).ToList();
    }

    private static TransferDto ToDto(PostedTransfer p, Account source, ResolvedDestination destination) => new(
        p.Transfer.Id,
        p.Transfer.Reference,
        p.Transfer.Status,
        new TransferSourceDto(source.Id, source.Name, source.MaskedNumber, p.Debit.BalanceAfter),
        new TransferDestinationDto(destination.Kind, destination.Id, destination.Name, destination.MaskedNumber,
            // A payee's balance is not ours to show; the credit there is on the clearing account.
            destination.Account is null ? null : p.Credit.BalanceAfter),
        p.Transfer.Amount,
        p.Transfer.Note,
        p.Debit.Id,
        p.Credit.Id,
        p.Transfer.CreatedAt);

    // ---- Helpers ------------------------------------------------------------------------------

    public async Task<ResolvedDestination?> ResolveDestinationAsync(Guid customerId, DestinationDto destination,
        CancellationToken ct)
    {
        if (destination.Kind == ResolvedDestination.AccountKind)
            return await accounts.GetOwnedAccountAsync(customerId, destination.Id!.Value, ct) is { } account
                ? ResolvedDestination.From(account)
                : null;

        return await payees.GetOwnedPayeeAsync(customerId, destination.Id!.Value, ct) is { } payee
            ? ResolvedDestination.From(payee)
            : null;
    }

    private static ExecuteOutcome Replay(IdempotencyRecord record, byte[] requestHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(record.RequestHash, requestHash))
            throw ApiProblemException.Validation([new(null, ProblemCodes.IdempotencyKeyReused,
                "This Idempotency-Key was already used with a different request.")]);
        return new ExecuteOutcome(record.ResponseStatus, record.ResponseBody, record.TransferId!.Value, Replayed: true);
    }
}
