using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using System.IO;

namespace EasyAuthForK8s.Web
{
    public static class EasyAuthBuilderExtensions
    {
        public static void AddEasyAuthForK8s(this IServiceCollection services, IConfiguration configuration)
        {
            var azureAdConfigSection = configuration.GetSection(Constants.AzureAdConfigSection);
            MicrosoftIdentityOptions microsoftIdentityOptions = new();
            azureAdConfigSection.Bind(microsoftIdentityOptions);
            services.AddSingleton<MicrosoftIdentityOptions>(microsoftIdentityOptions);

            var easyAuthConfig = configuration
                .GetSection(Constants.EasyAuthConfigSection)
                .Get<EasyAuthConfigurationOptions>();

            services.AddSingleton<EasyAuthConfigurationOptions>(easyAuthConfig);

            //for Web applications
            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(o => azureAdConfigSection.Bind(o), c =>
                {
                    c.Cookie.Name = "AzAD.EasyAuthForK8s";
                });

            //configure OIDC options
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, (OpenIdConnectOptions configureOptions) =>
            {
                configureOptions.ResponseType = "code";
                configureOptions.SaveTokens = true;
                configureOptions.Events.OnRedirectToIdentityProvider +=
                    context => OidcHelper.HandleRedirectToIdentityProvider(context, easyAuthConfig, microsoftIdentityOptions);
                configureOptions.Events.OnRemoteFailure += OidcHelper.HandleRemoteFailure;
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
              //TODO Add encryption .ProtectKeysWithCertificate(thumbprint)

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
            //the only middleware that is essential is authentication
            builder.UseAuthentication();
            return builder.UseMiddleware<EasyAuthMiddleWare>();
        }
    }
}
