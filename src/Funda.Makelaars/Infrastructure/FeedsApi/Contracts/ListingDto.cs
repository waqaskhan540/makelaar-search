using System.Text.Json.Serialization;

namespace Funda.Makelaars.Infrastructure.FeedsApi.Contracts;

internal sealed record ListingDto
{
    public string? Id { get; init; }

    public int MakelaarId { get; init; }

    [JsonPropertyName("MakelaarNaam")]
    public string? MakelaarName { get; init; }
}
