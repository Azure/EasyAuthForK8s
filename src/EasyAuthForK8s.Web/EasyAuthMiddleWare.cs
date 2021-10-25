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

namespace EasyAuthForK8s.Web
{
    public class EasyAuthMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly EasyAuthConfigurationOptions _configureOptions;
        private readonly MicrosoftIdentityOptions _aadOptions;
        private IAuthorizationService _authService;
        public EasyAuthMiddleWare(RequestDelegate next, EasyAuthConfigurationOptions configureOptions, MicrosoftIdentityOptions aadOptions, IAuthorizationService authservice)
        {
            _next = next;
            _configureOptions = configureOptions;
            _aadOptions = aadOptions;
            _authService = authservice;
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
            //AuthenticationProperties properties = new();
            //properties.SetParameter<string[]>("easyauth_requested_scopes", new string[]{ "foo" });
            
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        }
        public async Task HandleAuth(HttpContext context)
        {
            var response = context.Response;
            response.Clear();
            response.ContentType = "text/html";

            var failureMessage = "";
            var authN = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authN.Succeeded && _configureOptions.AllowBearerToken)
            {
                //fall back to bearer token
                failureMessage += authN.Failure?.Message ?? " Cookie authentication failed. ";
                authN = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

                if(!authN.Succeeded)
                    failureMessage += authN.Failure?.Message ?? " Fall back Bearer token authentication also failed.";
               
            }
            if(!authN.Succeeded)
            {
                response.StatusCode = StatusCodes.Status401Unauthorized;
                await response.WriteAsync($"User is not authenticated. {failureMessage}");
            }

            else
            {
                List<IAuthorizationRequirement> requirements = new() { new DenyAnonymousAuthorizationRequirement() };
                var query = context.Request.Query;

                if (query.ContainsKey(Constants.RoleParameterName))
                {
                    // a single role query parameter with multiple values will allow *any* of the roles to succeed
                    // e.g. "?role=foo|foo2
                    // multiple query parameters for roles will require *all* to succeed
                    // e.g. "?role=foo&role=foo2"
                    foreach(var item in query[Constants.RoleParameterName])
                    {
                        if (!string.IsNullOrEmpty(item))
                            requirements.Add(new RolesAuthorizationRequirement(item.Split("|", System.StringSplitOptions.RemoveEmptyEntries)));
                    }
                }
                if (query.ContainsKey(Constants.ScopeParameterName))
                {
                    //same && vs || treatment applies
                    foreach (var item in query[Constants.ScopeParameterName])
                    {
                        if (!string.IsNullOrEmpty(item))
                            requirements.Add(new ScopeAuthorizationRequirement(item.Split("|", System.StringSplitOptions.RemoveEmptyEntries)));
                    }
                }

                var authZ = await _authService.AuthorizeAsync(authN.Principal, default, requirements);
                
                if (!authZ.Succeeded)
                {
                    response.StatusCode = StatusCodes.Status403Forbidden;
                    await response.WriteAsync($"User {authN.Principal.GetObjectId()} forbidden. ");
                    foreach (var reason in authZ.Failure?.FailedRequirements)
                        await response.WriteAsync($"{reason.ToString()}. ");
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status202Accepted;
                    await response.WriteAsync($"User {authN.Principal.GetObjectId()} authorized. ");
                    //TODO set the headers
                }
            }
            await context.Response.StartAsync();
        }
    }
}
