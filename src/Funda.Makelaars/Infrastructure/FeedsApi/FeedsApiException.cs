namespace Funda.Makelaars.Infrastructure.FeedsApi;

public class FeedsApiException(string message, Exception? innerException = null) : Exception(message, innerException);

public sealed class FeedsApiRateLimitedException(string message) : FeedsApiException(message);
