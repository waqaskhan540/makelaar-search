using System.Text.Json.Serialization;

namespace Funda.Makelaars.Infrastructure.FeedsApi.Contracts;

internal sealed record Paging
{
    [JsonPropertyName("AantalPaginas")]
    public int TotalPages { get; init; }

    [JsonPropertyName("HuidigePagina")]
    public int CurrentPage { get; init; }
}
