using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>A FieldError in the Problem body.</summary>
public record FieldError(string? Field, string Code, string Message);

/// <summary>Thrown by services; turned into an RFC 9457 response by <see cref="ApiProblemExceptionHandler"/>.</summary>
public class ApiProblemException(int status, string code, string detail, IReadOnlyList<FieldError>? errors = null)
    : Exception(detail)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IReadOnlyList<FieldError>? Errors { get; } = errors;
    public Guid? TransferId { get; init; }
    public int? RetryAfterSeconds { get; init; }

    public static ApiProblemException NotFound(string detail = "The resource was not found.") =>
        new(StatusCodes.Status404NotFound, ProblemCodes.NotFound, detail);

    /// <summary>422 with one or more rule codes; a single rule becomes the top-level code.</summary>
    public static ApiProblemException Validation(IReadOnlyList<FieldError> errors) =>
        errors.Count == 1
            ? new(StatusCodes.Status422UnprocessableEntity, errors[0].Code, errors[0].Message, errors)
            : new(StatusCodes.Status422UnprocessableEntity, ProblemCodes.ValidationFailed,
                "The transfer failed one or more checks.", errors);
}

public static class ProblemCodes
{
    public const string BadRequest = "bad_request";
    public const string InvalidDateRange = "invalid_date_range";
    public const string InvalidCursor = "invalid_cursor";
    public const string IdempotencyKeyRequired = "idempotency_key_required";
    public const string Unauthenticated = "unauthenticated";
    public const string InvalidCredentials = "invalid_credentials";
    public const string NotFound = "not_found";
    public const string QuoteAlreadyUsed = "quote_already_used";
    public const string RequestInProgress = "request_in_progress";
    public const string ValidationFailed = "validation_failed";
    public const string AccountLocked = "account_locked";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";

    // Rule codes (FieldError.code)
    public const string AmountNotPositive = "amount_not_positive";
    public const string AmountInvalidPrecision = "amount_invalid_precision";
    public const string DestinationNotFound = "destination_not_found";
    public const string SameAccount = "same_account";
    public const string InsufficientFunds = "insufficient_funds";
    public const string AccountNotActive = "account_not_active";
    public const string NoteInvalid = "note_invalid";
    public const string QuoteExpired = "quote_expired";
    public const string IdempotencyKeyReused = "idempotency_key_reused";

    public static string ForStatus(int status) => status switch
    {
        400 => BadRequest,
        401 => Unauthenticated,
        404 => NotFound,
        422 => ValidationFailed,
        429 => RateLimited,
        >= 500 => InternalError,
        _ => BadRequest,
    };
}

public static class ApiProblem
{
    public const string TypeBase = "https://onlinebanking.example/problems/";

    public static ProblemDetails Create(HttpContext http, int status, string code, string? detail,
        IReadOnlyList<FieldError>? errors = null, Guid? transferId = null)
    {
        var problem = new ProblemDetails
        {
            Type = TypeBase + code,
            Title = Title(status),
            Status = status,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        if (transferId is not null) problem.Extensions["transferId"] = transferId;
        if (errors is { Count: > 0 }) problem.Extensions["errors"] = errors;
        return problem;
    }

    public static ObjectResult Result(HttpContext http, int status, string code, string? detail,
        IReadOnlyList<FieldError>? errors = null) =>
        new(Create(http, status, code, detail, errors))
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };

    public static Task WriteAsync(HttpContext http, int status, string code, string? detail,
        IReadOnlyList<FieldError>? errors = null, Guid? transferId = null)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(Create(http, status, code, detail, errors, transferId),
            (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }

    private static string Title(int status) => status switch
    {
        400 => "Bad request",
        401 => "Unauthenticated",
        404 => "Not found",
        409 => "Conflict",
        422 => "Unprocessable request",
        423 => "Locked",
        429 => "Too many requests",
        >= 500 => "Internal server error",
        _ => "Request failed",
    };
}
