namespace OnlineBankingAPI.Domain;

/// <summary>The stored outcome of a POST /transfers, keyed by (CustomerId, IdempotencyKey).</summary>
public class IdempotencyRecord
{
    public Guid CustomerId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public byte[] RequestHash { get; set; } = [];

    /// <summary>0 while the first request is still running inside its transaction.</summary>
    public int ResponseStatus { get; set; }

    public string ResponseBody { get; set; } = "{}";
    public Guid? TransferId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
