using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Repositories;

public interface IPayeeRepository
{
    /// <summary>The customer's saved payees, ordered by nickname.</summary>
    Task<List<Payee>> GetPayeesForCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>The payee, or null when it does not exist or belongs to someone else.</summary>
    Task<Payee?> GetOwnedPayeeAsync(Guid customerId, Guid payeeId, CancellationToken ct);
}

public class PayeeRepository(BankDbContext db) : IPayeeRepository
{
    public Task<List<Payee>> GetPayeesForCustomerAsync(Guid customerId, CancellationToken ct) =>
        db.Payees.AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderBy(p => p.Nickname)
            .ToListAsync(ct);

    public Task<Payee?> GetOwnedPayeeAsync(Guid customerId, Guid payeeId, CancellationToken ct) =>
        db.Payees.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == payeeId && p.CustomerId == customerId, ct);
}
