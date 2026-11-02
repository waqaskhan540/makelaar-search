using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Funda.Makelaars.Infrastructure.FeedsApi.Resilience;

internal static class FeedsApiResilience
{
    /// <summary>
    /// Adds retry (outermost), client-side throttling and a per-attempt timeout to the Feeds API client.
    /// The rate limiter sits inside the retry, so retries are throttled as well.
    /// </summary>
    public static IHttpClientBuilder AddFeedsApiResilience(this IHttpClientBuilder httpClientBuilder)
    {
        httpClientBuilder.AddResilienceHandler("feeds-api", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<FeedsApiOptions>>().Value;

            // One token every (1 minute / RequestsPerMinute) and a bucket of a single token: requests are spread
            // evenly, so any rolling 60-second window holds at most RequestsPerMinute + 1 requests.
            var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1) / options.RequestsPerMinute,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = int.MaxValue,
            });
            context.OnPipelineDisposed(rateLimiter.Dispose);

            builder
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = options.RetryBaseDelay,
                    ShouldHandle = async args =>
                        HttpClientResiliencePredicates.IsTransient(args.Outcome)
                        || (args.Outcome.Result is { } response
                            && await RateLimitDetection.IsRateLimitedAsync(response, args.Context.CancellationToken)),
                })
                .AddRateLimiter(rateLimiter)
                .AddTimeout(options.AttemptTimeout);
        });

        return httpClientBuilder;
    }
}
