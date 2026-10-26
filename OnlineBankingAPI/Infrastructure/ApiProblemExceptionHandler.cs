using Microsoft.AspNetCore.Diagnostics;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Maps <see cref="ApiProblemException"/> to Problem Details; anything else becomes 500 internal_error.</summary>
public class ApiProblemExceptionHandler(ILogger<ApiProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is ApiProblemException p)
        {
            if (p.RetryAfterSeconds is int retry) http.Response.Headers.RetryAfter = retry.ToString();
            await ApiProblem.WriteAsync(http, p.Status, p.Code, p.Message, p.Errors, p.TransferId);
            return true;
        }

        if (exception is BadHttpRequestException bad)
        {
            await ApiProblem.WriteAsync(http, StatusCodes.Status400BadRequest, ProblemCodes.BadRequest, bad.Message);
            return true;
        }

        logger.LogError(exception, "Unhandled exception");
        await ApiProblem.WriteAsync(http, StatusCodes.Status500InternalServerError, ProblemCodes.InternalError,
            "An unexpected error occurred.");
        return true;
    }
}
