using Funda.Makelaars;
using Funda.Makelaars.Application.MakelaarRanking;
using Funda.Makelaars.Cli;
using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Load appsettings.json from the output directory, regardless of the working directory the app is started from.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddFundaMakelaars(useMockFeedsApi: builder.Configuration.GetValue<bool>("UseMockFeedsApi"));

using var host = builder.Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

SearchQuery[] queries =
[
    new("koop", "/amsterdam/"),
    new("koop", "/amsterdam/tuin/"),
];

try
{
    var rankingService = host.Services.GetRequiredService<IMakelaarRankingService>();
    var tableWriter = new RankingTableWriter(Console.Out);

    foreach (var query in queries)
    {
        var topMakelaars = await rankingService.GetTopMakelaarsAsync(query, 10, cts.Token);
        tableWriter.Write(query, topMakelaars);
    }

    return 0;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}
catch (Exception ex) when (ex is FeedsApiException or OptionsValidationException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex}");
    return 1;
}
