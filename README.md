# Funda Assignment : Ranking Top Makelaars 

A .NET 10 console application that uses the Funda Feeds API to rank the top 10 makelaars in Amsterdam by number of listings for sale. It prints two rankings: one for all listings (`/amsterdam/`) and one for listings with a garden (`/amsterdam/tuin/`).

> **Note:** The Feeds API answers non-browser clients with a `200 OK` bot-check page instead of data, so it can't be called from code. As Funda asked, I did not try to bypass the bot protection. By default the application runs on mock data, served through the same HTTP pipeline the real API would use. See [Mock data and the real API](#mock-data-and-the-real-api).

## Getting Started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Mock data (the default in appsettings.json; no API key needed). Takes about 1.5 minutes because of throttling.
dotnet run --project src/Funda.Makelaars.Cli

# Real API, with the temporary key from the assignment
dotnet run --project src/Funda.Makelaars.Cli -- --UseMockFeedsApi=false --FeedsApi:ApiKey=<key>
```

The rankings are printed to stdout. Progress logs (one line per page fetched) go to stderr.

## Mock data and the real API

Mock mode replaces only the network. `MockFeedsApiHandler` is an `HttpMessageHandler` plugged in underneath the real `FeedsApiClient`. It reads the `page` and `pagesize` query parameters from the request URL and returns that slice of [MockData/](src/Funda.Makelaars.Cli/MockData/) in the Feeds API's JSON format. Everything else runs exactly as it would against the live API: URL building, the rate limiter, retries, timeouts, JSON parsing, paging, de-duplication and ranking.

To switch to the real API, set `UseMockFeedsApi=false` and provide an API key; no code changes are needed. Against the real API today, the run stops with a clear message:

```
Error: Feeds API returned 'text/html' instead of JSON for page 1 of /amsterdam/; the request was likely blocked by a bot check.
```

The mock data is synthetic: 2,400 listings from 62 makelaars, with a garden share that differs per makelaar. It also includes duplicate listings, names with extra whitespace and missing names, so the edge cases below are exercised.

## Sample Output

From the mock data:

```
Top 10 makelaars for koop /amsterdam/
  #  Makelaar                                                     Listings
  1  Verhoeven Makelaardij                                             252
  2  Makelaarskantoor Post                                             186
  3  Hoekstra Wonen                                                    173
  4  Carla van den Brink B.V.                                          135
  5  Pulles Makelaardij                                                129
  6  Steenbergen & Vermeulen Makelaars                                  82
  7  Koster Makelaardij                                                 79
  8  BNV Makelaars                                                      64
  9  Makelaardij Van der Linden Amsterdam                               56
 10  Molenaar Makelaars B.V.                                            52

Top 10 makelaars for koop /amsterdam/tuin/
  #  Makelaar                                                     Listings
  1  Makelaarskantoor Post                                             133
  2  Verhoeven Makelaardij                                             116
  3  Pulles Makelaardij                                                 62
  4  Steenbergen & Vermeulen Makelaars                                  42
  5  Hoekstra Wonen                                                     27
  6  BNV Makelaars                                                      22
  7  Groen Vastgoed                                                     21
  8  Makelaardij Van der Linden Amsterdam                               21
  9  Hallie & Van Klooster Makelaardij                                  20
 10  Hoekstra Makelaardij                                               20
```

## Architecture

```
src/
  Funda.Makelaars/          # Class library: Domain, Application, Infrastructure
  Funda.Makelaars.Cli/      # Thin console host
tests/
  Funda.Makelaars.UnitTests/
  Funda.Makelaars.IntegrationTests/
