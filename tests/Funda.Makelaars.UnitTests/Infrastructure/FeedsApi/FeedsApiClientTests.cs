using System.Net;
using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Funda.Makelaars.Infrastructure.FeedsApi.Contracts;
using Funda.Makelaars.Tests.Shared;
using Microsoft.Extensions.Options;

namespace Funda.Makelaars.UnitTests.Infrastructure.FeedsApi;

public class FeedsApiClientTests
{
    private const string ApiKey = "test-key";

    private static readonly SearchQuery Query = new("koop", "/amsterdam/tuin/");

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task GetListings_Success_BuildsUrlAndParsesResponse()
    {
        _handler.Enqueue(HttpStatusCode.OK, """
            {
              "Objects": [{ "Id": "abc", "MakelaarId": 42, "MakelaarNaam": "Alpha Makelaars" }],
              "Paging": { "AantalPaginas": 3, "HuidigePagina": 2 },
              "TotaalAantalObjecten": 51
            }
            """);

        var response = await CreateClient().GetListingsAsync(Query, page: 2, CancellationToken.None);

        Assert.Equal(
            $"http://feeds.test/json/{ApiKey}/?type=koop&zo=/amsterdam/tuin/&page=2&pagesize=25",
            _handler.Requests.Single().RequestUri!.OriginalString);
        var listing = Assert.Single(response.Listings!);
        Assert.Equal(new ListingDto { Id = "abc", MakelaarId = 42, MakelaarName = "Alpha Makelaars" }, listing);
        Assert.Equal(3, response.Paging!.TotalPages);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "")]
    [InlineData(HttpStatusCode.Unauthorized, """{"Message":"Request limit exceeded"}""")] 
    public async Task GetListings_RateLimited_ThrowsRateLimitedException(HttpStatusCode statusCode, string body)
    {
        _handler.Enqueue(statusCode, body);

        await Assert.ThrowsAsync<FeedsApiRateLimitedException>(
            () => CreateClient().GetListingsAsync(Query, page: 1, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"Message":"Invalid API key"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    public async Task GetListings_OtherFailure_ThrowsFeedsApiException(HttpStatusCode statusCode, string body)
    {
        _handler.Enqueue(statusCode, body);

        await Assert.ThrowsAsync<FeedsApiException>(
            () => CreateClient().GetListingsAsync(Query, page: 1, CancellationToken.None));
    }

    [Fact]
    public async Task GetListings_HtmlBotCheckPage_ThrowsFeedsApiException()
    {
        _handler.Enqueue(HttpStatusCode.OK, "<html>Are you a robot?</html>", "text/html");

        var ex = await Assert.ThrowsAsync<FeedsApiException>(
            () => CreateClient().GetListingsAsync(Query, page: 1, CancellationToken.None));
        Assert.Contains("bot check", ex.Message);
    }

    private FeedsApiClient CreateClient() =>
        new(
            new HttpClient(_handler) { BaseAddress = new Uri("http://feeds.test/") },
            Options.Create(new FeedsApiOptions { ApiKey = ApiKey }));
}
