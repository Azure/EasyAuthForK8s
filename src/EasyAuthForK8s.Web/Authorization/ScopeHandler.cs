using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Web.Authorization;

internal class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (requirement is null)
        {
            throw new ArgumentNullException(nameof(requirement));
        }


        // Can't determine what to do without scope metadata, so proceed
        if (requirement.AllowedValues is null)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var scopeClaim = context.User.FindFirst(ClaimConstants.Scp);

        if (scopeClaim is null)
        {
            return Task.CompletedTask;
        }

        if (scopeClaim != null && scopeClaim.Value.Split(' ').Intersect(requirement.AllowedValues).Any())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}

