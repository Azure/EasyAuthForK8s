using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace EasyAuthForK8s.Web
{
    public static class EasyAuthStateExtensions
    {
        public static EasyAuthState EasyAuthStateFromHttpContext(this HttpContext context)
        {
            //see if state exists in property bag, and return it
            if (context.Items.ContainsKey(Constants.StateCookieName))
                return context.Items[Constants.StateCookieName] as EasyAuthState;

            //see if cookie exists, read, delete cookie, and save to property bag
            else
            {
                EasyAuthState easyAuthState = new();

                if (context.Request.Cookies.ContainsKey(Constants.StateCookieName))
                {
                    var encodedString = context.Request.Cookies[Constants.StateCookieName];
                    if (encodedString != null)
                    {
                        var dp = context.RequestServices.GetDataProtector(Constants.StateCookieName);

                        easyAuthState = JsonSerializer.Deserialize<EasyAuthState>(dp.Unprotect(encodedString));
                    }
                    //remove the cookie, since this is a one-time use in the current request
                    context.Response.Cookies.Delete(Constants.StateCookieName);
                }

                context.Items.Add(Constants.StateCookieName, easyAuthState);
                return easyAuthState;
            }
        }
        public static void AddCookieToResponse(this EasyAuthState state, HttpContext httpContext)
        {
            var dp = httpContext.RequestServices.GetDataProtector(Constants.StateCookieName);
            var cookieValue = dp.Protect(state.ToJsonString());

            var cookieBuilder = new RequestPathBaseCookieBuilder
            {
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
                SecurePolicy = CookieSecurePolicy.Always,
                IsEssential = true,
                Expiration = TimeSpan.FromMinutes(Constants.StateTtlMinutes)
            };

            var options = cookieBuilder.Build(httpContext, DateTimeOffset.Now);

            httpContext.Response.Cookies.Append(Constants.StateCookieName, cookieValue, options);
        }
        public static string ToJsonString(this EasyAuthState state)
        {
            return JsonSerializer.Serialize<EasyAuthState>(state);
        }
    }
}
