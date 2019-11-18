using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace OCP.Msal.Proxy.Web.Controllers
{
    [ApiController]
    [Authorize(Policy = "api")]
    public class ApiAuthController : ControllerBase
    {
        AzureADOptions _options;
        public ApiAuthController(AzureADOptions options)
        {
            _options = options;
        }
        private ApiAuthController() { }

        [AllowAnonymous]
        [Route("api/auth")]
        public IActionResult Auth()
        {
            if (!User.Identity.IsAuthenticated) return StatusCode(401, new Models.ApiUnauthorizedMessageModel
            {
                tokenAuthorityMetadata = $"{_options.Instance}{_options.TenantId}/v2.0/.well-known/openid-configuration",
                scope = $"k8seasyauth://{_options.ClientId}/.default"
            });

            foreach (var claim in User.Claims)
            {
                var claimName = claim.Type;
                if (claimName.Contains("/")) claimName = claimName.Split('/')[claimName.Split('/').Length - 1];
                var name = $"X-Injected-{claimName}";
                if (!Response.Headers.ContainsKey(name)) Response.Headers.Add(name, claim.Value);
            }

            return StatusCode(202, User.Identity.Name);

        }
    }
}