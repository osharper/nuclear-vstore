using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

using Autofac.Extensions.DependencyInjection;

using Serilog;

namespace NuClear.VStore.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .ConfigureServices(services => services.AddAutofac())
                   .UseStartup<Startup>()
                   .UseSerilog(dispose: true)
                   .Build();
    }
}
