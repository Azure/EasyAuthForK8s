using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace EasyAuthForK8s.Web.Helpers
{
    internal class EventHelper
    {
        private readonly EasyAuthConfigurationOptions _configOptions;
        public EventHelper(EasyAuthConfigurationOptions configOptions)
        {
            _configOptions = configOptions ?? throw new ArgumentNullException(nameof(configOptions));
        }
        private static ILogger? _logger;
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

            //if additional scopes are requested, add them to the redirect
            EasyAuthState state = context.HttpContext.EasyAuthStateFromHttpContext();

            // there are three ways to determine where to send the user after successful signin
            // in order of precendence:
            //  1. if the rd parameter was supplied when they were sent the challenge it will already be set.  Don't change.
            //  2. The url extracted from the nginx header in the authreq (ie the url they were attempting to access, state.Url)
            //  3. Fall back to a predetermined path.  Default is the root "/"
            //  4. Never go back to the Login path, since it will just challenge again
            if (TryExtractInnerRedirect(context, out var rd))
                context.Properties.RedirectUri = rd;
            else
                context.Properties.RedirectUri = state.Url ?? _configOptions.DefaultRedirectAfterSignin;


            _logger!.LogInformation($"Redirecting sign-in to endpoint '{context.ProtocolMessage.IssuerAddress}' for '{context.Properties.RedirectUri}'");

            if (state.Scopes?.Count > 0)
            {
                context.Properties.Items.Add(Constants.OidcScopesStateBag, string.Join('&', state.Scopes.Select(x => string.Join('|', x))));
                try
                {
                    var graphService = context.HttpContext.RequestServices.GetService<GraphHelperService>();

                    if (graphService == null)
                        throw new("Graph Service could not be resolved.");

                    var manifestResult = await graphService!.GetManifestConfigurationAsync(context.HttpContext.RequestAborted);

                    if (!manifestResult.Succeeded)
                        throw manifestResult.Exception ?? new("Manifest retrieval operation returned a failure result.");

                    context.ProtocolMessage.Scope =
                        manifestResult.AppManifest!.FormattedScopeString(state.Scopes, context.ProtocolMessage.Scope);

                    _logger!.LogInformation($"Requested scopes: {context.ProtocolMessage.Scope}");
                }
                catch (Exception ex)
                {
                    _logger!.LogError(ex, "Failed to retrieve scopes from application manifest.");
                    throw new InvalidOperationException("Unable to retrieve available scopes for this application from Azure.  Try again later.", ex);
                }
            }

            //add the graph queries to the oidc message state so that they can be run after successful login
            context.Properties.Items.Add(Constants.OidcGraphQueryStateBag, string.Join('|', state.GraphQueries));

            await next(context).ConfigureAwait(false);
        }

        private static bool TryExtractInnerRedirect(RedirectContext context, out string? redirectUrl)
        {
            redirectUrl = null;
            var i = (context!.Properties!.RedirectUri ?? "").IndexOf('?');
            if (i < 0)
                return false;

            redirectUrl = HttpUtility.ParseQueryString(context!.Properties!.RedirectUri!.Substring(i))
                .Get(Constants.RedirectParameterName);

            return redirectUrl != null;
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

            ClaimsIdentity? remIdentity = context.Principal?.Identity as ClaimsIdentity;

            if (remIdentity != null)
            {
                List<ClaimsIdentity>? identities = context.Principal!.Identities as List<ClaimsIdentity>;
                identities!.Clear();

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

                string? access_token = context.Properties.GetTokenValue("access_token");

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

                //validate that the scopes granted account for all the scopes requested.  This is not strictly necessary
                //since the middleware will catch this.  However, the result there will be a return trip to request the scope
                //again, and if we didn't get it the first time, we must assume we never will and it will create an
                //infinite redirect loop.  So, just treat it as as fatal here and forbid.  In theory, this should never happen,
                //but it HAS happened, so this is here.
                if (context.Properties.Items.ContainsKey(Constants.OidcScopesStateBag))
                {
                    List<string[]> requestedScopes =
                        context.Properties.Items[Constants.OidcScopesStateBag]!
                        .Split('&', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Split('|', StringSplitOptions.RemoveEmptyEntries))
                        .ToList();

                    var scopeClaim = claimsToKeep.FirstOrDefault(x => x.Type == ClaimConstants.Scp);
                    if (scopeClaim != null)
                    {
                        var grantedScopes = scopeClaim!.Value.Split(' ');
                        foreach (var scope in requestedScopes)
                        {
                            if (!scope.Intersect(scopeClaim!.Value.Split(' ')).Any())
                            {
                                //turn this into something human readable
                                var requestedScopeString = string.Join(" AND ", requestedScopes.Select(x => "(" + string.Join(" OR ", x) + ")"));
                                throw new OpenIdConnectProtocolException($"The granted scope set '{scopeClaim!.Value}' " +
                                    $"does not satisfy the requested scope set '{requestedScopeString}'");
                            }
                        }
                    }
                }

                UserInfoPayload userInfo = new UserInfoPayload();

                if (context.Properties.Items.ContainsKey(Constants.OidcGraphQueryStateBag))
                {
                    string[] queries = context.Properties.Items[Constants.OidcGraphQueryStateBag]!.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    var graphService = context.HttpContext.RequestServices.GetService<GraphHelperService>();
                    if (graphService != null)
                        userInfo.graph = await graphService!.ExecuteQueryAsync(access_token!, queries, context.HttpContext.RequestAborted);

                }

                string? id_token = context.Properties.GetTokenValue("id_token");

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
            }
            await next(context).ConfigureAwait(false);

        }

        public static async Task HandleException(HttpContext context)
        {
            //TODO: we need to do some instrumentation here so that it can 
            //be used by the health check

            EnsureLogger(context);

            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var graphService = context.RequestServices.GetService<GraphHelperService>();

            StringBuilder sb = new StringBuilder(feature?.Error.Message ?? "Unknown Internal Error");
            var code = context.Response.StatusCode;
            string? reasonPhrase = null;

            if (feature?.Error != null)
            {
                var current = feature!.Error!;

                while (current.InnerException != null)
                {
                    sb.Append("... ");
                    current = current.InnerException;
                    if (current is BadHttpRequestException)
                    {
                        //get the real status
                        var ex = current as BadHttpRequestException;
                        if (!string.IsNullOrEmpty(ex?.Message))
                            sb.Append(ex!.Message);
                        code = ex?.StatusCode ?? code;
                    }
                    else if (current is OpenIdConnectProtocolException)
                    {
                        //unwrap oidc data
                        var ex = current as OpenIdConnectProtocolException;
                        sb.Append(ex?.Data["error_description"] as string ?? ex?.Message);
                        reasonPhrase = "Azure Active Directory Error";
                    }
                    else if (!string.IsNullOrEmpty(current.Message))
                        sb.Append(current.Message);
                }
            }

            var message = sb.ToString();
            _logger!.LogError($"Error: {message}, TraceId: {context.TraceIdentifier}, Status: {code}");

            var manifestResult = graphService != null ? await graphService.GetManifestConfigurationAsync(context.RequestAborted) : new();

            await ErrorPage.Render(context.Response,
                manifestResult.Succeeded ? manifestResult.AppManifest! : new AppManifest(),
                reasonPhrase ?? ReasonPhrases.GetReasonPhrase(code), message);
        }
        private string BuildScopeString(string baseScope, IList<string> additionalScopes)
        {
            return string.Join(' ', (baseScope ?? string.Empty)
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Union(additionalScopes));

        }

        private static void EnsureLogger(HttpContext context)
        {
            if (_logger != null)
                return;

            if (context == null)
                throw new ArgumentNullException("context");

            var factory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            _logger = factory.CreateLogger("EasyAuthEvents");
        }


    }
}

