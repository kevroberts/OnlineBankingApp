using System.ComponentModel.DataAnnotations;

namespace OnlineBankingAPI.Contracts;

public record LoginRequest(
    [Required, MaxLength(50)] string Username,
    [Required, MaxLength(128)] string Password);

public record CustomerDto(Guid Id, string DisplayName, string Username);

public record LoginResponse(CustomerDto Customer, string SessionToken, DateTimeOffset ExpiresAt);
