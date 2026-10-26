using Microsoft.AspNetCore.Mvc;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Produces("application/json")]
public class AccountsController(AccountService accounts) : ControllerBase
{
    [EndpointName("listAccounts")]
    [HttpGet]
    [ProducesResponseType<AccountListDto>(StatusCodes.Status200OK)]
    public Task<AccountListDto> List(CancellationToken ct) =>
        accounts.GetAccountsAsync(User.CustomerId(), ct);

    [EndpointName("getAccount")]
    [HttpGet("{accountId:guid}")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> Get(Guid accountId, CancellationToken ct)
    {
        var (account, etag) = await accounts.GetAccountAsync(User.CustomerId(), accountId, ct);
        Response.Headers.ETag = etag;

        var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
        if (ifNoneMatch == "*" || ifNoneMatch.Split(',').Select(s => s.Trim()).Contains(etag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(account);
    }

    [EndpointName("listTransactions")]
    [HttpGet("{accountId:guid}/transactions")]
    [ProducesResponseType<TransactionPageDto>(StatusCodes.Status200OK)]
    public Task<TransactionPageDto> ListTransactions(
        Guid accountId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? type,
        [FromQuery] string? q,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default) =>
        accounts.GetHistoryAsync(User.CustomerId(), accountId, from, to, type, q, pageSize, cursor, ct);

    [EndpointName("getTransaction")]
    [HttpGet("{accountId:guid}/transactions/{transactionId:guid}")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    public Task<TransactionDto> GetTransaction(Guid accountId, Guid transactionId, CancellationToken ct) =>
        accounts.GetTransactionAsync(User.CustomerId(), accountId, transactionId, ct);
}
