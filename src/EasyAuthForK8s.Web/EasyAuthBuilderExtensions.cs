using EasyAuthForK8s.Web.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace EasyAuthForK8s.Web;

public static class EasyAuthBuilderExtensions
{
    public static void AddEasyAuthForK8s(this IServiceCollection services, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger("EasyAuthForK8s.Web.EasyAuthBuilderExtensions");

        IConfigurationSection azureAdConfigSection = configuration.GetSection(Constants.AzureAdConfigSection);
        MicrosoftIdentityOptions microsoftIdentityOptions = new();
        azureAdConfigSection.Bind(microsoftIdentityOptions);
        services.AddSingleton<IOptions<MicrosoftIdentityOptions>>(Options.Create(microsoftIdentityOptions));

        EasyAuthConfigurationOptions easyAuthConfig = configuration
            .GetSection(Constants.EasyAuthConfigSection)
            .Get<EasyAuthConfigurationOptions>();

        services.AddSingleton<IOptions<EasyAuthConfigurationOptions>>(Options.Create(easyAuthConfig));

        logger.LogInformation($"Initializing services and middleware.  Configuration: AuthPath={easyAuthConfig.AuthPath}, SigninPath={easyAuthConfig.SigninPath}, AllowBearerToken={easyAuthConfig.AllowBearerToken}, DataProtectionFileLocation={easyAuthConfig.DataProtectionFileLocation}");


        //for Web applications
        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(async o =>
            {
                azureAdConfigSection.Bind(o);
            },
            async c =>
            {
                c.Cookie.Name = Constants.CookieName;
                c.Events.OnSigningIn += async context => await EventHelper.CookieSigningIn(context, easyAuthConfig);
            });

        //configure OIDC options
        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, (OpenIdConnectOptions configureOptions) =>
        {
            ILogger<EventHelper> logger = loggerFactory.CreateLogger<EventHelper>();

            configureOptions.ResponseType = "code";
            configureOptions.SaveTokens = true;

            configureOptions.Events.OnRedirectToIdentityProvider +=
                async context => await EventHelper.HandleRedirectToIdentityProvider(context, easyAuthConfig, microsoftIdentityOptions, logger);

            configureOptions.Events.OnRemoteFailure +=
                async context => await EventHelper.HandleRemoteFailure(context, logger);
        });


        //add bearer token support if allowed
        if (easyAuthConfig.AllowBearerToken)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(b => b.SaveToken = true, o => azureAdConfigSection.Bind(o));
        }
        //add authorization
        services.AddAuthorization();
        //add authz policies to distinguish between application types
        //services.AddAuthorization(options =>
        //{
        //    options.AddPolicy("api", policy =>
        //    {
        //        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        //        policy.RequireAuthenticatedUser();
        //    });
        //    options.AddPolicy("web", policy =>
        //    {
        //        policy.AuthenticationSchemes.Add(OpenIdConnectDefaults.AuthenticationScheme);
        //        policy.RequireAuthenticatedUser();
        //    });
        //});

        //required to ensure consistency across multiple nodes
        services.AddDataProtection()
          .PersistKeysToFileSystem(new DirectoryInfo(easyAuthConfig.DataProtectionFileLocation));
        //TODO .ProtectKeysWithCertificate(thumbprint) if we want cookies to remain valid across helm deployments

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }
    public static IApplicationBuilder UseEasyAuthForK8s(
         this IApplicationBuilder builder)
    {
        //needed to ensure redirect to IdP always returns via https
        ForwardedHeadersOptions options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        builder.UseForwardedHeaders(options);
        builder.Use(async (context, next) =>
        {
            context.Request.Scheme = "https";
            await next.Invoke();
        });

        //the only middleware that is essential is authentication
        builder.UseAuthentication();

        //we don't use the authentication middleware, which would perform its own check here,
        //so we need to ensure the authorization service was loaded in ConfigureServices
        if (builder.ApplicationServices.GetService(typeof(IAuthorizationService)) == null)
        {
            throw new InvalidOperationException("IAuthorization service was not found in the service collection. Call to servcies.AddEasyAuthForK8s() is required in ConfigureServices.");
        }

        return builder.UseMiddleware<EasyAuthMiddleware>();
    }

    /// <summary>
    /// Warns if Set-Cookie from OIDC callback is larger than  We have to register this separately so the event callback is created
    /// before the OIDC handler runs;
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="warnSizeBytes">header size in bytes that will trigger log warning</param>
    /// <returns></returns>
    public static IApplicationBuilder UseLargeSetCookieLogWarning(
         this IApplicationBuilder builder, uint warnSizeBytes)
    {
        var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>();
        var aadOptions = builder.ApplicationServices.GetService<IOptions<MicrosoftIdentityOptions>>();

        return builder.Use(async (context, next) =>
        {
            //if this is the oidc callback where we set the auth cookie,
            //calculate the cookie header size and log warning if large
            if (aadOptions.Value.CallbackPath == context.Request.Path)
            {
                var logger = loggerFactory.CreateLogger("EasyAuthForK8s.Web");
                context.Response.OnCompleted(async (state) =>
                {

                    var response = state as HttpResponse;
                    try
                    {
                        // this is really just back-of-the-napkin math.  With compression and/or non-ascii encoding,
                        // and any other run-time unknowns this may not be entirely accurate, but it should serve
                        // as a useful trouble-shooting tool if the ingress controller starts throwing
                        if (response.Headers.ContainsKey(HeaderNames.SetCookie))
                        {
                            var length = response.Headers[HeaderNames.SetCookie].Sum(c => Encoding.ASCII.GetByteCount(c));

                            if (length >= warnSizeBytes)
                                logger.LogWarning($"Large Set-Cookie response header detected.  Total size = {length} bytes, " +
                                    "check the configured limits on the ingress controller to ensure this is acceptable " +
                                    "or try to reduce the cookie payload");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error calculating Set-Cookie response header size");
                    }
                }, context.Response);
            }
           
            await next.Invoke();
        });
    }
}

