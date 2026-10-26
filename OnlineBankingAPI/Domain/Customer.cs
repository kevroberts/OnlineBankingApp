namespace OnlineBankingAPI.Domain;

public class Customer
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockoutEndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
