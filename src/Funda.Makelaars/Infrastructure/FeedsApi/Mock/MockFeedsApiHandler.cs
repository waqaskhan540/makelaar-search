using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Funda.Makelaars.Infrastructure.FeedsApi.Mock;

public class MockFeedsApiHandler(string mockDataDirectory) : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Lazy<JsonDocument>> _feeds = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var parameters = HttpUtility.ParseQueryString(request.RequestUri!.Query);

        if (!int.TryParse(parameters["page"], out var page) || page < 1
            || !int.TryParse(parameters["pagesize"], out var pageSize) || pageSize < 1)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = request });
        }

        var fileName = $"{parameters["type"]}{parameters["zo"]}".Trim('/').Replace('/', '-') + ".json";
        var listings = LoadFeed(fileName).RootElement.GetProperty("Objects");
        var listingCount = listings.GetArrayLength();

        using var body = new MemoryStream();
        using (var writer = new Utf8JsonWriter(body))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("Objects");
            foreach (var listing in listings.EnumerateArray().Skip((page - 1) * pageSize).Take(pageSize))
            {
                listing.WriteTo(writer);
            }
            writer.WriteEndArray();
            writer.WriteStartObject("Paging");
            writer.WriteNumber("AantalPaginas", (listingCount + pageSize - 1) / pageSize);
            writer.WriteNumber("HuidigePagina", page);
            writer.WriteEndObject();
            writer.WriteNumber("TotaalAantalObjecten", listingCount);
            writer.WriteEndObject();
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(Encoding.UTF8.GetString(body.ToArray()), Encoding.UTF8, "application/json"),
        });
    }

    private JsonDocument LoadFeed(string fileName) =>
        _feeds.GetOrAdd(fileName, name => new Lazy<JsonDocument>(() =>
        {
            var filePath = Path.Combine(mockDataDirectory, name);
            if (!File.Exists(filePath))
            {
                throw new FeedsApiException($"No mock data for this search; expected {filePath}.");
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                return JsonDocument.Parse(stream);
            }
            catch (JsonException ex)
            {
                throw new FeedsApiException($"Mock data file {filePath} is not valid JSON.", ex);
            }
        })).Value;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var feed in _feeds.Values.Where(f => f.IsValueCreated))
            {
                feed.Value.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
