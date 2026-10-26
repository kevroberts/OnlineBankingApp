using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Domain;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Repositories;

namespace OnlineBankingAPI.Services;

/// <summary>Sign-in with lockout, sign-out and the current customer.</summary>
public class AuthService(
    ICustomerRepository customers,
    SessionService sessions,
    BankDbContext db,
    IPasswordHasher<Customer> hasher,
    TimeProvider clock,
    IOptions<BankOptions> options)
{
    // Verified against when the username is unknown, so both failures take about the same time.
    private static readonly string DummyHash = new PasswordHasher<Customer>().HashPassword(new Customer(), "not-a-password");

    /// <summary>Starts a session. Throws 401 invalid_credentials or 423 account_locked.</summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? userAgent, CancellationToken ct)
    {
        var bank = options.Value;
        var now = clock.UtcNow();
        var customer = await customers.GetByUsernameAsync(request.Username, ct);

        if (customer is null)
        {
            hasher.VerifyHashedPassword(new Customer(), DummyHash, request.Password);
            throw InvalidCredentials();
        }

        if (customer.LockoutEndsAt > now)
            throw new ApiProblemException(StatusCodes.Status423Locked, ProblemCodes.AccountLocked,
                "Too many failed sign-in attempts. Try again later.");

        if (hasher.VerifyHashedPassword(customer, customer.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            customer.FailedLoginCount++;
            if (customer.FailedLoginCount >= bank.MaxFailedLogins)
            {
                customer.FailedLoginCount = 0;
                customer.LockoutEndsAt = now + bank.LockoutDuration;
            }
            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }

        customer.FailedLoginCount = 0;
        customer.LockoutEndsAt = null;
        // Saves the session and the reset counter together.
        var (token, session) = await sessions.CreateAsync(customer, userAgent, ct);
        return new LoginResponse(ToDto(customer), token, session.ExpiresAt);
    }

    /// <summary>Revokes the session for this token, if any. Safe to repeat.</summary>
    public Task LogoutAsync(string? token, CancellationToken ct) =>
        token is null ? Task.CompletedTask : sessions.RevokeAsync(token, ct);

    public async Task<CustomerDto> GetCurrentCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(customerId, ct)
                       ?? throw new InvalidOperationException("An authenticated session has no customer.");
        return ToDto(customer);
    }

    private static ApiProblemException InvalidCredentials() =>
        new(StatusCodes.Status401Unauthorized, ProblemCodes.InvalidCredentials, "The username or password is incorrect.");

    private static CustomerDto ToDto(Customer c) => new(c.Id, c.DisplayName, c.Username);
}
