using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ShindenAPI.Test;

internal sealed class RecordingHandler : HttpMessageHandler
{
    internal string Response { get; set; } = "{}";
    internal HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    internal int? RetryAfter { get; set; }
    internal Uri Uri { get; private set; } = null!;
    internal HttpMethod Method { get; private set; } = null!;
    internal string? Body { get; private set; }
    internal string? Authorization { get; private set; }
    internal bool HadApiKeyHeader { get; private set; }
    internal int CallCount { get; private set; }
    internal bool WasDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        Uri = request.RequestUri!;
        Method = request.Method;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Authorization = request.Headers.Authorization?.ToString();
        HadApiKeyHeader = request.Headers.Contains("X-Api-Key");
        var response = new HttpResponseMessage(StatusCode) { Content = new StringContent(Response) };
        if (RetryAfter.HasValue) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(RetryAfter.Value));
        return response;
    }

    internal Dictionary<string, string[]> Query() => Uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .GroupBy(pair => System.Uri.UnescapeDataString(pair[0]))
        .ToDictionary(group => group.Key, group => group.Select(pair => System.Uri.UnescapeDataString(pair[1])).ToArray());
}
