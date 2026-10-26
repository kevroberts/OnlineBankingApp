using Microsoft.AspNetCore.Mvc;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Controllers;

[ApiController]
[Route("api/v1/transfers")]
[Produces("application/json")]
public class TransfersController(TransferService transfers) : ControllerBase
{
    /// <summary>Validates a transfer and returns the confirmation summary. Moves no money.</summary>
    [EndpointName("createTransferQuote")]
    [HttpPost("quotes")]
    [ProducesResponseType<TransferQuoteDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateQuote(TransferQuoteRequest request, CancellationToken ct)
    {
        var quote = await transfers.CreateQuoteAsync(User.CustomerId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, quote);
    }

    /// <summary>Executes a confirmed quote exactly once. Requires an Idempotency-Key header (UUID).</summary>
    [EndpointName("executeTransfer")]
    [HttpPost]
    [RequireIdempotencyKey]
    [ProducesResponseType<TransferDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Execute(ExecuteTransferRequest request, CancellationToken ct)
    {
        var outcome = await transfers.ExecuteAsync(User.CustomerId(), RequireIdempotencyKeyAttribute.Get(HttpContext),
            request, ct);

        Response.Headers.Location = Url.Content($"~/api/v1/transfers/{outcome.TransferId}");
        if (outcome.Replayed) Response.Headers["Idempotent-Replayed"] = "true";
        // The body is the stored JSON, so a replay returns exactly what the first request returned.
        return new ContentResult
        {
            StatusCode = outcome.Status,
            ContentType = "application/json; charset=utf-8",
            Content = outcome.BodyJson,
        };
    }

    [EndpointName("listTransfers")]
    [HttpGet]
    [ProducesResponseType<TransferPageDto>(StatusCodes.Status200OK)]
    public Task<TransferPageDto> List([FromQuery] int pageSize = 25, [FromQuery] string? cursor = null,
        CancellationToken ct = default) =>
        transfers.ListAsync(User.CustomerId(), pageSize, cursor, ct);

    [EndpointName("getTransfer")]
    [HttpGet("{transferId:guid}")]
    [ProducesResponseType<TransferDto>(StatusCodes.Status200OK)]
    public async Task<TransferDto> Get(Guid transferId, CancellationToken ct) =>
        await transfers.GetAsync(User.CustomerId(), transferId, ct)
        ?? throw ApiProblemException.NotFound("The transfer was not found.");
}
