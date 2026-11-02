using System.ComponentModel.DataAnnotations;

namespace Funda.Makelaars.Infrastructure.FeedsApi;

public sealed class FeedsApiOptions
{
    public const string SectionName = "FeedsApi";

    [Required, Url]
    public string BaseUrl { get; set; } = "http://partnerapi.funda.nl/feeds/Aanbod.svc/";

    public string ApiKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PageSize { get; set; } = 25;

    [Range(1, int.MaxValue)]
    public int RequestsPerMinute { get; set; } = 80;

    [Range(1, int.MaxValue)]
    public int MaxRetryAttempts { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    [Range(typeof(TimeSpan), "00:00:00.010", "00:10:00")]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(15);
}
