using Funda.Makelaars.Application.MakelaarRanking;
using Funda.Makelaars.Domain;

namespace Funda.Makelaars.Cli;

internal sealed class RankingTableWriter(TextWriter output)
{
    public void Write(SearchQuery query, IReadOnlyList<MakelaarListingCount> ranking)
    {
        output.WriteLine();
        output.WriteLine($"Top {ranking.Count} makelaars for {query.Type} {query.Query}");
        output.WriteLine($"{"#",3}  {"Makelaar",-60} {"Listings",8}");

        for (var i = 0; i < ranking.Count; i++)
        {
            var makelaar = ranking[i];
            output.WriteLine($"{i + 1,3}  {makelaar.MakelaarName,-60} {makelaar.ListingCount,8}");
        }
    }
}
