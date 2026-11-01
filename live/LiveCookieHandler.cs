using System.Net;

namespace ShindenAPI.LiveTests;

internal sealed class LiveCookieHandler : HttpMessageHandler
{
    private readonly HttpMessageInvoker _transport;

    internal LiveCookieHandler(HttpMessageHandler? handler = null)
    {
        _transport = new HttpMessageInvoker(handler ?? new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }, disposeHandler: handler is null);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookies = request.Headers.TryGetValues("Cookie", out var values)
            ? values.SelectMany(value => value.Split(';')).Select(value => value.Trim())
                .Where(value => value.Length > 0 && value.Split('=', 2)[0].Trim() != "proba").ToList()
            : new List<string>();
        cookies.Add("proba=1");
        request.Headers.Remove("Cookie");
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies));
        return _transport.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _transport.Dispose();
        base.Dispose(disposing);
    }
}
