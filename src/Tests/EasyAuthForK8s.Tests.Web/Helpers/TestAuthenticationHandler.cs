using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Tests.Web.Helpers;

/// <summary>
/// AuthN always succeeds to allow us to test AuthZ
/// </summary>
public class TestAuthenticationHandler : IAuthenticationHandler
{
    public Task<AuthenticateResult> AuthenticateAsync()
    {
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("Cookies")),
                new AuthenticationProperties(),
                "Cookies")));
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
    
