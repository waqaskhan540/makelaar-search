using System.Net;
using System.Text;

namespace Funda.Makelaars.Tests.Shared;

public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string Body, string MediaType)> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
        _responses.Enqueue((statusCode, body, mediaType));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var (statusCode, body, mediaType) = _responses.Dequeue();

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });
    }

}
