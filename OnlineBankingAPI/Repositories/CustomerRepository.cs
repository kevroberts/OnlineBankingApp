using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Repositories;

public interface ICustomerRepository
{
    /// <summary>Tracked, because sign-in updates the failed-attempt counter and lockout.</summary>
    Task<Customer?> GetByUsernameAsync(string username, CancellationToken ct);

    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken ct);
}

public class CustomerRepository(BankDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByUsernameAsync(string username, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(c => c.Username == username, ct);

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken ct) =>
        db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
}
