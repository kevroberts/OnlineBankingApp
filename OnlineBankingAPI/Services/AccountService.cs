using Microsoft.Extensions.Options;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Repositories;

namespace OnlineBankingAPI.Services;

/// <summary>An account with the ETag of its row version, for conditional GETs.</summary>
public record VersionedAccount(AccountDto Account, string ETag);

/// <summary>Accounts overview and transaction history.</summary>
public class AccountService(
    IAccountRepository accounts,
    ILedgerRepository ledger,
    TimeProvider clock,
    IOptions<BankOptions> options)
{
    public async Task<AccountListDto> GetAccountsAsync(Guid customerId, CancellationToken ct)
    {
        var list = await accounts.GetAccountsForCustomerAsync(customerId, ct);
        var now = clock.GetUtcNow();
        return new AccountListDto(list.Select(a => ToDto(a, now)).ToList(), list.Sum(a => a.Balance));
    }

    public async Task<VersionedAccount> GetAccountAsync(Guid customerId, Guid accountId, CancellationToken ct)
    {
        var account = await FindOwnAsync(customerId, accountId, ct);
        return new VersionedAccount(ToDto(account, clock.GetUtcNow()), $"\"{account.Version}\"");
    }

    public async Task<TransactionPageDto> GetHistoryAsync(Guid customerId, Guid accountId, DateOnly? from,
        DateOnly? to, string? type, string? q, int pageSize, string? cursor, CancellationToken ct)
    {
        if (pageSize is < 1 or > 100) throw BadRequest("pageSize must be between 1 and 100.");
        if (type is not (null or "debit" or "credit")) throw BadRequest("type must be debit or credit.");
        if (q is not null && q.Length is < 2 or > 100) throw BadRequest("q must be 2 to 100 characters.");
        if (from > to)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, ProblemCodes.InvalidDateRange,
                "from must be on or before to.");

        var account = await FindOwnAsync(customerId, accountId, ct);

        var filterHash = Cursor.FilterHash("transactions", account.Id, from, to, type, q?.ToLowerInvariant());
        (DateTimeOffset, long)? after = null;
        if (cursor is not null)
        {
            var (at, tie) = Cursor.Decode(cursor, filterHash);
            if (!long.TryParse(tie, out var sequence)) throw BadRequest("Invalid cursor.", ProblemCodes.InvalidCursor);
            after = (at, sequence);
        }

        // Dates are whole days in the bank's time zone.
        var tz = options.Value.GetTimeZone();
        var page = await ledger.GetHistoryPageAsync(account.Id, new HistoryQuery(
            from is { } f ? StartOfDay(f, tz) : null,
            to is { } t ? StartOfDay(t.AddDays(1), tz) : null,
            type switch { "debit" => Directions.Debit, "credit" => Directions.Credit, _ => null },
            q,
            after,
            pageSize), ct);

        var items = page.Items;
        var next = page.HasMore ? Cursor.Encode(items[^1].PostedAt, items[^1].Sequence.ToString(), filterHash) : null;
        return new TransactionPageDto(account.Id, items.Select(ToDto).ToList(),
            new PageInfo(pageSize, next, page.HasMore), page.TotalCount);
    }

    public async Task<TransactionDto> GetTransactionAsync(Guid customerId, Guid accountId, Guid transactionId,
        CancellationToken ct)
    {
        var account = await FindOwnAsync(customerId, accountId, ct);
        var entry = await ledger.GetEntryAsync(account.Id, transactionId, ct)
                    ?? throw ApiProblemException.NotFound("The transaction was not found.");
        return ToDto(entry);
    }

    private async Task<Account> FindOwnAsync(Guid customerId, Guid accountId, CancellationToken ct) =>
        await accounts.GetOwnedAccountAsync(customerId, accountId, ct)
        ?? throw ApiProblemException.NotFound("The account was not found.");

    private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, tz), TimeSpan.Zero);
    }

    private static ApiProblemException BadRequest(string detail, string code = ProblemCodes.BadRequest) =>
        new(StatusCodes.Status400BadRequest, code, detail);

    private static AccountDto ToDto(Account a, DateTimeOffset asOf) =>
        // availableBalance equals balance until holds exist.
        new(a.Id, a.Kind, a.Name, a.MaskedNumber, a.Currency, a.Balance, a.Balance, asOf);

    private static TransactionDto ToDto(LedgerEntry e) => new(
        e.Id, e.PostedAt, e.Description, e.Counterparty,
        e.Direction == Directions.Debit ? "debit" : "credit",
        e.Amount, e.SignedAmount, e.BalanceAfter, e.Category, e.Note, e.TransferId);
}
