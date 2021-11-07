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

namespace EasyAuthForK8s.Tests.Web
{
    public class EasyAuthMiddlewareTests
    {
        [Theory]
        [InlineData("/testauth", "", "Requires an authenticated user.")]
        [InlineData("/testauth", "?role=foo", "User.IsInRole must be true for one of the following roles: (foo)")]
        public async Task Invoke_HandleAuth_Unauthenticated(string path, string query, string containsMessage)
        {
            EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions() { AuthPath = path };
            TestLogger.TestLoggerFactory loggerFactory = new TestLogger.TestLoggerFactory();

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

            await host.StartAsync();

            System.Net.Http.HttpResponseMessage response = await host.GetTestServer().CreateClient().GetAsync(string.Concat(path, query));

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Contains(containsMessage, responseBody);
            Assert.True(response.Headers.Contains(HeaderNames.SetCookie));
            Assert.True(CookieHeaderValue.TryParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList(), out IList<CookieHeaderValue> cookieValues));
            Assert.Contains(cookieValues, cookieValues => cookieValues.Name == Constants.StateCookieName);

            CookieHeaderValue cookieHeader = cookieValues.First(x => x.Name == Constants.StateCookieName);
            Assert.True(cookieHeader.Value.HasValue);

            IDataProtector dp = host.Services.GetService<IDataProtectionProvider>()
                .CreateProtector(Constants.StateCookieName);

            EasyAuthState state = JsonSerializer.Deserialize<EasyAuthState>(dp.Unprotect(cookieHeader.Value.Value));
            Assert.NotNull(state);
            Assert.Equal(EasyAuthState.AuthStatus.Unauthenticated, state.Status);
            Assert.Contains(containsMessage, state.Msg);

            Assert.Contains(loggerFactory.Logger.Messages, x => x.Message.Contains(containsMessage));
        }

        [Theory]
        [InlineData("/testauth", "?scope=foo", "ScopeAuthorizationRequirement:Scope= and `scp` or `http://schemas.microsoft.com/identity/claims/scope` is one of the following values: (foo)", new string[] { "foo" })]
        public async Task Invoke_HandleAuth_UnauthorizedWithScopes(string path, string query, string containsMessage, string[] scopes)
        {
            EasyAuthConfigurationOptions options = new EasyAuthConfigurationOptions() { AuthPath = path };
            TestLogger.TestLoggerFactory loggerFactory = new TestLogger.TestLoggerFactory();

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
                    .Configure(app => {
                        app.UseEasyAuthForK8s();
                    });
                }).Build();

            await host.StartAsync();

            System.Net.Http.HttpResponseMessage response = await host.GetTestServer().CreateClient().GetAsync(string.Concat(path, query));

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Contains(containsMessage, responseBody);
            Assert.True(response.Headers.Contains(HeaderNames.SetCookie));
            Assert.True(CookieHeaderValue.TryParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList(), out IList<CookieHeaderValue> cookieValues));
            Assert.Contains(cookieValues, cookieValues => cookieValues.Name == Constants.StateCookieName);

            CookieHeaderValue cookieHeader = cookieValues.First(x => x.Name == Constants.StateCookieName);
            Assert.True(cookieHeader.Value.HasValue);

            IDataProtector dp = host.Services.GetService<IDataProtectionProvider>()
                .CreateProtector(Constants.StateCookieName);

            EasyAuthState state = JsonSerializer.Deserialize<EasyAuthState>(dp.Unprotect(cookieHeader.Value.Value));
            Assert.NotNull(state);
            Assert.Equal(EasyAuthState.AuthStatus.Unauthorized, state.Status);
            Assert.Contains(containsMessage, state.Msg);
            Assert.Equal(state.Scopes, scopes.ToList());

            Assert.Contains(loggerFactory.Logger.Messages, x => x.Message.Contains(containsMessage));
        }

        private IConfiguration GetConfiguration(EasyAuthConfigurationOptions options)
        {
            return new ConfigurationBuilder()
                .AddJsonFile("testsettings.json", false, true)
                .Add(new EasyAuthOptionsConfigurationSource(options))
                .Build();
        }
    }
}