```

## Design Decisions

- **The logic lives in a class library.** The console app is only a host, so a web API or a background worker could use the same code through `AddFundaMakelaars()`.
- **Paging is separate from HTTP.** `IFeedsApiClient` fetches one page and `IListingSource` streams all listings, so each can be tested on its own.
- **The mock sits at the HTTP level, not as a fake client.** A fake `IFeedsApiClient` would skip the rate limiter, retries and parsing, which are exactly the parts that matter against the real API.
- **Resilience is configured, not hand-written.** Retry, rate limiting and timeouts use `Microsoft.Extensions.Http.Resilience` on a typed `HttpClient`.
- **No partial results.** If a page still fails after all retries, the whole search fails. A ranking built from incomplete data would look correct but be wrong.

## Throttling & Rate Limiting

The API rejects clients that send more than 100 requests per minute. Funda couldn't confirm whether the limit is counted per API key or per IP address. I therefore treat it as a single shared budget of 100 requests per minute, covering every request this process sends: both searches, every page and every retry.

The work doesn't fit inside that budget. The mock data alone needs 132 requests (97 pages for Amsterdam and 35 for gardens), so the app has to slow itself down rather than fire requests as fast as it can. The design has two parts: **avoid** hitting the limit, and **recover** if the API rejects requests anyway.

### Avoiding the limit: a token bucket at 80 requests per minute

- **Even spacing.** A token bucket adds one token every 60 s / 80 = 750 ms and holds at most one token. Each request takes a token, so requests are at least 750 ms apart. **No 60-second window can contain more than 81 requests**, wherever the server's window starts.
- **Why not a fixed or sliding window.** Those limiters hand back permits in batches when a window or segment rolls over. If requests bunch up on either side of a rollover, a 60-second span can contain up to twice the configured limit. Since we don't know how the server counts, even spacing is the only pattern that is safe under any counting method.
- **Why 80 and not 100.** The 20-request margin absorbs clock drift between us and the server, retries, and other clients using the same budget. The temporary key in the assignment is probably shared with other candidates. The limit can be changed in `FeedsApi:RequestsPerMinute`.
- **Queue, don't reject.** A request that arrives before a token is ready waits its turn in first-in, first-out order. Our own limiter therefore never makes a run fail; it only makes it slower.
- **One limiter for everything.** The limiter is part of the HTTP client's pipeline and sits inside the retry strategy, so both searches and every retry attempt draw from the same bucket.

Measured on the mock run: 132 requests in 99 seconds, with at most 80 in any rolling 60 seconds.

### Recovering when the API rejects requests anyway

- **Detection.** The Feeds API signals rate limiting with a `401 Unauthorized` whose body contains `Request limit exceeded`, as well as with the standard `429`. `RateLimitDetection` checks for both. Any other `401`, such as an invalid key, fails immediately and isn't retried.
- **Retry.** Rejected requests and transient failures (5xx, timeouts, network errors) are retried up to 5 times, with exponential backoff and jitter starting at 2 s. The waits add up to about one minute, enough for the server's window to reset. A `Retry-After` header is followed when the API sends one.
- **Giving up.** If the API still rejects requests after all retries, the client throws `FeedsApiRateLimitedException` and the run ends with a clear error and exit code 1, rather than printing a partial ranking.

### Not covered

The limiter only applies within one process. Several instances sharing a key would need a distributed limiter, for example one backed by Redis.

## Edge Cases

- **Duplicate listings across pages** (listings can shift while paging) are removed by `Id`.
- **Makelaars are grouped by `MakelaarId`**, not by name. A missing name is shown as `Unknown makelaar (<id>)`.
- **An empty page** stops paging, even if the API reports more pages.

## Testing

```bash
dotnet test
```

- **Unit tests** cover ranking, paging, how the client handles each kind of HTTP response (including the bot-check page), and how the mock handler slices pages.
- **Integration tests** run the real DI container and HTTP pipeline against a stubbed HTTP handler. They cover retries on rate-limit responses, giving up once retries run out, request spacing by the token bucket, and a full multi-page ranking from request to result.

## Assumptions

- `MakelaarId` identifies a makelaar. The name is only used for display.
- A complete, accurate ranking matters more than speed.
- The ranking is cut at exactly 10, even when the 10th and 11th makelaars have the same count.

## Trade-offs & Limitations

- **No consistent snapshot.** Listings can change while pages are being fetched, so a listing that moves between pages during a run could be missed.
- **The searches and the top-N value are set in `Program.cs`**, not in configuration.

## Future Improvements

- A cache layer can be added to compensate rate limiting against feeds api, but it has to be designed carefully based on how frequently the data changes, 
which data is in demand and how frequently the rate limit is being breached. 

## Use of AI

I used AI as a pair programmer. I made the design decisions; AI sped up the mechanical work so I could spend my time on design, edge cases and resilience.

- **Brainstorming:** I gave it the specification and my planned approach and asked it to find gaps. This helped me catch edge cases early.
- **Code generation:** I gave precise instructions on what to build, how to build it, and which libraries and patterns to use or avoid. 
- **Mock data:** a script generated synthetic data in the Feeds API's response format. This let the app run end to end while the real API was blocked by the bot check.
- **Code review:** I asked it to review the code and suggest improvements, then decided which ones to apply.
- **README:** written in small, incremental steps that I reviewed and edited.