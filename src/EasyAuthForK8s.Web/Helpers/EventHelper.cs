using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;

namespace EasyAuthForK8s.Web.Helpers
{
    internal class EventHelper
    {
        /// <summary>
        /// Modifies the OIDC message to add additional options
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Task HandleRedirectToIdentityProvider(RedirectContext context, 
            EasyAuthConfigurationOptions configOptions, 
            MicrosoftIdentityOptions aadOptions,
            ILogger logger)
        {
            logger.LogInformation($"Redirecting sign-in to endpoint {context.ProtocolMessage.IssuerAddress}");
            
            //configure the path where the user should be redirected after successful sign in
            //this should be provided by the ingress controller as the path they were originally
            //attempting to access before the auth challenge.  We probably could store the 
            //path of the original request in the state object and use that instead
            if (context.HttpContext.Request.Query.ContainsKey(Constants.RedirectParameterName))
                context.Properties.RedirectUri = context.HttpContext.Request.Query[Constants.RedirectParameterName].ToString();
            else
                context.Properties.RedirectUri = configOptions.DefaultRedirectAfterSignin;

            //if additional scopes are requested, add them to the redirect
            var state = context.HttpContext.EasyAuthStateFromHttpContext();
            context.ProtocolMessage.Scope = BuildScopeString(context.ProtocolMessage.Scope, state.Scopes);

            //This simplifies the user sign in by providing the the domain for home realm discovery
            //this is helpful when the user has multiple AAD accounts
            context.ProtocolMessage.DomainHint = aadOptions.Domain;

            //add the graph queries to the oidc message state so that they can be run after successful login
            context.Properties.Items.Add(Constants.OidcGraphQueryStateBag, string.Join('|', state.GraphQueries));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles scenarios where AAD sends back error information
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static async Task HandleRemoteFailure(RemoteFailureContext context, ILogger logger)
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

        /// <summary>
        /// Modifies the claims and properties before the authentication ticket is created 
        /// and written to the auth cookie
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configOptions"></param>
        /// <returns></returns>
        public static async Task CookieSigningIn(CookieSigningInContext context, EasyAuthConfigurationOptions configOptions)
        {
            /* 
             * after the initial sign in and claims extraction, we only really need
             * a valid authentication ticket and claims to support our authorization
             * requirements. From there we can strip everything down to keep the cookie as
             * small as possible, while adding back anything needed by the backend service
            */
            var remIdentity = context.Principal.Identity as ClaimsIdentity;
            var identities = context.Principal.Identities as List<ClaimsIdentity>;
            identities.Clear();

            //remove unneeded claims and re-map to save a few bytes in the cookie
            var claimsToKeep = new List<Claim>();
            Action<Claim, string> addClaim = (claim, name) =>
            {
                claimsToKeep.Add(new Claim(name, claim.Value, "", "", ""));
            };

            foreach (var claim in remIdentity.Claims)
            {
                if (claim.Type == remIdentity.RoleClaimType)
                    addClaim(claim, Constants.Claims.Role);

                else if (claim.Type == remIdentity.NameClaimType)
                    addClaim(claim, Constants.Claims.Name);

                else if (claim.Type == ClaimTypes.NameIdentifier)
                    addClaim(claim, Constants.Claims.Name);
            }

            claimsToKeep.AddRange(remIdentity.Claims
                .Where(x => x.Type == remIdentity.RoleClaimType)
                .Select(c => new Claim(Constants.Claims.Role, c.Value)));

            claimsToKeep.AddRange(remIdentity.Claims
                .Where(x => x.Type == remIdentity.NameClaimType)
                .Select(c => new Claim(Constants.Claims.Name, c.Value)));

            claimsToKeep.AddRange(remIdentity.Claims
                .Where(x => x.Type == ClaimTypes.NameIdentifier)
                .Select(c => new Claim(Constants.Claims.Subject, c.Value)));


            var userInfo = new UserInfoPayload();

            if(context.Properties.Items.ContainsKey(Constants.OidcGraphQueryStateBag))
            {
                var queries = context.Properties.Items[Constants.OidcGraphQueryStateBag].Split('|', StringSplitOptions.RemoveEmptyEntries);

                userInfo.graph = await GraphHelper.ExecuteQueryAsync(configOptions.GraphEndpoint, context.Properties.GetTokenValue("access_token"), queries);
            }

            JwtSecurityToken jwtSecurityToken = new JwtSecurityToken(context.Properties.GetTokenValue("id_token"));
            userInfo.PopulateFromClaims(jwtSecurityToken.Claims);
            claimsToKeep.Add(userInfo.ToPayloadClaim(configOptions));

            identities.Add(new ClaimsIdentity(claimsToKeep, remIdentity.AuthenticationType, Constants.Claims.Subject, Constants.Claims.Role));

            //at this point we are done with properties, so dump the item collection keeping only the expiry
            var expiresUtc = context.Properties.ExpiresUtc;
            context.Properties.Items.Clear();
            context.Properties.ExpiresUtc = expiresUtc;

        }
        private static string BuildScopeString(string baseScope, IList<string> additionalScopes)
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

        internal static void CookieValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            ClaimsIdentity id = new ClaimsIdentity(principal.Claims, principal.Identity.AuthenticationType, "name", "role");
            //principal.clo
        }
    }
}

