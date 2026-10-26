using Microsoft.AspNetCore.Mvc.Filters;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Rejects a request without a UUID Idempotency-Key header (400 idempotency_key_required).</summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequireIdempotencyKeyAttribute : Attribute, IResourceFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const string ItemKey = "obk.idempotency-key";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var header = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!Guid.TryParse(header, out var key))
            throw new ApiProblemException(StatusCodes.Status400BadRequest, ProblemCodes.IdempotencyKeyRequired,
                $"Send a UUID in the {HeaderName} header.");
        context.HttpContext.Items[ItemKey] = key;
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }

    public static Guid Get(HttpContext http) => (Guid)http.Items[ItemKey]!;
}
