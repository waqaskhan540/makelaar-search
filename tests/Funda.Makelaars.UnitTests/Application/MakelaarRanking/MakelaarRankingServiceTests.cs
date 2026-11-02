using Funda.Makelaars.Application;
using Funda.Makelaars.Application.MakelaarRanking;
using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Moq;

namespace Funda.Makelaars.UnitTests.Application.MakelaarRanking;

public class MakelaarRankingServiceTests
{
    private static readonly SearchQuery Query = new("koop", "/amsterdam/");

    private readonly Mock<IListingSource> _listingSource = new(MockBehavior.Strict);
    private readonly MakelaarRankingService _sut;

    public MakelaarRankingServiceTests()
    {
        _sut = new MakelaarRankingService(_listingSource.Object);
    }

    [Fact]
    public async Task GetTopMakelaars_ReturnsTopByCount()
    {
        SetupListings(
            new("1", 10, "Alpha"), new("2", 10, "Alpha"), new("3", 20, "Bravo"),
            new("4", 10, "Alpha"), new("5", 20, "Bravo"), new("6", 30, "Charlie"));

        var result = await _sut.GetTopMakelaarsAsync(Query, top: 2, CancellationToken.None);

        Assert.Equal(
            [new MakelaarListingCount(10, "Alpha", 3), new MakelaarListingCount(20, "Bravo", 2)],
            result);
    }

    [Fact]
    public async Task GetTopMakelaars_DuplicateListing_CountsOnce()
    {
        SetupListings(new("1", 10, "Alpha"), new("1", 10, "Alpha"), new("2", 10, "Alpha"));

        var result = await _sut.GetTopMakelaarsAsync(Query, top: 10, CancellationToken.None);

        Assert.Equal([new MakelaarListingCount(10, "Alpha", 2)], result);
    }

    [Fact]
    public async Task GetTopMakelaars_EqualCounts_OrdersByName()
    {
        SetupListings(new("1", 20, "Bravo"), new("2", 10, "alpha"), new("3", 30, "Charlie"));

        var result = await _sut.GetTopMakelaarsAsync(Query, top: 10, CancellationToken.None);

        Assert.Equal(["alpha", "Bravo", "Charlie"], result.Select(m => m.MakelaarName));
    }

    [Fact]
    public async Task GetTopMakelaars_MissingName_UsesFallback()
    {
        SetupListings(new("1", 10, ""), new("2", 10, "Alpha"), new("3", 20, ""));

        var result = await _sut.GetTopMakelaarsAsync(Query, top: 10, CancellationToken.None);

        Assert.Equal(
            [new MakelaarListingCount(10, "Alpha", 2), new MakelaarListingCount(20, "Unknown makelaar (20)", 1)],
            result);
    }

    [Fact]
    public async Task GetTopMakelaars_SourceFails_Throws()
    {
        _listingSource
            .Setup(s => s.GetListingsAsync(Query, It.IsAny<CancellationToken>()))
            .Returns(FailingAfterFirstListing());

        await Assert.ThrowsAsync<FeedsApiRateLimitedException>(
            () => _sut.GetTopMakelaarsAsync(Query, top: 10, CancellationToken.None));
    }

    private void SetupListings(params Listing[] listings) =>
        _listingSource
            .Setup(s => s.GetListingsAsync(Query, It.IsAny<CancellationToken>()))
            .Returns(listings.ToAsyncEnumerable());

    private static async IAsyncEnumerable<Listing> FailingAfterFirstListing()
    {
        yield return new Listing("1", 10, "Alpha");
        await Task.Yield();
        throw new FeedsApiRateLimitedException("Rate limit exceeded.");
    }
}
