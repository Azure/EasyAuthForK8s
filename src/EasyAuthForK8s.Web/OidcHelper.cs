using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Web
{
    public class OidcHelper
    {
        /// <summary>
        /// Modifies the OIDC message to add additional options
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Task HandleRedirectToIdentityProvider(RedirectContext context, EasyAuthConfigurationOptions configOptions, MicrosoftIdentityOptions aadOptions)
        {
            //configure the path where the user should be redirected after successful sign in
            //this should be provided by the ingress controller as the path they were originally
            //attempting to access before the auth challenge
            if (context.HttpContext.Request.Query.ContainsKey(configOptions.RedirectParam))
                context.Properties.Items[".redirect"] = context.HttpContext.Request.Query[configOptions.RedirectParam].ToString();
            else
                context.Properties.Items[".redirect"] = configOptions.DefaultRedirectAfterSignin;

            //if additional scopes are requested, add them to the redirect
            if (context.HttpContext.Request.Query.ContainsKey(Constants.ScopeParameterName))
                context.ProtocolMessage.Scope = BuildScopeString(context.ProtocolMessage.Scope, context.HttpContext.Request.Query[Constants.ScopeParameterName]);

            //This simplifies the user sign in by providing the the domain for home realm discovery
            //this is helpful when the user has multiple AAD accounts
            context.ProtocolMessage.DomainHint = aadOptions.Domain;

            return Task.CompletedTask;
        }

        public static async Task HandleRemoteFailure(RemoteFailureContext context)
        {
            //TODO switch to compiled razor view for this
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><title>Authentication Error</title></head><body>");
            sb.AppendLine("<h2>We're Trying to sign you in, but an error occured.</h2><br>");
            if(context.Failure.Data.Contains("error_description"))
                sb.AppendLine(context.Failure.Data["error_description"] as string);
            sb.AppendLine("</body></html>");
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(sb.ToString());
            context.HandleResponse();
        }
        private static string BuildScopeString(string baseScope, StringValues additionalScopes)
        {
            return string.Join(' ', (baseScope ?? string.Empty)
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Union(additionalScopes));
               
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

