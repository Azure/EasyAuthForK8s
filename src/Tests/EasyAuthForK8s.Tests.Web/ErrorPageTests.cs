using EasyAuthForK8s.Web.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace EasyAuthForK8s.Tests.Web
{

    public class ErrorPageTests
    {
        [Fact]
        public async Task ErrorPage_Render_OnThrow()
        {
            var error_text = "Really bad awful";
            using IHost host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseExceptionHandler(new ExceptionHandlerOptions() { AllowStatusCode404Response = true, ExceptionHandler = EventHelper.HandleException });
                    app.Run(context =>
                    {
                        throw new Exception(error_text);
                    });
                });
            }).Build();

            await host.StartAsync();
            var response = await host.GetTestServer().CreateClient().GetAsync("/");
            var body = await response.Content.ReadAsStringAsync();
            var match = Regex.Match(body, "<div id=\"error_details\">(.*?)</div>");

            Assert.True(match.Success);
            Assert.Equal(error_text, match.Groups[match.Groups.Count - 1].Value);
        }
    }
}
