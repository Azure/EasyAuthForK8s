using EasyAuthForK8s.Web.Helpers;
using EasyAuthForK8s.Web.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyAuthForK8s.Tests.Web.Helpers
{
    public class MockGraphHelperService
    {
        public static GraphHelperService Factory(ILogger logger = null)
        {
            var manifest = new AppManifest()
            {
                appId = TestUtility.DummyGuid,
                publishedPermissionScopes = new()
            {
                new() { value = "foo" },
                new() { value = "bar" }
            },
                oidcScopes = new string[]
                {
                "openid",
                "profile",
                "email",
                "offline_access"
                }
            };

            var openIdConnectOptions = Mock.Of<IOptionsMonitor<OpenIdConnectOptions>>();
            var httpClient = Mock.Of<HttpClient>();
            logger = logger ?? Mock.Of<ILogger<GraphHelperService>>();

            var graphService = new Mock<GraphHelperService>(openIdConnectOptions, httpClient, logger);

            graphService.Setup(x => x.GetManifestConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AppManifestResult() { AppManifest = manifest, Succeeded = true });

            graphService.Setup(x => x.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var data = new List<string>();
                    GraphHelperService.ExtractGraphResponse(data, File.OpenRead("./Helpers/sample-graph-result.json"), logger).Wait();
                    return data;
                });

            return graphService.Object;
        }
    }
}
