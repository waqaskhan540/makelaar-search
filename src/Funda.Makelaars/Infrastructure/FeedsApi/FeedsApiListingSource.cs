using System.Runtime.CompilerServices;
using Funda.Makelaars.Application;
using Funda.Makelaars.Domain;
using Microsoft.Extensions.Logging;

namespace Funda.Makelaars.Infrastructure.FeedsApi;

internal sealed class FeedsApiListingSource(IFeedsApiClient feedsApiClient, ILogger<FeedsApiListingSource> logger)
    : IListingSource
{
    public async IAsyncEnumerable<Listing> GetListingsAsync(
        SearchQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = 1;
        var totalPages = 1;

        do
        {
            var response = await feedsApiClient.GetListingsAsync(query, page, cancellationToken);

            if (response.Listings is not { Count: > 0 } listings)
            {
                yield break;
            }

            foreach (var dto in listings)
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    continue;
                }

                yield return new Listing(dto.Id, dto.MakelaarId, dto.MakelaarName?.Trim() ?? string.Empty);
            }

            totalPages = response.Paging?.TotalPages ?? page;
            logger.LogInformation("Fetched page {Page} of {TotalPages} for {Type} {Query}", page, totalPages, query.Type, query.Query);
            page++;
        }
        while (page <= totalPages);
    }
}
