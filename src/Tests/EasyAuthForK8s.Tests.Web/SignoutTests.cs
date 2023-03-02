using EasyAuthForK8s.Tests.Web.Helpers;
using EasyAuthForK8s.Web;
using EasyAuthForK8s.Web.Helpers;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Moq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace EasyAuthForK8s.Tests.Web
{
    public class SignoutTests
    {
        [Fact]
        public async Task CookieDeletedAndRedirectOnSignOut()
        {
            const string login_hint = "myloginhint";
            var options = new EasyAuthConfigurationOptions() { DefaultRedirectAfterSignout = "/testsignout-post-redirect" };
            using var server = await CookieAuthHelper.GetTestServerWithCookieSignedInAsync(options, 
                new () { Claims = { new System.Security.Claims.Claim( Constants.AadClaimParameters.LoginHint, login_hint) } });
            var cookieHttpClient = server.CreateClient();

            //send a dummy request to set the auth cookie
            var authCookie = CookieAuthHelper.GetAuthCookieFromResponse(await cookieHttpClient.GetAsync("foo"));

            Assert.NotNull(authCookie);
            Assert.False(StringSegment.IsNullOrEmpty(authCookie.Value));
            Assert.False(authCookie.Expires.HasValue && authCookie.Expires.Value == DateTimeOffset.MinValue);

            //validate cookie is invalidated after sign out 
            HttpResponseMessage signoutResponse = await GetResponseForSignout(options, authCookie);

            var authCookieSignedOut = CookieAuthHelper.GetAuthCookieFromResponse(signoutResponse);

            Assert.NotNull(authCookie);
            Assert.True(StringSegment.IsNullOrEmpty(authCookieSignedOut.Value));
            Assert.True(authCookieSignedOut.Expires.HasValue && authCookieSignedOut.Expires.Value == DateTimeOffset.UnixEpoch);

            //verify we are redirected for remote signout
            Assert.Equal(HttpStatusCode.Found, signoutResponse.StatusCode);
            Assert.Contains(signoutResponse.Headers, x => x.Key == HeaderNames.Location);

            var oidcOptions = (server.Services.GetService(typeof(IOptionsMonitor<OpenIdConnectOptions>)) as IOptionsMonitor<OpenIdConnectOptions>).Get(OpenIdConnectDefaults.AuthenticationScheme);
            var configuration = await oidcOptions.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);

            var redirectUri = new Uri(signoutResponse.Headers.Single(x => x.Key == HeaderNames.Location).Value.First());
            
            //make sure are are redirecting to the right place
            Assert.Equal(configuration.EndSessionEndpoint, redirectUri.AbsoluteUri.Replace(redirectUri.Query, string.Empty));

            var parameters = HttpUtility.ParseQueryString(redirectUri.Query);

            //make sure the logout hint is provided and matches
            Assert.Equal(login_hint, parameters.Get(Constants.AadClaimParameters.LogoutHint));

            //make sure default post-signout redirect url is used.
            var authProperties = oidcOptions.StateDataFormat.Unprotect(parameters.Get("state"));

            Assert.Equal(options.DefaultRedirectAfterSignout, authProperties.RedirectUri);

            //override the default post-signout redirect
            HttpResponseMessage signoutWithCustomRedirect = await GetResponseForSignout(options, authCookie, "/foo");
            var customRedirectUri = new Uri(signoutWithCustomRedirect.Headers.Single(x => x.Key == HeaderNames.Location).Value.First());
            var customAuthProps = oidcOptions.StateDataFormat.Unprotect(HttpUtility.ParseQueryString(customRedirectUri.Query).Get("state"));

            Assert.Equal("/foo", customAuthProps.RedirectUri);
        }
        private async Task<HttpResponseMessage> GetResponseForSignout(
          EasyAuthConfigurationOptions options,
          SetCookieHeaderValue authCookie,
          string redirectUri = null
          )
        {
            var redirectQuery = string.IsNullOrEmpty(redirectUri) ? string.Empty : $"?{Constants.RedirectParameterName}={HttpUtility.UrlEncode(redirectUri)}";
            TestLogger logger = new TestLogger();

            using IHost host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ILogger<EasyAuthMiddleware>>(logger.Factory().CreateLogger<EasyAuthMiddleware>());
                    services.AddEasyAuthForK8s(TestUtility.GetConfiguration(options), logger.Factory());
                   // services.Replace(new ServiceDescriptor(typeof(GraphHelperService), MockGraphHelperService.Factory()));
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

            var client = host.GetTestServer().CreateClient();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{options.SignoutPath}{redirectQuery}", UriKind.RelativeOrAbsolute));
            request.Headers.Add("Cookie", $"{authCookie.Name}={authCookie.Value}");
            return await client.SendAsync(request);
        }
    }
}
