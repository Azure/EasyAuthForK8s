using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAuthForK8s.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            Configuration = configuration;
            LoggerFactory = loggerFactory;
        }
        public IConfiguration Configuration { get; }
        public ILoggerFactory LoggerFactory { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddEasyAuthForK8s(Configuration, LoggerFactory);

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

            //call easyauth after healthchecks since it requires additional security configuration
            app.UseEasyAuthForK8s();

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapHealthChecks("/");
            //    endpoints.MapControllers();
            //});


        }


    }
}