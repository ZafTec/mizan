using System.Net;

namespace Mizan.Prices.Tests.Support;

/// <summary>
/// The internet, as a dictionary. Unrouted URLs answer 404 and are recorded
/// like every other request, so a test can assert what was and was not fetched.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpMessageHandler Map(string url, string body, string mediaType = "text/html", HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[url] = _ => new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) };
        return this;
    }

    public FakeHttpMessageHandler Map(string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes[url] = respond;
        return this;
    }

    public IEnumerable<string> RequestedUrls => Requests.Select(r => r.RequestUri!.ToString());

    public HttpClient CreateClient() => new(this) { Timeout = TimeSpan.FromSeconds(30) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var response = _routes.TryGetValue(request.RequestUri!.ToString(), out var respond)
            ? respond(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };

        return Task.FromResult(response);
    }
}
