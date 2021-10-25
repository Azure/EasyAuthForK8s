using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace EasyAuthForK8s.Web.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly MicrosoftIdentityOptions _identityOptions;
        private readonly EasyAuthConfigurationOptions _easyAuthConfig;

        public AuthController(MicrosoftIdentityOptions identityOptions, EasyAuthConfigurationOptions easyAuthConfig)
        {
            _easyAuthConfig = easyAuthConfig;
            _identityOptions = identityOptions;
        }
        private AuthController() { }

        [Route("easyauth/bearertoken")]
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
        [Route("easyauth/login")]
        [Authorize(Policy = "web")]
        public IActionResult Login()
        {
            return Content("The user was logged in successfully and should have been redirected to the backend application");
        }

        [AllowAnonymous]
        [Route("easyauth/index")]
        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login");
            return Content("this page should be handled by your backend application");
        }
        
        //[Authorize(Policy = "web")]
        [AllowAnonymous]
        [Route("easyauth/auth")]
        public async Task<IActionResult> Auth()
        {
            if (!User.Identity.IsAuthenticated)
                return StatusCode(401, "Not Authenticated"); 
            else
            {
                Response.Headers.Add("X-OriginalIdToken", await HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "id_token"));
                AddResponseHeadersFromClaims(User.Claims, Response.Headers);

                return StatusCode(202, User.Identity.Name);
            }
        }

        [AllowAnonymous]
        [Route("easyauth/error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return Content(Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        [AllowAnonymous]
        [Route("easyauth/logout")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Logout()
        {
            var rd = Request.Query[_easyAuthConfig.RedirectParam].ToString();
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