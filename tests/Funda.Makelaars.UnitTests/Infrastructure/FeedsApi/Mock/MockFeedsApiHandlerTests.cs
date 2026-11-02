using System.Net;
using System.Net.Http.Json;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Funda.Makelaars.Infrastructure.FeedsApi.Contracts;
using Funda.Makelaars.Infrastructure.FeedsApi.Mock;

namespace Funda.Makelaars.UnitTests.Infrastructure.FeedsApi.Mock;

public sealed class MockFeedsApiHandlerTests : IDisposable
{
    private readonly string _mockDataDirectory = Directory.CreateTempSubdirectory().FullName;
    private readonly HttpClient _client;

    public MockFeedsApiHandlerTests()
    {
        File.WriteAllText(Path.Combine(_mockDataDirectory, "koop-amsterdam-tuin.json"), """
            { "Objects": [ { "Id": "1" }, { "Id": "2" }, { "Id": "3" } ], "TotaalAantalObjecten": 3 }
            """);
        _client = new HttpClient(new MockFeedsApiHandler(_mockDataDirectory)) { BaseAddress = new Uri("http://feeds.test/") };
    }

    [Theory]
    [InlineData(1, new[] { "1", "2" })]
    [InlineData(2, new[] { "3" })]
    [InlineData(3, new string[0])]
    public async Task Send_PageOfFeed_ReturnsSliceWithPaging(int page, string[] expectedIds)
    {
        var response = await _client.GetFromJsonAsync<SearchResponse>(
            $"json/key/?type=koop&zo=/amsterdam/tuin/&page={page}&pagesize=2");

        Assert.Equal(expectedIds, response!.Listings!.Select(l => l.Id));
        Assert.Equal(2, response.Paging!.TotalPages);
        Assert.Equal(page, response.Paging.CurrentPage);
    }

    [Fact]
    public async Task Send_InvalidPage_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("json/key/?type=koop&zo=/amsterdam/tuin/&page=0&pagesize=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_UnknownSearch_ThrowsFeedsApiException()
    {
        await Assert.ThrowsAsync<FeedsApiException>(
            () => _client.GetAsync("json/key/?type=koop&zo=/rotterdam/&page=1&pagesize=2"));
    }

    public void Dispose()
    {
        _client.Dispose();
        Directory.Delete(_mockDataDirectory, recursive: true);
    }
}
