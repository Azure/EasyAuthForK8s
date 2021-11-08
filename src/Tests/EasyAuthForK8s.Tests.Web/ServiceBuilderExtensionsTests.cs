using EasyAuthForK8s.Tests.Web.Helpers;
using EasyAuthForK8s.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using Xunit;

namespace EasyAuthForK8s.Tests.Web;

public class ServiceBuilderExtensionsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddEasyAuthBuilderFromEnvironment(bool environment)
    {
        ServiceCollection services = new ServiceCollection();
        IConfiguration configuration = environment ? GetConfigurationFromEnvironment() : GetConfiguration();
        TestLogger.TestLoggerFactory loggerFactory = new TestLogger.TestLoggerFactory();

        string dpPath = "";

        services.AddEasyAuthForK8s(configuration, loggerFactory);

        //test that essential services are created
        Assert.Contains(services, s => s.ServiceType == typeof(IOptions<EasyAuthConfigurationOptions>));
        Assert.Contains(services, s => s.ServiceType == typeof(IOptions<MicrosoftIdentityOptions>));
        Assert.Contains(services, s => s.ServiceType == typeof(IAuthenticationService));
        Assert.Contains(services, s => s.ServiceType == typeof(IDataProtectionProvider));
        Assert.Contains(services, s => s.ServiceType == typeof(IAuthorizationService));

        services.PostConfigure<KeyManagementOptions>((KeyManagementOptions configureOptions) =>
        {
            //get the downstream config so we can see that the options cascaded properly
            FileSystemXmlRepository repo = configureOptions.XmlRepository as FileSystemXmlRepository;
            dpPath = repo.Directory.ToString();
        });

        ServiceProvider provider = services.BuildServiceProvider();

        IOptions<EasyAuthConfigurationOptions> options = provider.GetService<IOptions<EasyAuthConfigurationOptions>>();
        Assert.Equal("/mnt/foo", options.Value.DataProtectionFileLocation);

        //materialize data protection service to force PostConfigure above to run
        IDataProtectionProvider dp = provider.GetService<IDataProtectionProvider>();
        Assert.Equal("/mnt/foo", dpPath);

        Assert.True(loggerFactory.Logger.Messages.Count > 0);
        Assert.DoesNotContain(loggerFactory.Logger.Messages, x => x.LogLevel == LogLevel.Warning || x.LogLevel == LogLevel.Error);
    }

    private IConfiguration GetConfiguration()
    {
        ConfigurationBuilder builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(
            new Dictionary<string, string>()
            {
                    { "AzureAd:Scopes", "User.Read" },
                    { "EasyAuthForK8s:DataProtectionFileLocation", "/mnt/foo" }
            });

        return builder.Build();
    }
    private IConfiguration GetConfigurationFromEnvironment()
    {
        Environment.SetEnvironmentVariable("AzureAd__Scopes", "User.Read");
        Environment.SetEnvironmentVariable("EasyAuthForK8s__DataProtectionFileLocation", "/mnt/foo");
        ConfigurationBuilder builder = new ConfigurationBuilder();
        builder.AddEnvironmentVariables();
        return builder.Build();
    }
}


