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
        }

        private async Task Run()
        {
            // This example illustrates how a DSO can upload/search for/delete meter readings for
            // AssetGridAssignments using the Nodes.API.Http.Client nuget package to integrate with NODES.
            
            var client = _services.GetRequiredService<NodesClient>();
            
            // Requirement:
            // For a DSO to be allowed to provide meter readings for an asset grid assignment, the FSP
            // need to have those assets organized in portfolios where NODES is set as Meter Readings source
            // Also, "Aggregate meter readings from assets" checked on the portfolio(s) - i.e. the FSP has configured
            // the portfolio(s) to accept meter readings on the individual assets/asset grid assignments and let
            // NODES aggregate those values up to portfolio level.
            var assetGridAssignmentsToUse = new[]
            {
                "636b466f-6c15-449f-859b-7c1971c0735f"
            };

            // Search for the assetGridAssignments with the IDs provided
            var agaSearchFilters = new List<KeyValuePair<string, IFilter>>
            {
                KeyValuePair.Create(nameof(AssetGridAssignment.Id), OneOfMatcher.OneOf(assetGridAssignmentsToUse))
            };
            var assetGridAssignments = (await client.AssetGridAssignments.Search(agaSearchFilters)).Items;

            // Create some "dummy" meter readings to have some data to insert
            var start = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0,
                0, 0, 0, TimeSpan.Zero);
            var end = start.AddHours(1);
            var meterReadings = CreateExampleData(assetGridAssignments, start, end).ToList();

            // Insert the new meter readings. This can be for one or for multiple asset grid assignments. If there already is
            // existing data for those IDs, validation will fail with an error message. In other words, existing data will not
            // be replaced. If overwriting data is the intent, the old meter readings must first be deleted.
            await client.MeterReadings.CreateMultiple(meterReadings);

            // Create set of search filters to search for the newly inserted data
            var mrSearchFilters = new List<KeyValuePair<string, IFilter>>
            {
                KeyValuePair.Create(nameof(MeterReading.AssetGridAssignmentId), OneOfMatcher.OneOf(assetGridAssignmentsToUse)),
                KeyValuePair.Create(nameof(MeterReading.PeriodFrom), new DateTimeRange { StartOnOrAfter = start } as IFilter),
                KeyValuePair.Create(nameof(MeterReading.PeriodTo), new DateTimeRange { EndBefore = end.AddMinutes(10) } as IFilter)
            };

            // ...then search for the data using the searchFilter. Search results in NODES will have a max amount of hits. Even though
            // it is not needed for the small amount of data in this example, using Take and Skip to do multiple searches as shown below
            // is recommended to be sure that ALL items matching the search is fetched.
            const int take = 100;
            var skip = 0;
            SearchResult<MeterReading> searchResult;
            var meterReadingsFound = new List<MeterReading>();
            do
            {
                searchResult = await client.MeterReadings.Search(mrSearchFilters, new SearchOptions
                {
                    Take = take,
                    Skip = skip,
                    OrderBy = { "PeriodFrom" }
                });
                meterReadingsFound.AddRange(searchResult.Items);
                skip += take;
            } while (searchResult.Items.Count > 0 && searchResult.Items.Count >= take);


            // Delete meter readings for these ids using the same search filter
            var deletedItemCount = await client.MeterReadings.Delete(mrSearchFilters);
        }

        private static IEnumerable<MeterReading> CreateExampleData(
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