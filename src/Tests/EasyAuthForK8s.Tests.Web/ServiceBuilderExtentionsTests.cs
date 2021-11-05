using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using EasyAuthForK8s.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace EasyAuthForK8s.Tests.Web
{
    public class ServiceBuilderExtentionsTests
    {
        [Fact]
        public void AddEasyAuthBuilderFromEnvironment()
        {
            var services = new ServiceCollection();
            var configuration = GetConfigurationFromEnvironment();
            var loggerFactory = new TestLogger.TestLoggerFactory();

            services.AddEasyAuthForK8s(configuration, loggerFactory);

            Assert.Contains(services, s => s.ServiceType == typeof(IOptions<EasyAuthConfigurationOptions>));
            Assert.Contains(services, s => s.ServiceType == typeof(IAuthenticationService));
            Assert.Contains(services, s => s.ServiceType == typeof(IDataProtectionProvider));
            Assert.Contains(services, s => s.ServiceType == typeof(IAuthorizationService));

            var provider = services.BuildServiceProvider();
            
            var options = provider.GetService<IOptions<EasyAuthConfigurationOptions>>();
            Assert.Equal("/mnt/environment", options.Value.DataProtectionFileLocation);

            //trying to get at the key management location
            //services.PostConfigure<KeyManagementOptions>((KeyManagementOptions configureOptions) =>
            //{
            //    var repo = configureOptions.XmlRepository as FileSystemXmlRepository;
            //    //Assert.Equal("/mnt/environment", repo.Directory.
            //});

            Assert.True(loggerFactory.Logger.Messages.Count > 0);
            Assert.DoesNotContain(loggerFactory.Logger.Messages, x => x.LogLevel == LogLevel.Warning || x.LogLevel == LogLevel.Error);
        }

        private IConfiguration GetConfiguration()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "AzureAd:Scopes", "User.Read" },
                    { "EasyAuthForK8s:DataProtectionFileLocation", "/mnt/config" }
                });

            return builder.Build();
        }
        private IConfiguration GetConfigurationFromEnvironment()
        {
            Environment.SetEnvironmentVariable("AzureAd__Scopes", "User.Read");
            Environment.SetEnvironmentVariable("EasyAuthForK8s__DataProtectionFileLocation", "/mnt/environment");
            var builder = new ConfigurationBuilder();
            builder.AddEnvironmentVariables();
            return builder.Build();
        }
    }

}
