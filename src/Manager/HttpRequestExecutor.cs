using System;
using System.Net;
using System.Net.Http;
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
    private readonly string _marmolade;

    internal HttpRequestExecutor(HttpMessageHandler handler, HttpClient client,
        TimeSpan timeout, int maxRetries, TimeSpan retryDelay,
        Credentials.Auth auth)
    {
        if (handler is not null && client is not null)
            throw new ArgumentException("Specify either an HTTP handler or an HTTP client, not both.");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (retryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryDelay));

        Cookies = new CookieContainer();
        _timeout = timeout;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay;
        _userAgent = auth.UserAgent + $" (Shinden.NET/{Assembly.GetAssembly(typeof(HttpRequestExecutor)).GetName().Version})";
        _marmolade = auth.Marmolade;

        if (client is not null)
        {
            _client = client;
            _ownsClient = false;
            UsesExternalTransport = true;
        }
        else
        {
            var actualHandler = handler ?? new HttpClientHandler
            {
                CookieContainer = Cookies,
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
            UsesExternalTransport = handler is not null;
        }
    }

    internal CookieContainer Cookies { get; }
    internal bool UsesExternalTransport { get; }

    internal async Task<TransportResponse> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        var retryCount = 0;
        while (true)
        {
            using var attempt = await CloneAsync(request).ConfigureAwait(false);
            AddHeaders(attempt);

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

                if (!ShouldRetry(attempt.Method, response.StatusCode, retryCount))
                    return new TransportResponse(response.StatusCode, content);

                retryCount++;
                var delay = GetRetryDelay(response, retryCount);
                await Task.Delay(delay, timeoutSource.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Accept-Language", "pl");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

        if (!string.IsNullOrEmpty(_marmolade))
            request.Headers.TryAddWithoutValidation(_marmolade, "marmolada");
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

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
}

internal sealed class TransportResponse
{
    internal TransportResponse(HttpStatusCode statusCode, string content)
    {
        StatusCode = statusCode;
        Content = content;
    }

    internal HttpStatusCode StatusCode { get; }
    internal string Content { get; }
    internal bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
}
