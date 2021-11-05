﻿using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Web
{
    public class EasyAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly EasyAuthConfigurationOptions _configureOptions;
        private readonly MicrosoftIdentityOptions _aadOptions;
        private readonly IAuthorizationService _authService;
        private readonly ILogger _logger;
        public EasyAuthMiddleware(RequestDelegate next,
            IOptions<EasyAuthConfigurationOptions> configureOptions,
            IOptions<MicrosoftIdentityOptions> aadOptions,
            IAuthorizationService authservice,
            ILogger<EasyAuthMiddleware> logger)
        {

            if (next == null)
                throw new ArgumentNullException(nameof(next));

            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));
            
            if (aadOptions == null)
                throw new ArgumentNullException(nameof(aadOptions));

            if (authservice == null)
                throw new ArgumentNullException(nameof(authservice));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _next = next;
            _configureOptions = configureOptions.Value;
            _aadOptions = aadOptions.Value;
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
            await _next(context);

        }
        public async Task HandleChallenge(HttpContext context)
        {
            EasyAuthState state = context.EasyAuthStateFromHttpContext();

            LogRequestHeaders("HandleChallenge", context.Request);
            if (context.Request.Cookies.ContainsKey("foo-cookie"))
            {
                _logger.LogInformation($"Reading state from cookie: {state.ToJsonString()}");
            }
            if (state.Status == EasyAuthState.AuthStatus.Forbidden)
            {
                //show error or redirect
                _logger.LogInformation($"Fatal error logging user in: {state.Msg}");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(state.Msg);
            }
            else
            {
                await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
            }
        }
        public async Task HandleAuth(HttpContext context)
        {
            List<string> scopes = new List<string>();
            string message = "";
            EasyAuthState.AuthStatus authStatus = EasyAuthState.AuthStatus.Unauthenticated;

            LogRequestHeaders("HandleAuth", context.Request);

            HttpResponse response = context.Response;
            response.Clear();
            response.ContentType = "text/html";

            IQueryCollection query = context.Request.Query;
            if (context.Request.Query.ContainsKey(Constants.ScopeParameterName))
            {
                scopes.AddRange(context.Request.Query[Constants.ScopeParameterName]);
            }

            AuthenticateResult authN = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authN.Succeeded)
            {
                message += authN.Failure?.Message ?? "Cookie authentication failed. ";
                if (_configureOptions.AllowBearerToken)
                {
                    {
                        //fall back to bearer token
                        authN = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

                        if (!authN.Succeeded)
                        {
                            message += authN.Failure?.Message ?? "Bearer token authentication failed. ";
                        }
                    }
                }
            }

            List<IAuthorizationRequirement> requirements = new() { new DenyAnonymousAuthorizationRequirement() };

            if (query.ContainsKey(Constants.RoleParameterName))
            {
                // a single role query parameter with multiple values will allow *any* of the roles to succeed
                // e.g. "?role=foo|foo2
                // multiple query parameters for roles will require *all* to succeed
                // e.g. "?role=foo&role=foo2"
                // roles are CASE SENSITIVE!!
                foreach (string item in query[Constants.RoleParameterName])
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        requirements.Add(new RolesAuthorizationRequirement(item.Split("|", System.StringSplitOptions.RemoveEmptyEntries)));
                    }
                }
            }
            //same && vs || treatment applies for scopes
            foreach (string item in scopes)
            {
                requirements.Add(new ScopeAuthorizationRequirement(item.Split("|", System.StringSplitOptions.RemoveEmptyEntries)));
            }

            AuthorizationResult authZ = await _authService.AuthorizeAsync(authN.Principal ?? new ClaimsPrincipal(), default, requirements);

            if (!authZ.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                //in our parlance Forbbiden is a terminal failure and we only return 401
                //so that nginx can redirect to a friendly error for the user
                authStatus = EasyAuthState.AuthStatus.Forbidden;
                StringBuilder messageBuilder = new StringBuilder();

                messageBuilder.Append($"Access denied for subject {(authN.Principal?.Identity.Name ?? "[anonymous]")}. ");

                foreach (IAuthorizationRequirement reason in authZ.Failure?.FailedRequirements)
                {
                    //if authZ fails because of a missing scope or identiry, we can round-trip and ask for it
                    if (reason is ScopeAuthorizationRequirement || reason is DenyAnonymousAuthorizationRequirement)
                    {
                        authStatus = EasyAuthState.AuthStatus.Unauthorized;
                    }

                    messageBuilder.Append($"{reason.ToString()}. ");
                }

                message += messageBuilder.ToString();

                _logger.LogInformation($"AuthX failure: {message}");

                //build the state so we can have it after the redirect
                EasyAuthState state = new EasyAuthState
                {
                    Status = authStatus,
                    Scopes = scopes.SelectMany(x => x.Split("|", System.StringSplitOptions.RemoveEmptyEntries)).ToList(),
                    Msg = message
                };

                if (context.Request.Query.ContainsKey(Constants.GraphParameterName))
                {
                    state.GraphQueries = context.Request.Query[Constants.GraphParameterName]
                        .SelectMany(x => x.Split("|", System.StringSplitOptions.RemoveEmptyEntries))
                        .ToList();
                }
                // nginx authreq uses a subrequest and only does two things we can use:
                // 1. Read the status code
                // 2. Read the response headers.
                // a 401 status code tells nginx to redirect (302) the browser to the
                // handler page for the 401 code, a.k.a. the signin in page.  Here we 
                // are setting a cookie, which will be sent to the browser and sent back to
                // the challenge handler as a proxied request.  
                state.AddCookieToResponse(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                message = $"Subject {authN.Principal.Identity.Name} is authorized.";

                _logger.LogInformation($"Returning Status202Accepted. AuthZ success: {message}");

                //rehydrate the user information as an intermediate step
                UserInfoPayload info = authN.Principal.UserInfoPayloadFromPrincipal(_configureOptions);

                //append the user's information as headers using whatever options are configured
                info.AppendResponseHeaders(context.Response.Headers, _configureOptions);
            }
            //nginx does nothing with the response body, so this is primarily for debugging purposes
            await response.WriteAsync(message);

        }


        private void LogRequestHeaders(string prefix, HttpRequest request)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{prefix} - Request Headers [");

            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in request.Headers)
            {
                sb.Append($"{header.Key}:{header.Value}|");
            }
            if (sb[sb.Length - 1] == '|')
            {
                sb[sb.Length - 1] = ']';
            }
            else
            {
                sb.Append(']');
            }

            _logger.LogDebug(sb.ToString());
        }

    }
}