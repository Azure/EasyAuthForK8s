using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;

namespace OCP.Msal.Proxy.Web.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly MicrosoftIdentityOptions _identityOptions;
        private readonly IConfiguration _configuration;
        private string redirectParam { get { return this._configuration["RedirectParam"]; }}

        public AuthController(MicrosoftIdentityOptions options, IConfiguration configuration)
        {
            _configuration = configuration;
            _identityOptions = options;
        }
        private AuthController() { }

        [Route("msal/bearertoken")]
        [Authorize(Policy = "api")]
        [AllowAnonymous]
        public async Task<IActionResult> ApiAuth()
        {
            if (!User.Identity.IsAuthenticated) return Unauthorized(new Models.ApiUnauthorizedMessageModel
            {
                tokenAuthorityMetadata = $"{_identityOptions.Instance}{_identityOptions.TenantId}/v2.0/.well-known/openid-configuration",
                scope = $"k8seasyauth://{_identityOptions.ClientId}/.default"
            });

            Response.Headers.Add("X-OriginalBearerToken", await HttpContext.GetTokenAsync(JwtBearerDefaults.AuthenticationScheme, "access_token"));
            AddResponseHeadersFromClaims(User.Claims, Response.Headers);

            return StatusCode(202, User.Identity.Name);
        }
        [Route("msal/login")]
        [Authorize(Policy = "web")]
        public IActionResult Login()
        {
            return Content("The user was logged in successfully and should have been redirected to the backend application");
            //var rd = "/";
            //if (Request.Query.ContainsKey(redirectParam) && !string.IsNullOrEmpty(Request.Query[redirectParam].ToString()))
            //    rd = Request.Query[redirectParam].ToString();
            //return Redirect(rd);

            //OAuthChallengeProperties challengeProperties = new OAuthChallengeProperties();
            //if (!string.IsNullOrWhiteSpace(_identityOptions.SignUpSignInPolicyId))
            //    challengeProperties.Items.Add("policy", _identityOptions.SignUpSignInPolicyId);
            //if (!string.IsNullOrWhiteSpace(_identityOptions.Domain))
            //    challengeProperties.Parameters.Add("domain_hint", _identityOptions.Domain);
            //challengeProperties.RedirectUri = rd;
            //return Challenge(challengeProperties, OpenIdConnectDefaults.AuthenticationScheme);
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
        public async Task<IActionResult> Auth()
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized("Not Authenticated");
            else
            {
                Response.Headers.Add("X-OriginalIdToken", await HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "id_token"));
                AddResponseHeadersFromClaims(User.Claims, Response.Headers);

                return StatusCode(202, User.Identity.Name);
            }
        }
        [AllowAnonymous]
        [Route("/")]
        public IActionResult HealthProbe()
        {
            //TODO
            return StatusCode(200, "Healthy");
        }
        [AllowAnonymous]
        [Route("msal/debug")]
        public IActionResult Debug()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach(var header in Request.Headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }
            return StatusCode(200, sb.ToString());
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
        public IActionResult Logout()
        {
            var rd = Request.Query[redirectParam].ToString();
            return SignOut(new AuthenticationProperties() { RedirectUri = rd }, 
                CookieAuthenticationDefaults.AuthenticationScheme, 
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        internal static void AddResponseHeadersFromClaims(IEnumerable<Claim> claims, IHeaderDictionary headers)
        {
            if (claims == null || headers == null) return;

            foreach (var claim in claims)
            {
                if (IsValidHeaderValue(claim.Value))
                {
                    var claimName = claim.Type;

                    if (claimName.Contains("/"))
                        claimName = claimName.Split('/')[^1];

                    var name = $"X-Injected-{claimName}";

                    if (!headers.ContainsKey(name))
                        headers.Add(name, claim.Value);
                    else
                        headers[name] += $", {claim.Value}";
                }
            }
        }
        internal static bool IsValidHeaderValue(string value)
        {
            return value.All(c => c >= 32 && c < 127);
        }
    }
}