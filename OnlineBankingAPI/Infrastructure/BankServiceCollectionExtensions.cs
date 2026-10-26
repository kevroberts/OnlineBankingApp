using Microsoft.AspNetCore.Identity;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Repositories;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Infrastructure;

public static class BankServiceCollectionExtensions
{
    /// <summary>
    /// Repositories and services, all scoped so a request shares one BankDbContext (the unit of work)
    /// across them. Controllers depend on services only; services on repositories.
    /// </summary>
    public static IServiceCollection AddBankServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IPayeeRepository, PayeeRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();

        services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
        services.AddScoped<SessionService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<PayeeService>();
        services.AddSingleton<TransferValidator>();
        services.AddScoped<TransferService>();

        services.AddScoped<DbSeeder>();
        services.AddHostedService<CleanupService>();
        return services;
    }
}
