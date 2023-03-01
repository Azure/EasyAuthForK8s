using EasyAuthForK8s.Web;
using EasyAuthForK8s.Web.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace EasyAuthForK8s.Tests.Web.Helpers
{
    public class CookieAuthHelper
    {
        public static async Task<TestServer> GetTestServerWithCookieSignedInAsync(
          EasyAuthConfigurationOptions options,
          TestAuthenticationHandlerOptions handlerOptions = null)
        {
            IHost host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddEasyAuthForK8s(TestUtility.GetConfiguration(options), new TestLogger().Factory());
                    if (handlerOptions != null)
                        services.AddSingleton<TestAuthenticationHandlerOptions>(handlerOptions);

                    services.Replace(new ServiceDescriptor(typeof(GraphHelperService), MockGraphHelperService.Factory()));
                })
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            var token = new JwtSecurityToken("eyJhbGciOiJub25lIn0.eyJpc3MiOiJqb24ifQ.");
                            AuthenticationProperties props = new AuthenticationProperties(
                                new Dictionary<string, string>() {
                                { Constants.OidcGraphQueryStateBag, "foo" },
                                { ".Token.access_token", new JwtSecurityTokenHandler().WriteToken(token) },
                                { ".Token.id_token", new JwtSecurityTokenHandler().WriteToken(token) }
                                });

                            var signedIn = false;
                            var cookieValue = "";

                            //inject resultant cookie from the response back into to the request
                            var cookies = new Mock<IRequestCookieCollection>();
                            cookies.Setup(x => x[Constants.CookieName]).Returns(() =>
                            {
                                //force the sign in logic to run, which will execute graph queries
                                //should only run once
                                if (!signedIn)
                                {
                                    signedIn = true;

                                    context.SignInAsync(
                                        CookieAuthenticationDefaults.AuthenticationScheme,
                                        new TestAuthenticationHandler().AuthenticateAsync().Result.Principal,
                                        props).Wait();

                                    var cookies = CookieHeaderValue.ParseList(context.Response.Headers.SetCookie);

                                    cookieValue = cookies
                                        .Where(x => x.Name == Constants.CookieName)
                                        .Select(x => x.Value)
                                        .First()
                                        .ToString();

                                }
                                return cookieValue;
                            }
                            );
                            cookies.Setup(x => x.ContainsKey(Constants.CookieName)).Returns(true);
                            context.Request.Cookies = cookies.Object;

                            await next.Invoke();
                        });
                        app.UseEasyAuthForK8s();
                    });
                }).Build();

            await host.StartAsync();

            return host.GetTestServer();

        }
        public static CookieCollection GetCookiesFromResponseMessage(HttpResponseMessage response)
        {
            CookieContainer cookieContainer = new CookieContainer();
            var uri = new Uri("http://localhost");
            var setCookie = response.Headers.Where(x => x.Key == HeaderNames.SetCookie).Single();
            foreach(var value in setCookie.Value)
            {
                cookieContainer.SetCookies(uri, value);
            }
            return cookieContainer.GetAllCookies();
        }
    }
}
