using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Repositories;

public interface IAccountRepository
{
    /// <summary>The customer's accounts, checking first, then by name.</summary>
    Task<List<Account>> GetAccountsForCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>The account, or null when it does not exist or belongs to someone else.</summary>
    Task<Account?> GetOwnedAccountAsync(Guid customerId, Guid accountId, CancellationToken ct);

    /// <summary>The bank's internal account that payee payments credit.</summary>
    Task<Guid> GetClearingAccountIdAsync(CancellationToken ct);

    /// <summary>SELECT … FOR UPDATE on the given accounts, in ascending id order. Needs a transaction.</summary>
    Task<List<Account>> LockAccountsAsync(Guid[] ids, CancellationToken ct);
}

public class AccountRepository(BankDbContext db) : IAccountRepository
{
    public Task<List<Account>> GetAccountsForCustomerAsync(Guid customerId, CancellationToken ct) =>
        db.Accounts.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.Kind == AccountKinds.Checking ? 0 : 1).ThenBy(a => a.Name)
            .ToListAsync(ct);

    public Task<Account?> GetOwnedAccountAsync(Guid customerId, Guid accountId, CancellationToken ct) =>
        db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == accountId && a.CustomerId == customerId, ct);

    public Task<Guid> GetClearingAccountIdAsync(CancellationToken ct) =>
        db.Accounts.Where(a => a.Kind == AccountKinds.Clearing).Select(a => a.Id).FirstAsync(ct);

    public Task<List<Account>> LockAccountsAsync(Guid[] ids, CancellationToken ct) =>
        db.Accounts.FromSql($"SELECT *, xmin FROM accounts WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
}
