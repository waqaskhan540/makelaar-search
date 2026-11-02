using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi.Contracts;

namespace Funda.Makelaars.Infrastructure.FeedsApi;

internal interface IFeedsApiClient
{
    Task<SearchResponse> GetListingsAsync(SearchQuery query, int page, CancellationToken cancellationToken);
}
