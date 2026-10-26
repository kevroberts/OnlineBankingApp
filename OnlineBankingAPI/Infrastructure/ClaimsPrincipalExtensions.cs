using System.Security.Claims;

namespace OnlineBankingAPI.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public const string SessionIdClaim = "sid";

    /// <summary>The customer id comes only from the session, never from the request.</summary>
    public static Guid CustomerId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No authenticated customer."));

    public static Guid SessionId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(SessionIdClaim)
            ?? throw new InvalidOperationException("No authenticated session."));
}
