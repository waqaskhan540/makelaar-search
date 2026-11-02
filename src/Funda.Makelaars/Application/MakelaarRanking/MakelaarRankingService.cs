using Funda.Makelaars.Domain;

namespace Funda.Makelaars.Application.MakelaarRanking;

internal sealed class MakelaarRankingService(IListingSource listingSource)
    : IMakelaarRankingService
{
    public async Task<IReadOnlyList<MakelaarListingCount>> GetTopMakelaarsAsync(
        SearchQuery query, int top, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);

        var listings = new List<Listing>();
        await foreach (var listing in listingSource.GetListingsAsync(query, cancellationToken))
        {
            listings.Add(listing);
        }

        return listings
            .DistinctBy(l => l.Id)
            .GroupBy(l => l.MakelaarId)
            .Select(g => new MakelaarListingCount(
                g.Key,
                g.Select(l => l.MakelaarName).FirstOrDefault(n => n.Length > 0) ?? $"Unknown makelaar ({g.Key})",
                g.Count()))
            .OrderByDescending(m => m.ListingCount)
            .ThenBy(m => m.MakelaarName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.MakelaarId)
            .Take(top)
            .ToList();
    }
}
