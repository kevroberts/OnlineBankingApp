namespace OnlineBankingAPI.Domain;

/// <summary>A validated, unexecuted transfer. Valid for 5 minutes and usable once.</summary>
public class Quote
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SourceAccountId { get; set; }
    public Guid? DestinationAccountId { get; set; }
    public Guid? PayeeId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when executed; a partial unique index makes a quote single-use.</summary>
    public Guid? TransferId { get; set; }
}
