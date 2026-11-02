using System.Text.Json.Serialization;

namespace Funda.Makelaars.Infrastructure.FeedsApi.Contracts;

internal sealed record SearchResponse
{
    [JsonPropertyName("Objects")]
    public IReadOnlyList<ListingDto>? Listings { get; init; }

    [JsonPropertyName("Paging")]
    public Paging? Paging { get; init; }

    [JsonPropertyName("TotaalAantalObjecten")]
    public int TotalListingCount { get; init; }
}
