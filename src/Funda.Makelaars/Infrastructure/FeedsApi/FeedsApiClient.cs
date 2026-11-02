using System.Net.Http.Json;
using System.Text.Json;
using Funda.Makelaars.Domain;
using Funda.Makelaars.Infrastructure.FeedsApi.Contracts;
using Funda.Makelaars.Infrastructure.FeedsApi.Resilience;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Funda.Makelaars.Infrastructure.FeedsApi;

internal sealed class FeedsApiClient(HttpClient httpClient, IOptions<FeedsApiOptions> options) : IFeedsApiClient
{
    private readonly FeedsApiOptions _options = options.Value;

    public async Task<SearchResponse> GetListingsAsync(SearchQuery query, int page, CancellationToken cancellationToken)
    {
        var requestUri = $"json/{_options.ApiKey}/?type={query.Type}&zo={query.Query}&page={page}&pagesize={_options.PageSize}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (await RateLimitDetection.IsRateLimitedAsync(response, cancellationToken))
            {
                throw new FeedsApiRateLimitedException($"Rate limit still exceeded after retries for page {page} of {query.Query}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FeedsApiException($"Feeds API returned {(int)response.StatusCode} for page {page} of {query.Query}.");
            }

            // The API answers non-browser clients with a 200 HTML bot-check page instead of JSON.
            if (response.Content.Headers.ContentType?.MediaType is { } mediaType
                && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                throw new FeedsApiException(
                    $"Feeds API returned '{mediaType}' instead of JSON for page {page} of {query.Query}; the request was likely blocked by a bot check.");
            }

            return await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken)
                ?? throw new FeedsApiException($"Feeds API returned an empty response for page {page} of {query.Query}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or JsonException)
        {
            throw new FeedsApiException($"Request for page {page} of {query.Query} failed.", ex);
        }
    }
}
