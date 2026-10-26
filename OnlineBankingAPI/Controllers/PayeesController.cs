using Microsoft.AspNetCore.Mvc;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Controllers;

[ApiController]
[Route("api/v1/payees")]
[Produces("application/json")]
public class PayeesController(PayeeService payees) : ControllerBase
{
    [EndpointName("listPayees")]
    [HttpGet]
    [ProducesResponseType<PayeeListDto>(StatusCodes.Status200OK)]
    public Task<PayeeListDto> List(CancellationToken ct) => payees.GetPayeesAsync(User.CustomerId(), ct);
}
