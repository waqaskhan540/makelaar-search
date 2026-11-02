using Funda.Makelaars.Domain;

namespace Funda.Makelaars.Application;

internal interface IListingSource
{
    IAsyncEnumerable<Listing> GetListingsAsync(SearchQuery query, CancellationToken cancellationToken);
}
