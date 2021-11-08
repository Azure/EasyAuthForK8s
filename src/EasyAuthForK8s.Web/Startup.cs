using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAuthForK8s.Web;

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
        app.UseHealthChecks("/");

        // log warnings for size that might cause NGINX problems.  Must add 
        // this middleware before EasyAuth
        app.UseLargeSetCookieLogWarning(4096);
        
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

        
        app.UseEasyAuthForK8s();

    }
}
