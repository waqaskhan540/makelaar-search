using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Funda.Makelaars.Infrastructure.FeedsApi.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Funda.Makelaars.UnitTests.Infrastructure.FeedsApi;

public class FeedsApiListingSourceTests
{
    private static readonly SearchQuery Query = new("koop", "/amsterdam/");

    private readonly Mock<IFeedsApiClient> _feedsApiClient = new(MockBehavior.Strict);
    private readonly FeedsApiListingSource _sut;

    public FeedsApiListingSourceTests()
    {
        _sut = new FeedsApiListingSource(_feedsApiClient.Object, NullLogger<FeedsApiListingSource>.Instance);
    }

    [Fact]
    public async Task GetListings_MultiplePages_ReturnsListingsFromAllPages()
    {
        SetupPage(1, totalPages: 2, Dto("1", 10, "Alpha"), Dto("2", 20, "Bravo"));
        SetupPage(2, totalPages: 2, Dto("3", 30, "Charlie"));

        var listings = await _sut.GetListingsAsync(Query, CancellationToken.None).ToListAsync();

        Assert.Equal(["1", "2", "3"], listings.Select(l => l.Id));
        _feedsApiClient.Verify(c => c.GetListingsAsync(Query, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetListings_EmptyPage_StopsPaging()
    {
        SetupPage(1, totalPages: 5, Dto("1", 10, "Alpha"));
        SetupPage(2, totalPages: 5);

        var listings = await _sut.GetListingsAsync(Query, CancellationToken.None).ToListAsync();

        Assert.Single(listings);
        _feedsApiClient.Verify(c => c.GetListingsAsync(Query, 3, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetListings_MissingIdOrUntrimmedName_SkipsAndTrims()
    {
        SetupPage(1, totalPages: 1, Dto(null, 10, "Alpha"), Dto(" ", 10, "Alpha"), Dto("1", 20, "  Bravo "), Dto("2", 30, null));

        var listings = await _sut.GetListingsAsync(Query, CancellationToken.None).ToListAsync();

        Assert.Equal([new Listing("1", 20, "Bravo"), new Listing("2", 30, "")], listings);
    }

    private void SetupPage(int page, int totalPages, params ListingDto[] listings) =>
        _feedsApiClient
            .Setup(c => c.GetListingsAsync(Query, page, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResponse
            {
                Listings = listings,
                Paging = new Paging { CurrentPage = page, TotalPages = totalPages },
            });

    private static ListingDto Dto(string? id, int makelaarId, string? makelaarName) =>
        new() { Id = id, MakelaarId = makelaarId, MakelaarName = makelaarName };
}
