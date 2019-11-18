using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace OCP.Msal.Proxy.Web.Controllers
{
    [Authorize(Policy = "web")]
    public class MsalController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly string redirectParam;
        public MsalController(IConfiguration configuration)
        {
            this.configuration = configuration;
            this.redirectParam = this.configuration["RedirectParam"];
        }

        public IActionResult Login()
        {
            var rd = "/";
            if (Request.Query.ContainsKey(redirectParam) && !string.IsNullOrEmpty(Request.Query[redirectParam].ToString()))
                rd = Request.Query[redirectParam].ToString();
            return Redirect(rd);
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (Convert.ToBoolean(configuration["ShowLogin"]))
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

        [AllowAnonymous]
        public IActionResult Auth()
        {
            if (!User.Identity.IsAuthenticated) return StatusCode(401, "Not Authenticated");

            foreach (var claim in User.Claims)
            {
                var claimName = claim.Type;
                if (claimName.Contains("/")) claimName = claimName.Split('/')[claimName.Split('/').Length - 1];
                var name = $"X-Injected-{claimName}";
                if (!Response.Headers.ContainsKey(name)) Response.Headers.Add(name, claim.Value);
            }

            return StatusCode(202, User.Identity.Name);

        }


        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return Content(Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }
    }
}