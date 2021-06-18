using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

        [Route("api/auth")]
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
        [AllowAnonymous]
        public IActionResult Login()
        {
            var rd = "/";
            if (Request.Query.ContainsKey(redirectParam) && !string.IsNullOrEmpty(Request.Query[redirectParam].ToString()))
                rd = Request.Query[redirectParam].ToString();
            return Redirect($"/MicrosoftIdentity/Account/SignIn/{OpenIdConnectDefaults.AuthenticationScheme}?redirectUri={HttpUtility.UrlEncode(rd)}");
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
            if (!User.Identity.IsAuthenticated) return Unauthorized("Not Authenticated");
            
            Response.Headers.Add("X-OriginalIdToken", await HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "id_token"));
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
        public IActionResult Logout()
        {           
            return Redirect("/MicrosoftIdentity/Account/SignOut");
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