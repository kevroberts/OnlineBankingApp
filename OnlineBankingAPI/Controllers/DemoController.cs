using Microsoft.AspNetCore.Mvc;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Infrastructure;

namespace OnlineBankingAPI.Controllers;

/// <summary>Resets the seeded data for live demos. Only exists in the Demo environment.</summary>
[ApiController]
[Route("api/v1/demo")]
[ApiExplorerSettings(IgnoreApi = true)]
public class DemoController(DbSeeder seeder, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        if (!env.IsEnvironment("Demo")) throw ApiProblemException.NotFound();
        await seeder.ResetAsync(ct);
        return NoContent();
    }
}
