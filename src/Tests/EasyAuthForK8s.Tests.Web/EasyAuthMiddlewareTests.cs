using EasyAuthForK8s.Web;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Builder;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Principal;
using EasyAuthForK8s.Tests.Web.Helpers;
using System.Net.Http;
using System;
using System.Net;
using System.Runtime.InteropServices;

namespace EasyAuthForK8s.Tests.Web;

public class EasyAuthMiddlewareTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/foo")]
    [InlineData("/foo/")]
    [InlineData("/foo/foo/foo/foo/foo")]
    public void Invoke_HandleAuth_ResponseFromConfiguredPath(string path)
    {
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions() { AuthPath = path};

        var response = GetResponseForAuthN(options, "", out var logs, out var stateResolver);

        Assert.Equal(path, response.RequestMessage.RequestUri.LocalPath);
        Assert.Contains(logs, l => l.Message.StartsWith($"Invoke HandleAuth - Path:{path},"));
    }

    [Theory]
    [InlineData("", "Requires an authenticated user.")]
    [InlineData("?role=foo", "User.IsInRole must be true for one of the following roles: (foo)")]
    [InlineData("?scope=foo", "is one of the following values: (foo)")]
    public async Task Invoke_HandleAuth_Unauthenticated(string query, string containsMessage)
    {
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions();

        var response = GetResponseForAuthN(options, query, out var logs, out var stateResolver);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        EasyAuthState state = stateResolver(response);

        Assert.Equal(EasyAuthState.AuthStatus.Unauthenticated, state.Status);
        await EvaluateMessagesWithAsserts(containsMessage, response.Content, state, logs);
    }

    [Theory]
    [InlineData("?scope=foo", "ScopeAuthorizationRequirement:Scope= and `scp` or `http://schemas.microsoft.com/identity/claims/scope` is one of the following values: (foo)", new string[] { "foo" })]
    [InlineData("?scope=foo|foo2", "is one of the following values: (foo|foo2)", new string[] { "foo", "foo2" })]
    [InlineData("?scope=foo&scope=foo2", "is one of the following values: (foo)", new string[] { "foo", "foo2" })]
    public async Task Invoke_HandleAuth_UnauthorizedWithScopes(string query, string containsMessage, string[] scopes)
    {
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions();

        var response = GetResponseForAuthZ(options, query, out var logs, out var stateResolver);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        EasyAuthState state = stateResolver(response);

        Assert.Equal(EasyAuthState.AuthStatus.Unauthorized, state.Status);
        Assert.Equal(state.Scopes, scopes.ToList());

        await EvaluateMessagesWithAsserts(containsMessage, response.Content, state, logs);
    }

    [Theory]
    [InlineData("?role=foo", "RolesAuthorizationRequirement:User.IsInRole must be true for one of the following roles: (foo) ")]
    [InlineData("?role=foo|foo2", "RolesAuthorizationRequirement:User.IsInRole must be true for one of the following roles: (foo|foo2)")]
    [InlineData("?role=foo&scope=foo2", "RolesAuthorizationRequirement:User.IsInRole must be true for one of the following roles: (foo)")]
    public async Task Invoke_HandleAuth_ForbiddenWithRoles(string query, string containsMessage)
    {
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions();

        var response = GetResponseForAuthZ(options, query, out var logs, out var stateResolver);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
       
        EasyAuthState state = stateResolver(response);

        Assert.Equal(EasyAuthState.AuthStatus.Forbidden, state.Status);

        await EvaluateMessagesWithAsserts(containsMessage, response.Content, state, logs);
    }

    private IConfiguration GetConfiguration(EasyAuthConfigurationOptions options)
    {
        return new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", false, true)
            .Add(new EasyAuthOptionsConfigurationSource(options))
            .Build();
    }
    private HttpResponseMessage GetResponseForAuthN(EasyAuthConfigurationOptions options, string query,
       out IReadOnlyList<TestLogger.LoggedMessage> logs, [Optional] out Func<HttpResponseMessage, EasyAuthState> stateResolver)
    {
        TestLogger.TestLoggerFactory loggerFactory = new TestLogger.TestLoggerFactory();
        logs = loggerFactory.Logger.Messages;

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<EasyAuthMiddleware>>(loggerFactory.CreateLogger<EasyAuthMiddleware>());
                services.AddEasyAuthForK8s(GetConfiguration(options), loggerFactory);
            })
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseEasyAuthForK8s();
                });
            }).Build();

        host.StartAsync().Wait();

        IDataProtector dp = host.Services.GetService<IDataProtectionProvider>().CreateProtector(Constants.StateCookieName);
        stateResolver = new Func<HttpResponseMessage, EasyAuthState>((response) => GetStateFromResponseWithAsserts(dp, response));

        return host.GetTestServer().CreateClient().GetAsync(string.Concat(options.AuthPath, query)).Result;
    }
    private HttpResponseMessage GetResponseForAuthZ(EasyAuthConfigurationOptions options, string query, 
        out IReadOnlyList<TestLogger.LoggedMessage> logs, out Func<HttpResponseMessage, EasyAuthState> stateResolver)
    {
        TestLogger.TestLoggerFactory loggerFactory = new TestLogger.TestLoggerFactory();
        logs = loggerFactory.Logger.Messages;

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<EasyAuthMiddleware>>(loggerFactory.CreateLogger<EasyAuthMiddleware>());
                services.AddEasyAuthForK8s(GetConfiguration(options), loggerFactory);

                //swap out the cookiehandler with one that will do what we tell it
                services.Configure<AuthenticationOptions>(options =>
                {
                    var schemes = options.Schemes as List<AuthenticationSchemeBuilder>;
                    schemes.First(c => c.Name == CookieAuthenticationDefaults.AuthenticationScheme).HandlerType = typeof(TestAuthenticationHandler);
                });
            })
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseEasyAuthForK8s();
                });
            }).Build();

        host.StartAsync().Wait();

        IDataProtector dp = host.Services.GetService<IDataProtectionProvider>().CreateProtector(Constants.StateCookieName);
        stateResolver = new Func<HttpResponseMessage, EasyAuthState>((response) => GetStateFromResponseWithAsserts(dp, response));

        return host.GetTestServer().CreateClient().GetAsync(string.Concat(options.AuthPath, query)).Result;
    }
    private EasyAuthState GetStateFromResponseWithAsserts(IDataProtector dp, HttpResponseMessage response)
    {
        Assert.True(response.Headers.Contains(HeaderNames.SetCookie));
        Assert.True(CookieHeaderValue.TryParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList(), out IList<CookieHeaderValue> cookieValues));
        Assert.Contains(cookieValues, cookieValues => cookieValues.Name == Constants.StateCookieName);

        CookieHeaderValue cookieHeader = cookieValues.First(x => x.Name == Constants.StateCookieName);
        Assert.True(cookieHeader.Value.HasValue);

        EasyAuthState state = JsonSerializer.Deserialize<EasyAuthState>(dp.Unprotect(cookieHeader.Value.Value));
        Assert.NotNull(state);

        return state;
    }
    private async Task EvaluateMessagesWithAsserts(string containsMessage, HttpContent body, EasyAuthState state, IReadOnlyList<TestLogger.LoggedMessage> logs )
    {
        string responseBody = await body.ReadAsStringAsync();
        Assert.Contains(containsMessage, responseBody);
        Assert.Contains(containsMessage, state.Msg);
        Assert.Contains(logs, x => x.Message.Contains(containsMessage));
    }
}

