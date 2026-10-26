namespace OnlineBankingAPI.Domain;

/// <summary>An append-only ledger line. BalanceAfter is the account balance immediately after posting.</summary>
public class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public long Sequence { get; set; }
    public Guid? TransferId { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public char Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = "";
    public string? Counterparty { get; set; }
    public string Category { get; set; } = "";
    public string? Note { get; set; }

    public decimal SignedAmount => Direction == Directions.Debit ? -Amount : Amount;
}

public static class Directions
{
    public const char Debit = 'D';
    public const char Credit = 'C';
}

public static class Categories
{
    public const string Transfer = "transfer";
    public const string Payment = "payment";
    public const string Deposit = "deposit";
    public const string Card = "card";
    public const string Fee = "fee";
    public const string Interest = "interest";
}
