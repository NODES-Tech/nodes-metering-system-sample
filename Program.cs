using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Nodes.API.Http.Client.Support;
using Nodes.API.Models;
using Nodes.API.Queries;

namespace Nodes.MeteringSystem.Sample
{
    internal class Program
    {
        private readonly ServiceProvider _services;

        private static async Task Main(string[] args)
        {
            await new Program().Run();
            
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
            
            //Example 1: DSO Uploads meter readings for AssetGridAssignments, gets them as CSV, and finally deletes them.
            var assetGridAssignmentsToUse = new[]
            {
                "225b356b-e953-4e06-a953-81533dfbd800",
                "3c9e5053-c997-4bc5-8ec0-da876a792bad"
            };
            
            var agaSearchFilters = new List<KeyValuePair<string, IFilter>>
            {
                KeyValuePair.Create(nameof(AssetGridAssignment.Id), OneOfMatcher.OneOf(assetGridAssignmentsToUse))
            };
            
            var assetGridAssignments = (await client.AssetGridAssignments.Search(agaSearchFilters)).Items;
            var start = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0,
                0, 0, 0, TimeSpan.Zero);
            var end = start.AddHours(1);
            var meterReadings = CreateExampleData(assetGridAssignments, start, end).ToList();

            // Insert new meter readings
            await client.MeterReadings.CreateMultiple(meterReadings);

            // Search for existing meter readings for these ids and period
            var mrSearchFilters = new List<KeyValuePair<string, IFilter>>
            {
                KeyValuePair.Create(nameof(MeterReading.AssetGridAssignmentId), OneOfMatcher.OneOf(assetGridAssignmentsToUse)),
                KeyValuePair.Create(nameof(MeterReading.PeriodFrom), new DateTimeRange { StartOnOrAfter = start } as IFilter),
                KeyValuePair.Create(nameof(MeterReading.PeriodTo), new DateTimeRange { EndBefore = end.AddMinutes(10) } as IFilter)
                // KeyValuePair.Create(nameof(MeterReading.PeriodFrom), DateTimeRange.InRange(start, end.AddSeconds(1)))
            };
            var foundMeterReadings = await client.MeterReadings.Search(mrSearchFilters, SearchOptions.Max);

            // Delete meter readings for these ids using the same search filter
            var deletedItemCount = await client.MeterReadings.Delete(mrSearchFilters);
        }

        private IEnumerable<MeterReading> CreateExampleData(
            IReadOnlyCollection<AssetGridAssignment> assetGridAssignments,
            DateTimeOffset from,
            DateTimeOffset to)
        {
            var meterReadings = new List<MeterReading>();
            while (from < to)
            {
                meterReadings.AddRange(assetGridAssignments.Select(aga => new MeterReading
                {
                    AssetGridAssignmentId = aga.Id,
                    PeriodFrom = @from,
                    PeriodTo = @from.AddMinutes(1),
                    AveragePowerProduction = 1
                }));

                from = from.AddMinutes(1);
            }

            return meterReadings;
        }

        private static IConfigurationRoot BuildConfigurationRoot() =>
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();

        private Program() =>
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