using System.Diagnostics;
using System.Net;
using Funda.Makelaars.Application.MakelaarRanking;
using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Funda.Makelaars.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funda.Makelaars.IntegrationTests.Infrastructure.FeedsApi;

public class FeedsApiClientResilienceTests
{
    private const string RateLimitBody = """{"Message":"Request limit exceeded"}""";

    private static readonly SearchQuery Query = new("koop", "/amsterdam/tuin/");

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task GetListings_RateLimitedThenOk_Retries()
    {
        _handler.Enqueue(HttpStatusCode.Unauthorized, RateLimitBody);
        _handler.Enqueue(HttpStatusCode.TooManyRequests, "");
        _handler.Enqueue(HttpStatusCode.OK, """{ "Objects": [], "Paging": { "AantalPaginas": 0 } }""");
        await using var services = CreateServiceProvider(maxRetryAttempts: 2);

        var response = await services.GetRequiredService<IFeedsApiClient>()
            .GetListingsAsync(Query, page: 1, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task GetListings_RateLimitPersists_ThrowsAfterRetries()
    {
        for (var i = 0; i < 3; i++)
        {
            _handler.Enqueue(HttpStatusCode.Unauthorized, RateLimitBody);
        }
        await using var services = CreateServiceProvider(maxRetryAttempts: 2);

        await Assert.ThrowsAsync<FeedsApiRateLimitedException>(
            () => services.GetRequiredService<IFeedsApiClient>().GetListingsAsync(Query, page: 1, CancellationToken.None));
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task GetListings_RequestsPerMinuteLimit_SpacesRequestsEvenly()
    {
        for (var i = 0; i < 4; i++)
        {
            _handler.Enqueue(HttpStatusCode.OK, """{ "Objects": [] }""");
        }
        var stopwatch = Stopwatch.StartNew();
        await using var services = CreateServiceProvider(requestsPerMinute: 600);
        var client = services.GetRequiredService<IFeedsApiClient>();

        for (var page = 1; page <= 4; page++)
        {
            await client.GetListingsAsync(Query, page, CancellationToken.None);
        }

        // 600 per minute = one token every 100 ms; the first request is immediate, the next three wait for a token.
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(250), $"Elapsed {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task GetTopMakelaars_ThroughFullPipeline_PagesAndRanks()
    {
        _handler.Enqueue(HttpStatusCode.OK, """
            {
              "Objects": [
                { "Id": "1", "MakelaarId": 10, "MakelaarNaam": "Alpha" },
                { "Id": "2", "MakelaarId": 20, "MakelaarNaam": "Bravo" }
              ],
              "Paging": { "AantalPaginas": 2, "HuidigePagina": 1 }
            }
            """);
        _handler.Enqueue(HttpStatusCode.OK, """
            {
              "Objects": [
                { "Id": "2", "MakelaarId": 20, "MakelaarNaam": "Bravo" },
                { "Id": "3", "MakelaarId": 20, "MakelaarNaam": "Bravo" }
              ],
              "Paging": { "AantalPaginas": 2, "HuidigePagina": 2 }
            }
            """);
        await using var services = CreateServiceProvider();

        var ranking = await services.GetRequiredService<IMakelaarRankingService>()
            .GetTopMakelaarsAsync(Query, top: 10, CancellationToken.None);

        Assert.Equal([new MakelaarListingCount(20, "Bravo", 2), new MakelaarListingCount(10, "Alpha", 1)], ranking);
        Assert.Equal(
            ["page=1", "page=2"],
            _handler.Requests.Select(r => r.RequestUri!.Query.Split('&').Single(p => p.StartsWith("page="))));
    }

    private ServiceProvider CreateServiceProvider(int maxRetryAttempts = 2, int requestsPerMinute = 60_000)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{FeedsApiOptions.SectionName}:BaseUrl"] = "http://feeds.test/",
                [$"{FeedsApiOptions.SectionName}:ApiKey"] = "test-key",
                [$"{FeedsApiOptions.SectionName}:MaxRetryAttempts"] = maxRetryAttempts.ToString(),
                [$"{FeedsApiOptions.SectionName}:RetryBaseDelay"] = "00:00:00",
                [$"{FeedsApiOptions.SectionName}:RequestsPerMinute"] = requestsPerMinute.ToString(),
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddFundaMakelaars();
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => _handler));

        return services.BuildServiceProvider();
    }
}
