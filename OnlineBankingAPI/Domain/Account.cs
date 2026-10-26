namespace OnlineBankingAPI.Domain;

public class Account
{
    public Guid Id { get; set; }

    /// <summary>Null only for the bank's internal accounts (the payee clearing account).</summary>
    public Guid? CustomerId { get; set; }

    public string Kind { get; set; } = AccountKinds.Checking;
    public string Name { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public decimal Balance { get; set; }
    public string Status { get; set; } = AccountStatuses.Active;
    public long NextSequence { get; set; } = 1;

    /// <summary>PostgreSQL xmin, used as the concurrency token and the ETag.</summary>
    public uint Version { get; set; }

    public string MaskedNumber => Masking.Mask(AccountNumber);
}

public static class AccountKinds
{
    public const string Checking = "checking";
    public const string Savings = "savings";
    public const string Clearing = "clearing";
}

public static class AccountStatuses
{
    public const string Active = "active";
    public const string Frozen = "frozen";
    public const string Closed = "closed";
}

public static class Masking
{
    public static string Mask(string number) =>
        "****" + (number.Length <= 4 ? number : number[^4..]);
}
