using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace EasyAuthForK8s.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddEasyAuthForK8s(Configuration);
            
            //var configSection = Configuration.GetSection(AzureAdConfigSection);
            //MicrosoftIdentityOptions options = new MicrosoftIdentityOptions();
            //configSection.Bind(options);
            
           

            //services.AddControllers();
            services.AddHealthChecks();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            //if (env.IsDevelopment())
            //{
            //    IdentityModelEventSource.ShowPII = true;
            //    app.UseDeveloperExceptionPage();
            //}
            //else
            //{
            //    app.UseStatusCodePages();
            //    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            //    app.UseHsts();
            //}


            //if (Convert.ToBoolean(Configuration["ForceHttps"]))
            //{
            //    var options = new ForwardedHeadersOptions
            //    {
            //        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            //    };
            //    app.UseForwardedHeaders(options);

            //    app.Use(async (context, next) =>
            //    {
            //        context.Request.Scheme = "https";
            //        await next.Invoke();
            //    });
            //}

            app.UseHealthChecks("/");
            //app.UseRouting();
            //app.UseAuthentication();
            //app.UseAuthorization();
            app.UseEasyAuthForK8s();

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapHealthChecks("/");
            //    endpoints.MapControllers();
            //});


        }


    }
}