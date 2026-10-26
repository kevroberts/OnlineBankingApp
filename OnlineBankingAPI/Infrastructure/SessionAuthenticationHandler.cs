using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Authenticates the obk_session cookie or the same token sent as a bearer token.</summary>
public class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SessionService sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = SessionService.ReadToken(Request);
        if (token is null) return AuthenticateResult.NoResult();

        var session = await sessions.ValidateAsync(token, Context.RequestAborted);
        if (session is null) return AuthenticateResult.Fail("Session is missing, expired or signed out.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, session.CustomerId.ToString()),
            new Claim(ClaimTypes.Name, session.Customer.Username),
            new Claim(ClaimsPrincipalExtensions.SessionIdClaim, session.Id.ToString()),
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(Context, StatusCodes.Status401Unauthorized, ProblemCodes.Unauthenticated,
            "Sign in to continue.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(Context, StatusCodes.Status404NotFound, ProblemCodes.NotFound,
            "The resource was not found.");
}
