using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Text;
using EasyAuthForK8s.Web.Models;
using System.Linq;
using System.Security.Claims;

namespace EasyAuthForK8s.Web
{
    public class EasyAuthMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly EasyAuthConfigurationOptions _configureOptions;
        private readonly MicrosoftIdentityOptions _aadOptions;
        private IAuthorizationService _authService;
        private ILogger _logger;
        public EasyAuthMiddleWare(RequestDelegate next, 
            EasyAuthConfigurationOptions configureOptions, 
            MicrosoftIdentityOptions aadOptions, 
            IAuthorizationService authservice, 
            ILogger<EasyAuthMiddleWare> logger)
        {
            _next = next;
            _configureOptions = configureOptions;
            _aadOptions = aadOptions;
            _authService = authservice;
            _logger = logger; 
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_configureOptions.SigninPath == context.Request.Path)
            {

                await HandleChallenge(context);
                return;
            }
            else if (_configureOptions.AuthPath == context.Request.Path)
            {
                await HandleAuth(context);
                return;
            }

            // Call the next delegate/middleware in the pipeline
            //else
                await _next(context);
            
        }
        public async Task HandleChallenge(HttpContext context)
        {
            var state = context.EasyAuthStateFromHttpContext();

            LogRequestHeaders("HandleChallenge", context.Request);
            if (context.Request.Cookies.ContainsKey("foo-cookie"))
            {
                _logger.LogInformation($"Reading state from cookie: {state.ToJsonString()}");
            }
            if(state.Status == EasyAuthState.AuthStatus.Forbidden)
            {
                //show error or redirect
                _logger.LogInformation($"Fatal error logging user in: {state.Msg}");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(state.Msg);
            }
            else             
                await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        }
        public async Task HandleAuth(HttpContext context)
        {
            List<string> scopes = new List<string>();
            string message = "";
            EasyAuthState.AuthStatus authStatus = EasyAuthState.AuthStatus.Unauthenticated;

            LogRequestHeaders("HandleAuth", context.Request);

            var response = context.Response;
            response.Clear();
            response.ContentType = "text/html";

            var query = context.Request.Query;
            if (context.Request.Query.ContainsKey(Constants.ScopeParameterName))
                scopes.AddRange(context.Request.Query[Constants.ScopeParameterName]);

            var authN = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authN.Succeeded)
            {
                message += authN.Failure?.Message ?? " Cookie authentication failed.";
                if (_configureOptions.AllowBearerToken)
                {
                    {
                        //fall back to bearer token
                        authN = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

                        if (!authN.Succeeded)
                            message += authN.Failure?.Message ?? " Bearer token authentication failed.";

                    }
                }
                else
                {
                    message += " Bearer tokens are disabled.";
                    _logger.LogInformation($"User not authenticated. {message}");
                }
            }

            List<IAuthorizationRequirement> requirements = new() { new DenyAnonymousAuthorizationRequirement() };

            if (query.ContainsKey(Constants.RoleParameterName))
            {
                // a single role query parameter with multiple values will allow *any* of the roles to succeed
                // e.g. "?role=foo|foo2
                // multiple query parameters for roles will require *all* to succeed
                // e.g. "?role=foo&role=foo2"
                foreach (var item in query[Constants.RoleParameterName])
                {
                    if (!string.IsNullOrEmpty(item))
                        requirements.Add(new RolesAuthorizationRequirement(item.Split("|", System.StringSplitOptions.RemoveEmptyEntries)));
                }
            }
            //same && vs || treatment applies for scopes
            foreach (var item in scopes)
                requirements.Add(new ScopeAuthorizationRequirement(item.Split("|", System.StringSplitOptions.RemoveEmptyEntries)));

            var authZ = await _authService.AuthorizeAsync(authN.Principal ?? new ClaimsPrincipal(), default, requirements);

            if (!authZ.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                
                //in our parlance Forbbiden is a terminal failure and we only return 401
                //so that nginx can redirect to a friendly error for the user
                authStatus = EasyAuthState.AuthStatus.Forbidden;

                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.Append($"User {authN.Principal.GetObjectId()} is forbidden. ");

                foreach (var reason in authZ.Failure?.FailedRequirements)
                {
                    //if authZ fails because of a missing scope or identiry, we can round-trip and ask for it
                    if (reason is ScopeAuthorizationRequirement || reason is DenyAnonymousAuthorizationRequirement)
                        authStatus = EasyAuthState.AuthStatus.Unauthorized;
                    messageBuilder.Append($"{reason.ToString()}. ");
                }

                message = messageBuilder.ToString();

                _logger.LogInformation($"AuthZ failure: {message}");

                //build the state so we can have it after the redirect
                EasyAuthState state = new EasyAuthState
                {
                    Status = authStatus,
                    Scopes = scopes.SelectMany(x => x.Split("|", System.StringSplitOptions.RemoveEmptyEntries)).ToList(),
                    Msg = message
                };
                state.AddCookieToResponse(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                message = $"User {authN.Principal.GetObjectId()} is authorized.";

                _logger.LogInformation($"Returning Status202Accepted. AuthZ success: {message}");

                //TODO set the headers
            }
            await response.WriteAsync(message);
            
        }
            

        private void LogRequestHeaders(string prefix, HttpRequest request)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{prefix} - Request Headers [");

            foreach(var header in request.Headers)
            {
                sb.Append($"{header.Key}:{header.Value}|");
            }
            if (sb[sb.Length - 1] == '|')
                sb[sb.Length - 1] = ']';
            else sb.Append(']');

            _logger.LogDebug(sb.ToString());
        }

    }
}
