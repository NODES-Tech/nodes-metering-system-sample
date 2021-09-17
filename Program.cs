using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Nodes.API.Http.Client.Support;

namespace Nodes.MeteringSystem.Sample
{
    class Program
    {
        private ServiceProvider _services;

        static async Task Main(string[] args)
        {
            await new Program().Run();
            // DSO example:
            
            //set ut authorization
            // Get asset grid assignments
            // create meter reading sample values
            // Save to NODES
            // Get data as CSV
            // Delete data
            
            // FSP example:
            //set ut authorization
            // Get assets
            // Get portfolios, check that aggregation is on
            // create meter reading sample values
            // Save to NODES
            // Get data as CSV
            // Delete data
        }

        private async Task Run()
        {
            var client = _services.GetRequiredService<NodesClient>();
            var me = await client.Users.GetCurrentUser();
            Console.WriteLine(me);
        }

        public static IConfigurationRoot BuildConfigurationRoot() =>
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();            
        
        public Program() =>
            _services = new ServiceCollection()

                // Read appsettings and appsettings. Customize if needed.
                .AddSingleton<IConfiguration>(BuildConfigurationRoot())

                // In cases where the server provides invalid/self-signed SSL certificates, e.g. localhost 
                // or certain corporate / educational environments, add a dummy validator: 
                .AddSingleton<HttpMessageHandler>(
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true
                    })
                .AddSingleton<HttpClient, HttpClient>()

                // This token provider will get a token from the B2C server, using clientid/secret found in appsettings*.json
                // We recommend creating a new file appsettings.local.json and adding your values there. 
                // See TokenProvider for details. 
                .AddSingleton<ITokenProvider, TokenProvider>()
                .AddSingleton<HttpUtils>()

                // The APIUrl is specified in appsettings.json or appsettings.local.json
                // The rest of the parameters to NodesClient will be fetched from the service collection on instantiation. 
                .AddSingleton(x =>
                    ActivatorUtilities.CreateInstance<NodesClient>(x,
                        x.GetRequiredService<IConfiguration>().GetSection("APIUrl").Value))
                .BuildServiceProvider();
        
    }
}