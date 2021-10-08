using System;
using System.IO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Logging;


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
            MicrosoftIdentityOptions options = Configuration.GetSection(AzureAdConfigSection).Get<MicrosoftIdentityOptions>();
            
            services.AddSingleton<MicrosoftIdentityOptions>(options);

            //for Api applications
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(Configuration.GetSection(AzureAdConfigSection));
            
            //for Web applications
            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(Configuration.GetSection(AzureAdConfigSection));

            //configure bearer options
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, (configureOptions) =>
            {
                configureOptions.SaveToken = true;
            });

            //configure OIDC options
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, (configureOptions) =>
            {
                configureOptions.ResponseType = "code";
                configureOptions.SaveTokens = true;
            });

            //add authz policies to distinguish between application types
            services.AddAuthorization(options =>
            {
                options.AddPolicy("api", policy =>
                {
                    policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });
                options.AddPolicy("web", policy =>
                {
                    policy.AuthenticationSchemes.Add(OpenIdConnectDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });
            });

            //required to ensure consistency across multiple nodes
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Configuration["DataProtectionFileLocation"]));

            services.AddControllers();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 2;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddRazorPages()
                .AddMicrosoftIdentityUI();

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
            //rewriting the url so that we don't have to add an ingress route for the MicrosoftIdentity area
            app.Use(async (context, next) =>
            {
                var url = context.Request.Path.Value;

                // Rewrite to index
                if (url.StartsWith("/msal/MicrosoftIdentity"))
                {
                    // rewrite and continue processing
                    context.Request.Path = url.Substring(5);
                }

                await next();
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();

                });

        }


    }
}