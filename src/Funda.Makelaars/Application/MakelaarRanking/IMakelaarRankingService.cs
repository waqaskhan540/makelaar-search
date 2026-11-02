using Funda.Makelaars.Domain;

namespace Funda.Makelaars.Application.MakelaarRanking;

public interface IMakelaarRankingService
{
    Task<IReadOnlyList<MakelaarListingCount>> GetTopMakelaarsAsync(SearchQuery query, int top, CancellationToken cancellationToken);
}
