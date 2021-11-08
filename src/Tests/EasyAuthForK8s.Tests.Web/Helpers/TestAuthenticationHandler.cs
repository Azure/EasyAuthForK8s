using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Tests.Web.Helpers;

/// <summary>
/// AuthN always succeeds to allow us to test AuthZ scenarios
/// </summary>
internal class TestAuthenticationHandler : IAuthenticationHandler
{
    public Task<AuthenticateResult> AuthenticateAsync()
    {
        ClaimsIdentity identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, "name", null);
        identity.AddClaim(new("name", "Jon"));

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    public Task ChallengeAsync(AuthenticationProperties? properties)
    {
        return Task.FromResult(0);
    }

    public Task ForbidAsync(AuthenticationProperties? properties)
    {
        return Task.FromResult(0);
    }

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
    {
        return Task.FromResult(0);
    }
}

