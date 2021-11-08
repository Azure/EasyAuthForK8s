using EasyAuthForK8s.Tests.Web.Helpers;
using EasyAuthForK8s.Web;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicrosoftIdentityOptions = Microsoft.Identity.Web.MicrosoftIdentityOptions;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

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
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions() { AuthPath = path };

        HttpResponseMessage response = GetResponseForAuthN(options, "", out IReadOnlyList<TestLogger.LoggedMessage> logs, out Func<HttpResponseMessage, EasyAuthState> stateResolver);

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

        HttpResponseMessage response = GetResponseForAuthN(options, query, out IReadOnlyList<TestLogger.LoggedMessage> logs, out Func<HttpResponseMessage, EasyAuthState> stateResolver);

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

        HttpResponseMessage response = GetResponseForAuthZ(options, query, out var logs, out var stateResolver);

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

        HttpResponseMessage response = GetResponseForAuthZ(options, query, out var logs, out var stateResolver);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        EasyAuthState state = stateResolver(response);

        Assert.Equal(EasyAuthState.AuthStatus.Forbidden, state.Status);

        await EvaluateMessagesWithAsserts(containsMessage, response.Content, state, logs);
    }
    
    [Fact]
    public async Task Invoke_HandleAuth_IdentityOnly()
    {
        string containsMessage = "Subject Jon is authorized";
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions();

        HttpResponseMessage response = GetResponseForAuthZ(options, "", out var logs, out var stateResolver);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains(containsMessage, responseBody);
        Assert.Contains(logs, x => x.Message.Contains(containsMessage));
    }

    [Theory]
    [InlineData("/foo", "/bar", 200, false )] //No warn: cookie is large, but isn't set by the callback
    [InlineData("/foo", "/foo", 200, true)] //Warn: cookie is large and is set by the call back
    [InlineData("/foo", "/bar", 50, false)] //No warn: cookie is small, and isn't set by the callback
    [InlineData("/foo", "/foo", 50, false)] //No warn: cookie is small, and is set by the callback
    public async Task LogWarningForLargeAuthCookie(string callbackPath, string testPath, uint datasize, bool shouldWarn)
    {
        TestLogger.TestLoggerFactory loggerFactory = new TestLogger.TestLoggerFactory();
        Microsoft.Identity.Web.MicrosoftIdentityOptions options = new() { CallbackPath = callbackPath };

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddSingleton<IOptions<MicrosoftIdentityOptions>>(Options.Create<MicrosoftIdentityOptions>(options));
            })
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseLargeSetCookieLogWarning(100);
                    app.Run(async context =>
                    {
                        context.Response.Cookies.Append("auth", TestUtility.RandomSafeString(datasize));
                        await context.Response.WriteAsync("");
                    });
                });
            }).Build();

        await host.StartAsync();
        _ = await host.GetTestServer().CreateClient().GetAsync(testPath);

        var logs = loggerFactory.Logger.Messages;

        if (shouldWarn)
            Assert.Contains(logs, m => m.LogLevel == LogLevel.Warning && m.Message.StartsWith("Large Set-Cookie response header detected"));
        else
            Assert.DoesNotContain(logs, m => m.LogLevel == LogLevel.Warning);
    }
    private IConfiguration GetConfiguration(EasyAuthConfigurationOptions options)
    {
        return new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", false, true)
            .Add(new EasyAuthOptionsConfigurationSource(options))
            .Build();
    }
    private HttpResponseMessage GetResponseForAuthN(EasyAuthConfigurationOptions options, string query,
       out IReadOnlyList<TestLogger.LoggedMessage> logs, out Func<HttpResponseMessage, EasyAuthState> stateResolver)
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
                    List<AuthenticationSchemeBuilder> schemes = options.Schemes as List<AuthenticationSchemeBuilder>;
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
    private async Task EvaluateMessagesWithAsserts(string containsMessage, HttpContent body, EasyAuthState state, IReadOnlyList<TestLogger.LoggedMessage> logs)
    {
        string responseBody = await body.ReadAsStringAsync();
        Assert.Contains(containsMessage, responseBody);
        Assert.Contains(containsMessage, state.Msg);
        Assert.Contains(logs, x => x.Message.Contains(containsMessage));
    }
}

