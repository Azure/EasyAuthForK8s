using EasyAuthForK8s.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System;
using Xunit;

namespace EasyAuthForK8s.Tests.Web;

public class AppBuilderExtensionsTests
{
    [Fact]
    public void ThrowWhenRequiredServicesNotRegistered()
    {
        using IHost host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseEasyAuthForK8s();
                });
            }).Build();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => host.Start());

        Assert.Equal("IAuthorization service was not found in the service collection. Call to services.AddEasyAuthForK8s() is required in ConfigureServices.", ex.Message);
    }
}
