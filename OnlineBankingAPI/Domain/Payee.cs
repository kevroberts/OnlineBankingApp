namespace OnlineBankingAPI.Domain;

public class Payee
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Nickname { get; set; } = "";
    public string Name { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string RoutingNumber { get; set; } = "";
    public string? Category { get; set; }

    public string MaskedAccountNumber => Masking.Mask(AccountNumber);
}
