using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using EasyAuthForK8s.Web;

namespace EasyAuthForK8s.Tests.Web
{
    public class EasyAuthMiddlewareTests
    {
        [Fact]
        public void ThrowFriendlyErrorWhenServicesNotRegistered()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseEasyAuthForK8s();
                    });
                }).Build();

            var ex = Assert.Throws<InvalidOperationException>(() => host.Start());

            Assert.Equal("IAuthorization service was not found in the service collection. Call to servcies.AddEasyAuthForK8s() is required in ConfigureServices.", ex.Message);
        }
    }
}
