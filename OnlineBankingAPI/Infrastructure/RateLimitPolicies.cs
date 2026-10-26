using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Infrastructure;

public static class RateLimitPolicies
{
    public const string Login = "login";

    /// <summary>10 logins per minute per IP; 60 requests per minute per session (token bucket).</summary>
    public static IServiceCollection AddBankRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.AddPolicy(Login, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Options(http).LoginsPerMinutePerIp,
                    Window = TimeSpan.FromMinutes(1),
                }));

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                SessionService.ReadToken(http.Request) is { } token
                    ? RateLimitPartition.GetTokenBucketLimiter(Convert.ToHexString(SessionService.Hash(token)),
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = Options(http).RequestsPerMinutePerSession,
                            TokensPerPeriod = Options(http).RequestsPerMinutePerSession,
                            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        })
                    : RateLimitPartition.GetNoLimiter("anonymous"));

            o.OnRejected = (context, ct) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after)
                    ? (int)Math.Ceiling(after.TotalSeconds)
                    : 60;
                context.HttpContext.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
                return new ValueTask(ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    ProblemCodes.RateLimited, "Too many requests. Slow down and try again."));
            };
        });

    private static BankOptions Options(HttpContext http) =>
        http.RequestServices.GetRequiredService<IOptions<BankOptions>>().Value;
}
