using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OnlineBankingAPI.Assistant;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController(AuthService auth, AssistantService assistant) : ControllerBase
{
    [EndpointName("login")]
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<LoginResponse> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await auth.LoginAsync(request, Request.Headers.UserAgent.ToString(), ct);
        Response.Cookies.Append(SessionService.CookieName, response.SessionToken, SessionService.CookieOptions());
        return response;
    }

    /// <summary>Revokes the session and expires the cookie. Safe to repeat.</summary>
    [EndpointName("logout")]
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // Sign-out also drops the session's assistant chats. Logout is anonymous, so the session may be gone already.
        if (User.Identity?.IsAuthenticated == true) assistant.EndSession(User.SessionId());
        await auth.LogoutAsync(SessionService.ReadToken(Request), ct);
        Response.Cookies.Delete(SessionService.CookieName, SessionService.CookieOptions());
        return NoContent();
    }

    [EndpointName("getCurrentCustomer")]
    [HttpGet("me")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    public Task<CustomerDto> Me(CancellationToken ct) => auth.GetCurrentCustomerAsync(User.CustomerId(), ct);
}
