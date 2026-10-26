namespace OnlineBankingAPI.Domain;

public class Transfer
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Guid SourceAccountId { get; set; }
    public Account SourceAccount { get; set; } = null!;
    public Guid? DestinationAccountId { get; set; }
    public Account? DestinationAccount { get; set; }
    public Guid? PayeeId { get; set; }
    public Payee? Payee { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = TransferStatuses.Completed;
    public DateTimeOffset CreatedAt { get; set; }
}

public static class TransferStatuses
{
    public const string Completed = "completed";
}
