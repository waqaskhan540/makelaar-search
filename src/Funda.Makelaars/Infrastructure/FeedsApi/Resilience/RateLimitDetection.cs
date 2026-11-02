using System.Net;

namespace Funda.Makelaars.Infrastructure.FeedsApi.Resilience;

internal static class RateLimitDetection
{
    public static async ValueTask<bool> IsRateLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        response.StatusCode == HttpStatusCode.TooManyRequests
        || (response.StatusCode == HttpStatusCode.Unauthorized
            && (await response.Content.ReadAsStringAsync(cancellationToken)).Contains("Request limit exceeded", StringComparison.OrdinalIgnoreCase));
}
