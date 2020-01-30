using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace OCP.Msal.Proxy.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        public const string AzureAdConfigSection = "AzureAd";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            AzureADOptions azureAdOptions = Configuration.GetSection(AzureAdConfigSection).Get<AzureADOptions>();
            services.AddSingleton<AzureADOptions>(azureAdOptions);
            services.Configure<CookiePolicyOptions>(options => { options.MinimumSameSitePolicy = SameSiteMode.None; });

            //services.AddHttpContextAccessor();


            services.AddAuthentication()
                // allow Bearer tokens for non-interactive applications
                .AddAzureADBearer(options => Configuration.Bind(AzureAdConfigSection, options))
                // web applications
                .AddAzureAD(options => Configuration.Bind(AzureAdConfigSection, options));

            //configure bearer options
            services.Configure<JwtBearerOptions>(AzureADDefaults.JwtBearerAuthenticationScheme, (configureOptions) =>
            {
                configureOptions.Authority += "/v2.0";
            });

            //configure OIDC options
            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, (configureOptions) =>
            {
                configureOptions.Authority += "/v2.0";
                configureOptions.ResponseType = "code";
            });

            //add authz policies to distinguish between application types
            services.AddAuthorization(options =>
            {
                options.AddPolicy("api", policy =>
                {
                    policy.AuthenticationSchemes.Add(AzureADDefaults.JwtBearerAuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });
                options.AddPolicy("web", policy =>
                {
                    policy.AuthenticationSchemes.Add(AzureADDefaults.OpenIdScheme);
                    policy.RequireAuthenticatedUser();
                });
            });

            //required to ensure consistency across multiple nodes
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Configuration["DataProtectionFileLocation"]));

            
            services.AddMvc(options => options.EnableEndpointRouting = false)
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 2;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });



        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                IdentityModelEventSource.ShowPII = true;
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Msal/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            if (Convert.ToBoolean(Configuration["ForceHttps"]))
            {
                var options = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                };
                app.UseForwardedHeaders(options);

                app.Use(async (context, next) =>
                {
                    context.Request.Scheme = "https";
                    await next.Invoke();
                });
            }

            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseMvc();

        }


    }
}