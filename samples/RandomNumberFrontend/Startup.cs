using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static RandomNumberGenerator.RandomNumberGenerator;

namespace RandomNumberFrontend
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            var serviceUrl = Configuration["RngServiceUrl"];
            var channel = GrpcChannel.ForAddress(serviceUrl);
            var client = new RandomNumberGeneratorClient(channel);
            logger.LogInformation("Using RNG Service at: {RngServiceUrl}", serviceUrl);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });

                endpoints.MapGet("/rando", async context =>
                {
                    var min = int.TryParse(context.Request.Query["min"], out var m1) ? m1 : 0;
                    var max = int.TryParse(context.Request.Query["max"], out var m2) ? m2 : 100;
                    logger.LogInformation("Requesting new random number between {Min} and {Max} from {Peer}", min, max, channel.Target);
                    var result = await client.GetRandomNumberAsync(new RandomNumberGenerator.RandomNumberRequest()
                    {
                        Min = min,
                        Max = max,
                    });
                    await context.Response.WriteAsync($"Random number between {min} and {max}: {result.Result}");
                });
            });
        }
    }
}
