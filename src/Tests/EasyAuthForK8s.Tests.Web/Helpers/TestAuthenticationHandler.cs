using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Identity.Web;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Tests.Web.Helpers;

/// <summary>
/// AuthN always succeeds to allow us to test AuthZ scenarios
/// </summary>
internal class TestAuthenticationHandler : IAuthenticationHandler
{
    TestAuthenticationHandlerOptions _options;
    public TestAuthenticationHandler(TestAuthenticationHandlerOptions options = null)
    {
        if (options == null)
            _options = new TestAuthenticationHandlerOptions();
        else
            _options = options;
    }

    public Task<AuthenticateResult> AuthenticateAsync()
    {
        ClaimsIdentity identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, "name", ClaimConstants.Roles);
        identity.AddClaim(new("name", "Jon"));
        identity.AddClaims(_options.Claims);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                CookieAuthenticationDefaults.AuthenticationScheme)));
    }
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public Task ChallengeAsync(AuthenticationProperties? properties) => Task.FromResult(0);
    public Task ForbidAsync(AuthenticationProperties? properties) => Task.FromResult(0);
    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.FromResult(0);
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
}
public class TestAuthenticationHandlerOptions
{
    public List<Claim> Claims { get; set; } = new List<Claim>();
}

