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
using ClaimConstants = Microsoft.Identity.Web.ClaimConstants;
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
using System.Security.Claims;
using EasyAuthForK8s.Web.Helpers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.WebUtilities;

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
    [InlineData("?scope=foo", "Consented scope must contain one of the following values: (foo)")]
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
    [InlineData("?scope=foo", "ScopeRequirement: Consented scope must contain one of the following values: (foo)", new string[] { "foo" })]
    [InlineData("?scope=foo|foo2", "ScopeRequirement: Consented scope must contain one of the following values: (foo, foo2)", new string[] { "foo", "foo2" })]
    [InlineData("?scope=foo&scope=foo2", "ScopeRequirement: Consented scope must contain one of the following values: (foo)", new string[] { "foo", "foo2" })]
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
    [InlineData(CookieAuthenticationDefaults.AuthenticationScheme, true, CookieAuthenticationDefaults.AuthenticationScheme)]
    [InlineData(CookieAuthenticationDefaults.AuthenticationScheme, false, CookieAuthenticationDefaults.AuthenticationScheme)]
    [InlineData(JwtBearerDefaults.AuthenticationScheme, true, JwtBearerDefaults.AuthenticationScheme)]
    [InlineData(JwtBearerDefaults.AuthenticationScheme, false, "")]
    public void Invoke_HandleAuth_CorrectSchemeUsed(string scheme, bool allowBearer, string expectedScheme)
    {
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions() { AllowBearerToken = allowBearer };

        //must force a 401 so we can inspect the scheme used to acquire the identity
        HttpResponseMessage response = GetResponseForAuthZ(options, "?scope=nonexistent", out var logs, out var stateResolver, null, scheme);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        EasyAuthState state = stateResolver(response);

        Assert.Equal(expectedScheme, state.Scheme);
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
    [InlineData(202, "?role=", new string[] { }, "")]
    [InlineData(202, "?role=", new string[] { "foo" }, "")]
    [InlineData(202, "?role=foo", new string[] { "foo" }, "")]
    [InlineData(202, "?role=foo|bar", new string[] { "foo" }, "")]
    [InlineData(202, "?role=foo|bar", new string[] { "foo", "bar" }, "")]
    [InlineData(202, "?role=foo&role=bar", new string[] { "foo", "bar" }, "")]
    [InlineData(401, "?role=foo", new string[] { }, "")]
    [InlineData(401, "?role=foo", new string[] { "bar" }, "")]
    [InlineData(401, "?role=foo&role=bar", new string[] { "foo" }, "")]
    [InlineData(202, "?scope=", new string[] { }, "")]
    [InlineData(202, "?scope=", new string[] { }, "foo")]
    [InlineData(202, "?scope=foo", new string[] { }, "foo")]
    [InlineData(202, "?scope=foo|bar", new string[] { }, "foo")]
    [InlineData(202, "?scope=foo|bar", new string[] { }, "foo bar")]
    [InlineData(202, "?scope=foo&scope=bar", new string[] { }, "foo bar")]
    [InlineData(401, "?scope=foo", new string[] { }, "")]
    [InlineData(401, "?scope=foo", new string[] { }, "bar")]
    [InlineData(401, "?scope=foo&role=bar", new string[] { }, "foo")]
    [InlineData(202, "?role=", new string[] { }, "", true)]
    [InlineData(202, "?role=", new string[] { "foo" }, "", true)]
    [InlineData(202, "?role=foo", new string[] { "foo" }, "", true)]
    [InlineData(202, "?role=foo|bar", new string[] { "foo" }, "", true)]
    [InlineData(202, "?role=foo|bar", new string[] { "foo", "bar" }, "", true)]
    [InlineData(202, "?role=foo&role=bar", new string[] { "foo", "bar" }, "", true)]
    [InlineData(401, "?role=foo", new string[] { }, "", true)]
    [InlineData(401, "?role=foo", new string[] { "bar" }, "", true)]
    [InlineData(401, "?role=foo&role=bar", new string[] { "foo" }, "", true)]
    [InlineData(202, "?scope=", new string[] { }, "", true)]
    [InlineData(202, "?scope=", new string[] { }, "foo", true)]
    [InlineData(202, "?scope=foo", new string[] { }, "foo", true)]
    [InlineData(202, "?scope=foo|bar", new string[] { }, "foo", true)]
    [InlineData(202, "?scope=foo|bar", new string[] { }, "foo bar", true)]
    [InlineData(202, "?scope=foo&scope=bar", new string[] { }, "foo bar", true)]
    [InlineData(401, "?scope=foo", new string[] { }, "", true)]
    [InlineData(401, "?scope=foo", new string[] { }, "bar", true)]
    [InlineData(401, "?scope=foo&role=bar", new string[] { }, "foo", true)]
    public void Invoke_HandleAuth_Authorize_WithRolesAndScopes(int expectedStatus, string query, string[] roles, string scope, bool allowBearerToken = false)
    {
        EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions() { AllowBearerToken = allowBearerToken };
        TestAuthenticationHandlerOptions handlerOptions = new();
        handlerOptions.Claims.AddRange(roles.Select(x => new Claim(ClaimConstants.Roles, x)));

        if (!string.IsNullOrEmpty(scope))
            handlerOptions.Claims.Add(new Claim(ClaimConstants.Scp, scope));

        HttpResponseMessage response = GetResponseForAuthZ(options, query, handlerOptions,
            allowBearerToken ? JwtBearerDefaults.AuthenticationScheme : CookieAuthenticationDefaults.AuthenticationScheme);

        if (expectedStatus != (int)response.StatusCode)
        {
            var body = response.Content.ReadAsStringAsync().Result;
        }

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("/foo", "/bar", 500, false)] //No warn: cookie is large, but isn't set by the callback
    [InlineData("/foo", "/foo", 500, true)] //Warn: cookie is large and is set by the call back
    [InlineData("/foo", "/bar", 50, false)] //No warn: cookie is small, and isn't set by the callback
    [InlineData("/foo", "/foo", 50, false)] //No warn: cookie is small, and is set by the callback
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Assertions", "xUnit2012:Do not use Enumerable.Any() to check if a value exists in a collection",
        Justification = "Race condition prevents Contains() from evaluating determinitistically")]
    public async Task LogWarningForLargeAuthCookie(string callbackPath, string testPath, uint datasize, bool shouldWarn)
    {
        TestLogger logger = new TestLogger();
        Microsoft.Identity.Web.MicrosoftIdentityOptions options = new() { CallbackPath = callbackPath };

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerFactory>(logger.Factory());
                services.AddSingleton<IOptions<MicrosoftIdentityOptions>>(Options.Create<MicrosoftIdentityOptions>(options));
            })
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseLargeSetCookieLogWarning(250);
                    app.Run(async context =>
                    {
                        context.Response.Cookies.Append("auth", TestUtility.RandomSafeString(datasize));
                        await context.Response.WriteAsync("");
                        return;
                    });
                });
            }).Build();

        await host.StartAsync();
        await host.GetTestServer().CreateClient().GetAsync(testPath);

        //this particular test looks for something that is logged after the request completes
        //it takes a moment for the logs to flush, so block while the host is shut down
        //so the logs get written before we evaluate them
        await host.StopAsync();
        await host.WaitForShutdownAsync();

        //it's late, and I still can't force logs to flush before they are evaluated.  So Task.Delay it is for now.
        await Task.Delay(2000);

        if (shouldWarn)
            Assert.True(logger.Messages.Any(m => m.LogLevel == LogLevel.Warning && m.Message.Contains("Large Set-Cookie response header detected")));
        else
            Assert.DoesNotContain(logger.Messages, m => m.LogLevel == LogLevel.Warning);

    }
    private IConfiguration GetConfiguration(EasyAuthConfigurationOptions options)
    {
        return new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", false, true)
            .Add(new EasyAuthOptionsConfigurationSource(options))
            .Build();
    }
    private HttpResponseMessage GetResponseForAuthN(
        EasyAuthConfigurationOptions options,
        string query,
        out IReadOnlyList<TestLogger.LoggedMessage> logs,
        out Func<HttpResponseMessage, EasyAuthState> stateResolver)
    {
        TestLogger logger = new TestLogger();
        logs = logger.Messages;

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<EasyAuthMiddleware>>(logger.Factory().CreateLogger<EasyAuthMiddleware>());
                services.AddEasyAuthForK8s(GetConfiguration(options), logger.Factory());
                services.Replace(new ServiceDescriptor(typeof(GraphHelperService), MockGraphHelper()));
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

        IDataProtector dp = host.Services
            .GetService<IDataProtectionProvider>()
            .CreateProtector(Constants.StateCookieName);

        stateResolver = new(((response) => GetStateFromResponseWithAsserts(dp, response)));

        return host.GetTestServer()
            .CreateClient()
            .GetAsync(string.Concat(options.AuthPath, query))
            .Result;
    }
    private HttpResponseMessage GetResponseForAuthZ(EasyAuthConfigurationOptions options,
        string query,
        TestAuthenticationHandlerOptions handlerOptions = null,
        string scheme = CookieAuthenticationDefaults.AuthenticationScheme) =>
        GetResponseForAuthZ(options, query, out var logs, out var stateResolver, handlerOptions, scheme);

    private HttpResponseMessage GetResponseForAuthZ(
        EasyAuthConfigurationOptions options,
        string query,
        out List<TestLogger.LoggedMessage> logs,
        out Func<HttpResponseMessage, EasyAuthState> stateResolver,
        TestAuthenticationHandlerOptions handlerOptions = null,
        string scheme = CookieAuthenticationDefaults.AuthenticationScheme)
    {
        TestLogger logger = new TestLogger();
        logs = logger.Messages;

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<EasyAuthMiddleware>>(logger.Factory().CreateLogger<EasyAuthMiddleware>());
                services.AddEasyAuthForK8s(GetConfiguration(options), logger.Factory());
                if (handlerOptions != null)
                    services.AddSingleton<TestAuthenticationHandlerOptions>(handlerOptions);

                //swap out the cookiehandler with one that will do what we tell it
                services.Configure<AuthenticationOptions>(options =>
                {
                    List<AuthenticationSchemeBuilder> schemes = options.Schemes as List<AuthenticationSchemeBuilder>;
                    var s = schemes.FirstOrDefault(c => c.Name == scheme);
                    if (s != null)
                    {
                        s.HandlerType = typeof(TestAuthenticationHandler);
                    }
                });
                services.Replace(new ServiceDescriptor(typeof(GraphHelperService), MockGraphHelper()));
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

        IDataProtector dp = host.Services
            .GetService<IDataProtectionProvider>()
            .CreateProtector(Constants.StateCookieName);

        stateResolver = new((response) => GetStateFromResponseWithAsserts(dp, response));

        return host.GetTestServer()
            .CreateClient()
            .GetAsync(string.Concat(options.AuthPath, query))
            .Result;
    }

    [Fact]
    public async Task Invoke_HandleChallenge_Redirect()
    {
        var options = new EasyAuthConfigurationOptions();
        TestLogger logger = new TestLogger();

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<EasyAuthMiddleware>>(logger.Factory().CreateLogger<EasyAuthMiddleware>());
                services.AddEasyAuthForK8s(GetConfiguration(options), logger.Factory());

                //swap out the cookiehandler with one that will do what we tell it
                services.Configure<AuthenticationOptions>(options =>
                {
                    List<AuthenticationSchemeBuilder> schemes = options.Schemes as List<AuthenticationSchemeBuilder>;
                    var s = schemes.FirstOrDefault(c => c.Name == CookieAuthenticationDefaults.AuthenticationScheme);
                    if (s != null)
                    {
                        s.HandlerType = typeof(TestAuthenticationHandler);
                    }
                });
                services.Replace(new ServiceDescriptor(typeof(GraphHelperService), MockGraphHelper()));
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

        await host.StartAsync();

        var server = host.GetTestServer();

        var authResponse = await server.CreateClient().GetAsync(string.Concat(options.AuthPath, "?scope=unrecognized&scope=foo"));
        Assert.Equal(HttpStatusCode.Unauthorized, authResponse.StatusCode);
        Assert.Contains(authResponse.Headers, x => x.Key == HeaderNames.SetCookie);

        var signinReponse = await server
            .CreateRequest(options.SigninPath)
            .AddHeader(HeaderNames.Cookie, authResponse.Headers.First(x => x.Key == HeaderNames.SetCookie).Value.First())
            .GetAsync();

        Assert.Equal(HttpStatusCode.Redirect, signinReponse.StatusCode);
        Assert.NotNull(signinReponse.Headers.Location);

        var redirectQuery = TestUtility.ParseQuery(signinReponse.Headers.Location.Query);

        Assert.True(redirectQuery.ContainsKey("scope"));
        var scopeValues = redirectQuery["scope"].First().Split(' ');

        //converted to audience/scope format
        Assert.Contains($"{TestUtility.DummyGuid}/foo", scopeValues);

        //not recognized, so not converted
        Assert.Contains("unrecognized", scopeValues);

        //not recognized, so moved to the end
        Assert.Equal("unrecognized", scopeValues.Last());
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

    private GraphHelperService MockGraphHelper()
    {
        var manifest = new AppManifest()
        {
            appId = TestUtility.DummyGuid,
            publishedPermissionScopes = new()
            {
                new() { value = "foo" },
                new() { value = "bar" }
            }
        };

        var openIdConnectOptions = Mock.Of<IOptionsMonitor<OpenIdConnectOptions>>();
        var httpClient = Mock.Of<HttpClient>();
        var logger = Mock.Of<ILogger<GraphHelperService>>();

        var graphService = new Mock<GraphHelperService>(openIdConnectOptions, httpClient, logger);

        graphService.Setup(x => x.GetManifestConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppManifestResult() { AppManifest = manifest, Succeeded = true });

        return graphService.Object;
    }
   
}

