namespace OnlineBankingAPI.Contracts;

public record AccountDto(
    Guid Id,
    string Type,
    string Name,
    string MaskedNumber,
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    DateTimeOffset AsOf);

public record AccountListDto(IReadOnlyList<AccountDto> Items, decimal TotalBalance);

public record PageInfo(int PageSize, string? NextCursor, bool HasMore);

public record TransactionDto(
    Guid Id,
    DateTimeOffset PostedAt,
    string Description,
    string? Counterparty,
    string Type,
    decimal Amount,
    decimal SignedAmount,
    decimal RunningBalance,
    string Category,
    string? Note,
    Guid? TransferId);

public record TransactionPageDto(Guid AccountId, IReadOnlyList<TransactionDto> Items, PageInfo Page, int TotalCount);

public record PayeeDto(Guid Id, string Nickname, string Name, string BankName, string MaskedAccountNumber,
    string? Category);

public record PayeeListDto(IReadOnlyList<PayeeDto> Items);
