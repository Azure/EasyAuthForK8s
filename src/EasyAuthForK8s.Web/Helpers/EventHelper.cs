using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAuthForK8s.Web.Helpers
{
    internal class EventHelper
    {
        private readonly EasyAuthConfigurationOptions _configOptions;
        public EventHelper(EasyAuthConfigurationOptions configOptions)
        {
            _configOptions = configOptions ?? throw new ArgumentNullException(nameof(configOptions));
        }
        private static ILogger _logger;
        /// <summary>
        /// Modifies the OIDC message to add additional options
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task HandleRedirectToIdentityProvider(
            RedirectContext context,
            Func<RedirectContext, Task> next)
        {
            EnsureLogger(context.HttpContext);
            _logger!.LogInformation($"Redirecting sign-in to endpoint {context.ProtocolMessage.IssuerAddress}");

            //if additional scopes are requested, add them to the redirect
            EasyAuthState state = context.HttpContext.EasyAuthStateFromHttpContext();
            
            // there are three ways to determine where to send the user after successful signin
            // in order of precendence:
            //  1. if the rd parameter was supplied when they were sent the challenge
            //  2. The url extracted from the nginx header in the authreq (ie the url they were attempting to access)
            //  3. Fall back to a predetermined path.  Default is the root "/"
            if (context.Properties.RedirectUri == context.Options.CallbackPath)
                context.Properties.RedirectUri = state.Url ?? _configOptions.DefaultRedirectAfterSignin;

            context.ProtocolMessage.Scope = BuildScopeString(context.ProtocolMessage.Scope, state.Scopes);

            //add the graph queries to the oidc message state so that they can be run after successful login
            context.Properties.Items.Add(Constants.OidcGraphQueryStateBag, string.Join('|', state.GraphQueries));

            await next(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles scenarios where AAD sends back error information
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task HandleRemoteFailure(RemoteFailureContext context, Func<RemoteFailureContext, Task> next)
        {
            EnsureLogger(context.HttpContext);
            _logger!.LogWarning("A remote error was return during signin: {message}", context.Failure.Message);

            //TODO switch to compiled razor view for this
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<html><head><title>Authentication Error</title></head><body>");
            sb.AppendLine("<h2>We're Trying to sign you in, but an error occured.</h2><br>");
            if (context.Failure.Data.Contains("error_description"))
            {
                sb.AppendLine(context.Failure.Data["error_description"] as string);
            }

            sb.AppendLine("</body></html>");
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(sb.ToString());
            context.HandleResponse();

            await next(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Modifies the claims and properties before the authentication ticket is created 
        /// and written to the auth cookie
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configOptions"></param>
        /// <returns></returns>
        public async Task CookieSigningIn(CookieSigningInContext context, 
            Func<CookieSigningInContext, Task> next)
        {
            /* 
             * after the initial sign in and claims extraction, we only really need
             * a valid authentication ticket and claims to support our authorization
             * requirements. From there we can strip everything down to keep the cookie as
             * small as possible, while adding back anything needed by the backend service
            */
            EnsureLogger(context.HttpContext);

            ClaimsIdentity remIdentity = context.Principal.Identity as ClaimsIdentity;
            List<ClaimsIdentity> identities = context.Principal.Identities as List<ClaimsIdentity>;
            identities.Clear();

            //remove unneeded claims and re-map to save a few bytes in the cookie
            List<Claim> claimsToKeep = new List<Claim>();
            Action<Claim, string> addClaim = (claim, name) =>
            {
                claimsToKeep.Add(new Claim(name, claim.Value, "", "", ""));
            };

            foreach (Claim claim in remIdentity.Claims)
            {
                if (claim.Type == remIdentity.RoleClaimType)
                {
                    addClaim(claim, Constants.Claims.Role);
                }
                else if (claim.Type == remIdentity.NameClaimType)
                {
                    addClaim(claim, Constants.Claims.Name);
                }
                else if (claim.Type == ClaimTypes.NameIdentifier)
                {
                    addClaim(claim, Constants.Claims.Subject);
                }
            }

            string access_token = context.Properties.GetTokenValue("access_token");

            if (string.IsNullOrEmpty(access_token))
            {
                throw new InvalidOperationException("access_token is missing from authentication properties.  Ensure that SaveTokens option is 'true'.");
            }

            JwtSecurityToken accessToken = new JwtSecurityToken(access_token);
            
            //for whatever reason, id_tokens do not contain scp claims, but
            //the OIDC handler extracts claims from the id_token, and 
            //discards the scope from the token response, so we are left 
            //with peeking inside the access_token to get the scopes.
            foreach (Claim claim in accessToken.Claims)
            {
                if (claim.Type == ClaimConstants.Scp)
                {
                    addClaim(claim, ClaimConstants.Scp);
                }
            }

            UserInfoPayload userInfo = new UserInfoPayload();

            if (context.Properties.Items.ContainsKey(Constants.OidcGraphQueryStateBag))
            {
                string[] queries = context.Properties.Items[Constants.OidcGraphQueryStateBag].Split('|', StringSplitOptions.RemoveEmptyEntries);
                var graphService = context.HttpContext.RequestServices.GetService<GraphHelperService>(); 
                if(graphService != null)
                    userInfo.graph = await graphService.ExecuteQueryAsync(_configOptions.GraphEndpoint, context.Properties.GetTokenValue("access_token"), queries);
                    
            }

            string id_token = context.Properties.GetTokenValue("id_token");
            
            if (string.IsNullOrEmpty(id_token))
            {
                throw new InvalidOperationException("id_token is missing from authentication properties.  Ensure that SaveTokens option is 'true'.");
            }

            JwtSecurityToken jwtSecurityToken = new JwtSecurityToken(id_token);
            userInfo.PopulateFromClaims(jwtSecurityToken.Claims);
            claimsToKeep.Add(userInfo.ToPayloadClaim(_configOptions));

            identities.Add(new ClaimsIdentity(claimsToKeep, remIdentity.AuthenticationType, Constants.Claims.Subject, Constants.Claims.Role));

            //at this point we are done with properties, so dump the item collection keeping only the expiry
            DateTimeOffset? expiresUtc = context.Properties.ExpiresUtc;
            context.Properties.Items.Clear();
            context.Properties.ExpiresUtc = expiresUtc;

            await next(context).ConfigureAwait(false);

        }
        private string BuildScopeString(string baseScope, IList<string> additionalScopes)
        {
            return string.Join(' ', (baseScope ?? string.Empty)
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Union(additionalScopes));

        }

        private void EnsureLogger(HttpContext context)
        {
            if (_logger != null)
                return;
            
            if(context == null)
                throw new ArgumentNullException("context");

            var factory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            _logger = factory.CreateLogger("EasyAuthEvents");
        }

    }
}

