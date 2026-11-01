using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ShindenAPI;

internal sealed class HttpRequestExecutor : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private readonly string _userAgent;
    private readonly string _token;
    private readonly bool _legacy;
    private readonly string _marmolade;
    private const int MaxRedirects = 10;

    internal HttpRequestExecutor(HttpMessageHandler handler, HttpClient client,
        TimeSpan timeout, int maxRetries, TimeSpan retryDelay,
        Credentials.Auth auth, bool legacy = false)
    {
        if (handler is not null && client is not null)
            throw new ArgumentException("Specify either an HTTP handler or an HTTP client, not both.");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (retryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryDelay));

        _timeout = timeout;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay;
        _userAgent = auth.UserAgent + $" (Shinden.NET/{Assembly.GetAssembly(typeof(HttpRequestExecutor)).GetName().Version})";
        _token = auth.Token;
        _legacy = legacy;
        _marmolade = auth.Marmolade;

        if (client is not null)
        {
            _client = client;
            _ownsClient = false;
        }
        else
        {
            var actualHandler = handler ?? new HttpClientHandler
            {
                UseCookies = false,
                // Redirects are followed in SendAsync so credentials stay on the original site.
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _client = new HttpClient(actualHandler, disposeHandler: handler is null)
            {
                // Timeout enforcement is handled exclusively by the per-request
                // CancellationTokenSource in SendAsync, so HttpClient's own timeout is disabled
                // to avoid two independent timers racing against each other.
                Timeout = Timeout.InfiniteTimeSpan
            };
            _ownsClient = true;
        }
    }


    internal async Task<TransportResponse> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);
        var elapsed = Stopwatch.StartNew();

        var current = request;
        // Credentials (Bearer token, Marmolade header, PHP cookies) are sent only while
        // every hop stays on the site of the original request.
        var sameSite = true;
        var redirectCount = 0;
        var retryCount = 0;
        var setCookieHeaders = new List<string>();
        try
        {
            while (true)
            {
                using var attempt = await CloneAsync(current, copyHeaders: sameSite).ConfigureAwait(false);
                AddHeaders(attempt, sameSite);

                HttpResponseMessage response;
                try
                {
                    response = await _client.SendAsync(
                        attempt, HttpCompletionOption.ResponseContentRead, timeoutSource.Token).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (IsRetryableMethod(attempt.Method) && retryCount < _maxRetries)
                {
                    retryCount++;
                    await Task.Delay(GetRetryDelay(retryCount), timeoutSource.Token).ConfigureAwait(false);
                    continue;
                }

                using (response)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    timeoutSource.Token.ThrowIfCancellationRequested();

                    // A PHP login may set its session cookies on a redirect response.
                    if (sameSite && response.Headers.TryGetValues("Set-Cookie", out var cookies))
                        setCookieHeaders.AddRange(cookies);

                    var target = redirectCount < MaxRedirects ? GetRedirectTarget(attempt.RequestUri, response) : null;
                    if (target is not null)
                    {
                        var next = await CreateRedirectAsync(current, target, response.StatusCode).ConfigureAwait(false);
                        if (!ReferenceEquals(current, request))
                            current.Dispose();
                        current = next;
                        sameSite = sameSite && IsSameSite(request.RequestUri, target);
                        redirectCount++;
                        continue;
                    }

                    if (!ShouldRetry(attempt.Method, response.StatusCode, retryCount))
                        return Snapshot(response, content, setCookieHeaders);

                    retryCount++;
                    var delay = GetRetryDelay(response, retryCount);
                    if (delay >= _timeout - elapsed.Elapsed)
                        return Snapshot(response, content, setCookieHeaders);
                    await Task.Delay(delay, timeoutSource.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (!ReferenceEquals(current, request))
                current.Dispose();
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private void AddHeaders(HttpRequestMessage request, bool withCredentials)
    {
        request.Headers.TryAddWithoutValidation("Accept-Language", "pl");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

        if (!withCredentials)
            return;
        if (_legacy && !string.IsNullOrEmpty(_marmolade))
            request.Headers.TryAddWithoutValidation(_marmolade, "marmolada");
        if (!_legacy && !string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    private static Uri GetRedirectTarget(Uri requestUri, HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status is not (301 or 302 or 303 or 307 or 308) || response.Headers.Location is not { } location)
            return null;

        var target = location.IsAbsoluteUri ? location : new Uri(requestUri, location);
        if (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp)
            return null;
        // Never downgrade from HTTPS to HTTP; the redirect response is returned as is.
        if (requestUri.Scheme == Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttps)
            return null;
        return target;
    }

    private static async Task<HttpRequestMessage> CreateRedirectAsync(
        HttpRequestMessage request, Uri target, HttpStatusCode statusCode)
    {
        var redirect = await CloneAsync(request, copyHeaders: true).ConfigureAwait(false);
        redirect.RequestUri = target;

        // 307 and 308 keep the method and body; 303 and POST after 301/302 continue as GET.
        var status = (int)statusCode;
        if (request.Method != HttpMethod.Head
            && (status == 303 || (status is 301 or 302 && request.Method == HttpMethod.Post)))
        {
            redirect.Method = HttpMethod.Get;
            redirect.Content?.Dispose();
            redirect.Content = null;
        }

        return redirect;
    }

    // Hosts that differ only by a "www." prefix are treated as the same site, so are
    // HTTP-to-HTTPS upgrades on default ports.
    private static bool IsSameSite(Uri origin, Uri target)
        => string.Equals(SiteHost(origin), SiteHost(target), StringComparison.OrdinalIgnoreCase)
            && (origin.Port == target.Port || (origin.IsDefaultPort && target.IsDefaultPort));

    private static string SiteHost(Uri uri)
        => uri.IdnHost.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.IdnHost.Substring(4) : uri.IdnHost;

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, bool copyHeaders)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (copyHeaders)
        {
            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        return clone;
    }

    private bool ShouldRetry(HttpMethod method, HttpStatusCode statusCode, int retryCount)
    {
        if (retryCount >= _maxRetries)
            return false;
        if (!IsRetryableMethod(method))
            return false;

        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode >= 500;
    }

    private static bool IsRetryableMethod(HttpMethod method)
        => method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    private TimeSpan GetRetryDelay(int retryCount)
    {
        var multiplier = Math.Min(retryCount, 8);
        return TimeSpan.FromTicks(Math.Min(
            _retryDelay.Ticks * multiplier,
            TimeSpan.FromSeconds(30).Ticks));
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int retryCount)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is not null)
        {
            if (retryAfter.Delta is { } delta && delta >= TimeSpan.Zero)
                return delta;

            // Some servers send an absolute HTTP-date instead of a delta-seconds value.
            if (retryAfter.Date is { } date)
            {
                var delay = date - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    return delay;
            }
        }

        var multiplier = Math.Min(retryCount, 8);
        return TimeSpan.FromTicks(Math.Min(
            _retryDelay.Ticks * multiplier,
            TimeSpan.FromSeconds(30).Ticks));
    }

    private static int? RetryAfterSeconds(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
        return delay.HasValue ? (int?)Math.Min(int.MaxValue, Math.Max(0, Math.Ceiling(delay.Value.TotalSeconds))) : null;
    }

    private static TransportResponse Snapshot(HttpResponseMessage response, string content, IEnumerable<string> setCookieHeaders)
        => new(response.StatusCode, content, RetryAfterSeconds(response), setCookieHeaders);
}

internal sealed class TransportResponse
{
    internal TransportResponse(HttpStatusCode statusCode, string content, int? retryAfterSeconds, IEnumerable<string> setCookieHeaders)
    {
        StatusCode = statusCode;
        Content = content;
        RetryAfterSeconds = retryAfterSeconds;
        SetCookieHeaders = setCookieHeaders.ToArray();
    }

    internal HttpStatusCode StatusCode { get; }
    internal string Content { get; }
    internal int? RetryAfterSeconds { get; }
    internal IReadOnlyList<string> SetCookieHeaders { get; }
    internal bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
}
