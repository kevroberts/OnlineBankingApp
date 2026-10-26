using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OnlineBankingAPI.Contracts;

public record DestinationDto(
    [Required, RegularExpression("^(account|payee)$")] string Kind,
    [Required] Guid? Id);

public record TransferQuoteRequest(
    [Required] Guid? SourceAccountId,
    [Required] DestinationDto? Destination,
    decimal? Amount,
    string? Note);

public record ExecuteTransferRequest([Required] Guid? QuoteId);

public record QuoteSourceDto(Guid Id, string Name, string MaskedNumber, decimal Balance);

public record DestinationRefDto(string Kind, Guid Id, string Name, string MaskedNumber);

public record TransferQuoteDto(
    Guid QuoteId,
    QuoteSourceDto Source,
    DestinationRefDto Destination,
    decimal Amount,
    decimal Fee,
    string? Note,
    decimal SourceBalanceAfter,
    DateTimeOffset ExpiresAt);

public record TransferSourceDto(Guid Id, string Name, string MaskedNumber, decimal BalanceAfter);

public record TransferDestinationDto(
    string Kind,
    Guid Id,
    string Name,
    string MaskedNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? BalanceAfter);

public record TransferDto(
    Guid Id,
    string Reference,
    string Status,
    TransferSourceDto Source,
    TransferDestinationDto Destination,
    decimal Amount,
    string? Note,
    Guid DebitTransactionId,
    Guid CreditTransactionId,
    DateTimeOffset CreatedAt);

public record TransferPageDto(IReadOnlyList<TransferDto> Items, PageInfo Page);
