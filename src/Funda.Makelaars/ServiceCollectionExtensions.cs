using System.Net.Http.Headers;
using Funda.Makelaars.Application;
using Funda.Makelaars.Application.MakelaarRanking;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Funda.Makelaars.Infrastructure.FeedsApi.Mock;
using Funda.Makelaars.Infrastructure.FeedsApi.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Funda.Makelaars;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFundaMakelaars(this IServiceCollection services, bool useMockFeedsApi = false)
    {
        var options = services.AddOptions<FeedsApiOptions>()
            .BindConfiguration(FeedsApiOptions.SectionName)
            .ValidateDataAnnotations();

        var httpClientBuilder = services.AddHttpClient<IFeedsApiClient, FeedsApiClient>((serviceProvider, client) =>
            {
                var feedsApiOptions = serviceProvider.GetRequiredService<IOptions<FeedsApiOptions>>().Value;
                client.BaseAddress = new Uri(feedsApiOptions.BaseUrl.EndsWith('/') ? feedsApiOptions.BaseUrl : feedsApiOptions.BaseUrl + "/");
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddFeedsApiResilience();

        if (useMockFeedsApi)
        {
            // Only the network is replaced: requests still go through FeedsApiClient and the full resilience pipeline.
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
                new MockFeedsApiHandler(Path.Combine(AppContext.BaseDirectory, "MockData")));
        }
        else
        {
            options.Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), $"{FeedsApiOptions.SectionName}:ApiKey is required.");
        }

        services.AddTransient<IListingSource, FeedsApiListingSource>();
        services.AddTransient<IMakelaarRankingService, MakelaarRankingService>();

        return services;
    }
}
