using EasyAuthForK8s.Tests.Web.Helpers;
using EasyAuthForK8s.Web;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EasyAuthForK8s.Tests.Web
{
    public class SignoutTests
    {
        [Fact]
        public async Task CookieDeletedAndRedirectOnSignOut()
        {
            var options = new EasyAuthConfigurationOptions();
            using var server = await CookieAuthHelper.GetTestServerWithCookieSignedInAsync(options);
            var client = server.CreateClient();

            //send a dummy request to set the auth cookie
            HttpResponseMessage response = await client.GetAsync("foo");
            
            //validate initial cookie
            Assert.Contains(response.Headers, x => x.Key == HeaderNames.SetCookie);

            var cookiesSignedIn = SetCookieHeaderValue.ParseList(response.Headers.Single(x => x.Key == HeaderNames.SetCookie).Value.ToList());
            Assert.Contains(cookiesSignedIn, x => x.Name == Constants.CookieName);

            var authCookie = cookiesSignedIn.Single(x => x.Name == Constants.CookieName);

            Assert.False(StringSegment.IsNullOrEmpty(authCookie.Value));
            Assert.False(authCookie.Expires.HasValue && authCookie.Expires.Value == DateTimeOffset.MinValue);

            //validate cookie is invalidated after sign out 
            HttpResponseMessage signoutResponse = await client.GetAsync(options.SignoutPath);
            Assert.Contains(signoutResponse.Headers, x => x.Key == HeaderNames.SetCookie);

            var cookiesSignedOut = SetCookieHeaderValue.ParseList(signoutResponse.Headers.Single(x => x.Key == HeaderNames.SetCookie).Value.ToList());
            Assert.Contains(cookiesSignedOut, x => x.Name == Constants.CookieName);
            
            var authCookieSignedOut = cookiesSignedOut.Single(x => x.Name == Constants.CookieName);

            Assert.True(StringSegment.IsNullOrEmpty(authCookieSignedOut.Value));
            Assert.True(authCookieSignedOut.Expires.HasValue && authCookieSignedOut.Expires.Value == DateTimeOffset.UnixEpoch);

            //verify we are redirected for remote signout
            Assert.Equal(HttpStatusCode.Found, signoutResponse.StatusCode);
            Assert.Contains(signoutResponse.Headers, x => x.Key == HeaderNames.Location);

            var oidcOptions = (server.Services.GetService(typeof(IOptionsMonitor<OpenIdConnectOptions>)) as IOptionsMonitor<OpenIdConnectOptions>).Get(OpenIdConnectDefaults.AuthenticationScheme);
            var configuration = await oidcOptions.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);

            var redirectUri = new Uri(signoutResponse.Headers.Single(x => x.Key == HeaderNames.Location).Value.First());
            
            //make sure are are redirecting to the right place
            Assert.Equal(redirectUri.AbsoluteUri.Replace(redirectUri.Query, string.Empty), configuration.EndSessionEndpoint);

            //TODO: add test for post-signout redirect

        }
    }
}
