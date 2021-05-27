using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace OCP.Msal.Proxy.Web.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AzureADOptions _adOptions;
        private readonly IConfiguration _configuration;
        private string redirectParam { get { return this._configuration["RedirectParam"]; }}

        public AuthController(AzureADOptions options, IConfiguration configuration)
        {
            _configuration = configuration;
            _adOptions = options;
        }
        private AuthController() { }

        [Route("api/auth")]
        [Authorize(Policy = "api")]
        [AllowAnonymous]
        public IActionResult ApiAuth()
        {
            if (!User.Identity.IsAuthenticated) return StatusCode(401, new Models.ApiUnauthorizedMessageModel
            {
                tokenAuthorityMetadata = $"{_adOptions.Instance}{_adOptions.TenantId}/v2.0/.well-known/openid-configuration",
                scope = $"k8seasyauth://{_adOptions.ClientId}/.default"
            });

            AddResponseHeadersFromClaims(User.Claims, Response.Headers);

            return StatusCode(202, User.Identity.Name);
        }
        [Route("msal/login")]
        [Authorize(Policy = "web")]
        public IActionResult Login()
        {
            var rd = "/";
            if (Request.Query.ContainsKey(redirectParam) && !string.IsNullOrEmpty(Request.Query[redirectParam].ToString()))
                rd = Request.Query[redirectParam].ToString();
            return Redirect(rd);
        }

        [AllowAnonymous]
        [Route("msal/index")]
        public IActionResult Index()
        {
            if (Convert.ToBoolean(_configuration["ShowLogin"]))
            {
                var rd = Request.Query[redirectParam].ToString();
                var content = $"<html><body><a href='/msal/login?{redirectParam}={rd}'>Login</a></body></html>";
                return new ContentResult
                {
                    Content = content,
                    ContentType = "text/html"
                };
            }
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login");
            return Content("this page should be handled by your backend application");
        }
        
        [Authorize(Policy = "web")]
        [AllowAnonymous]
        [Route("msal/auth")]
        public IActionResult Auth()
        {
            if (!User.Identity.IsAuthenticated) return StatusCode(401, "Not Authenticated");
            AddResponseHeadersFromClaims(User.Claims, Response.Headers);

            return StatusCode(202, User.Identity.Name);
        }


        [AllowAnonymous]
        [Route("msal/error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return Content(Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        [AllowAnonymous]
        [Route("msal/logout")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> CookieLogout()
        {
            await HttpContext.SignOutAsync(AzureADDefaults.CookieScheme);
            return Redirect("/msal/auth");
        }

        internal static void AddResponseHeadersFromClaims(IEnumerable<Claim> claims, IHeaderDictionary headers)
        {
            if (claims == null || headers == null) return;

            foreach (var claim in claims)
            {
                var claimName = claim.Type;
                if (claimName.Contains("/")) claimName = claimName.Split('/')[claimName.Split('/').Length - 1];
                var name = $"X-Injected-{claimName}";
                if (!headers.ContainsKey(name) && IsValidHeaderValue(claim.Value)) headers.Add(name, claim.Value);
            }
        }

        internal static bool IsValidHeaderValue(string value)
        {
            return value.All(c => c >= 32 && c < 127);
        }
    }
}