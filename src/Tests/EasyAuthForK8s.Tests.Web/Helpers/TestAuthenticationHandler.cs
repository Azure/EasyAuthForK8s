using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Identity.Web;

namespace EasyAuthForK8s.Tests.Web.Helpers;

/// <summary>
/// AuthN always succeeds to allow us to test AuthZ scenarios
/// </summary>
internal class TestAuthenticationHandler : IAuthenticationHandler
{
    TestAuthenticationHandlerOptions _options;
    public TestAuthenticationHandler(TestAuthenticationHandlerOptions options = null)
    {
        if(options == null)
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
internal class TestAuthenticationHandlerOptions
{
    public List<Claim> Claims { get; set; } = new List<Claim>();
}

