using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace EasyAuthForK8s.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args).ConfigureAppConfiguration((ctx, builder) =>
                {

                    builder.AddJsonFile("appsettings.json", false, true);
#if DEBUG
                    builder.AddJsonFile($"appsettings.Development.json", true, true);
#endif
                    builder.AddEnvironmentVariables();
                })
                .UseStartup<Startup>();
        }
    }
}