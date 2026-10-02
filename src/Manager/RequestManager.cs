using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShindenAPI;

internal class RequestManager : IDisposable
{
    private readonly Uri _baseUri;
    private readonly Credentials.Auth _auth;
    private readonly ILogger<Shinden> _logger;
    private readonly ShindenUrlResolver _urlResolver;
    private readonly HttpRequestExecutor _executor;
    private readonly TimeSpan _sessionLifetime;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    // Raw and URL-escaped forms of the API token, used to scrub it from anything written
    // through Log() so it can never be observed in application logs.
    private readonly string _redactToken;
    private readonly string _redactTokenEscaped;
    private int _disposed;

    private UserSession _session;
    private Queries.User.Login _userLoginReq;

    internal RequestManager(Uri baseUri, Credentials.Auth auth, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!,
        ShindenUrlResolver urlResolver = default!,
        TimeSpan requestTimeout = default, TimeSpan sessionLifetime = default,
        Func<DateTimeOffset> clock = default!, int maxRetries = 0,
        TimeSpan retryDelay = default)
    {
        if (baseUri is null)
            throw new ArgumentNullException(nameof(baseUri));
        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(baseUri));
        if (auth is null)
            throw new ArgumentNullException(nameof(auth));
        ValidateAuth(auth);
        _auth = auth;
        _redactToken = auth.Token;
        _redactTokenEscaped = Uri.EscapeDataString(auth.Token);
        _session = null;
        _logger = logger;
        _baseUri = baseUri;
        if (!string.Equals(_baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            Log(LogLevel.Warning,
                "The configured API URI ({uri}) does not use HTTPS. The API token is sent as a query " +
                "parameter and will be transmitted in plain text over an insecure connection.", _baseUri);
        }
        _urlResolver = urlResolver ?? new ShindenUrlResolver(baseUri);
        _sessionLifetime = sessionLifetime > TimeSpan.Zero ? sessionLifetime : Config.DefaultSessionLifetime;
        _clock = clock ?? Config.DefaultClock;
        _userLoginReq = null;

        _executor = new HttpRequestExecutor(handler, client,
            requestTimeout > TimeSpan.Zero ? requestTimeout : Config.DefaultRequestTimeout,
            maxRetries, retryDelay, auth);

        if (_auth is Credentials.UserAuth uAuth)
        {
            _userLoginReq = new Queries.User.Login(uAuth);
        }
    }

    internal bool IsValidUserSession()
    {
        ThrowIfDisposed();
        if (_userLoginReq is null)
            return false;

        return _session is not null && _session.IsValid(_clock());
    }

    internal async Task<Models.UserSearchResult> ForceLoginAsync(CancellationToken cancellationToken = default)
        => (await LoginAsUserAsync(cancellationToken).ConfigureAwait(false)).Get();

    internal async Task<ErrorOr<Models.UserSearchResult>> LoginAsUserAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_userLoginReq is null)
        {
            return ErrorOr<Models.UserSearchResult>.FromError(
                Error.FromMessage(ErrorType.Authentication, 401,
                    "User credentials are not configured."));
        }

        if (_session is not null && _session.IsValid(_clock()))
            return ErrorOr<Models.UserSearchResult>.From(_session.User);

        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not null && _session.IsValid(_clock()))
                return ErrorOr<Models.UserSearchResult>.From(_session.User);

            return await LoginAsUser(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loginLock.Release();
        }
    }

    internal async Task<ErrorOr<TReturn>> MakeQueryAsync<TReturn>(Queries.Request<TReturn> request,
        bool skipSession = false, CancellationToken cancellationToken = default)
        where TReturn : class
    {
        ThrowIfDisposed();
        if (!skipSession)
        {
            var sessionError = await CheckSession(cancellationToken).ConfigureAwait(false);
            if (sessionError is not null)
                return ErrorOr<TReturn>.FromError(sessionError);
        }

        HttpRequestMessage body;
        try
        {
            body = request.Build(_baseUri, _auth.Token);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Critical, ex, "[build]: request construction failed | {msg}", ex.Message);
            return ErrorOr<TReturn>.FromException(ex, ErrorType.Configuration);
        }

        try
        {
            Log(LogLevel.Trace, "[{method}]: {uri} | starting...", body.Method, body.RequestUri.LocalPath);

            if (request.RequireHttps() && body.RequestUri.Scheme != Uri.UriSchemeHttps)
            {
                var builder = new UriBuilder(body.RequestUri)
                {
                    Scheme = Uri.UriSchemeHttps,
                    Port = -1
                };
                body.RequestUri = builder.Uri;
            }

            if (_executor.UsesExternalTransport && _session is not null)
            {
                body.Headers.TryAddWithoutValidation("Cookie",
                    $"name={_session.Name}; id={_session.Id}");
            }

            var requestStart = StartStopwatch();
            var response = await _executor.SendAsync(body, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log(LogLevel.Error, "[{method}]: {uri} | {code} | {time} ms | {error}", body.Method, body.RequestUri.LocalPath,
                    (int)response.StatusCode, GetElapsedMilliseconds(requestStart), response.Content);

                return ErrorOr<TReturn>.FromHttpResponse(response.StatusCode, response.Content);
            }

            Log(LogLevel.Information, "[{method}]: {uri} | {code} | {time} ms", body.Method, body.RequestUri.LocalPath,
                (int)response.StatusCode, GetElapsedMilliseconds(requestStart));

            Log(LogLevel.Trace, "[{method}]: {uri} | parsing...", body.Method, body.RequestUri.LocalPath);

            var parseStart = StartStopwatch();
            TReturn obj;
            try
            {
                obj = request.Parse(response.Content);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Critical, ex, "[{method}]: {uri} | parsing failed | {msg}",
                    body.Method, body.RequestUri.LocalPath, ex.Message);
                return ErrorOr<TReturn>.FromException(ex, ErrorType.Parsing);
            }

            if (obj is null)
            {
                return ErrorOr<TReturn>.FromException(
                    new InvalidOperationException("The API returned an empty response."),
                    ErrorType.Parsing);
            }

            ShindenUrlResolver.Apply(obj, _urlResolver);

            Log(LogLevel.Trace, "[{method}]: {uri} | parsed | {time} ms", body.Method, body.RequestUri.LocalPath, GetElapsedMilliseconds(parseStart));

            return ErrorOr<TReturn>.From(obj);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            return ErrorOr<TReturn>.FromException(ex, ErrorType.Timeout, 408);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Critical, ex, "[{method}]: {uri} | {msg}", body.Method, body.RequestUri.LocalPath, ex.Message);
            return ErrorOr<TReturn>.FromException(ex);
        }
        finally
        {
            body.Dispose();
        }
    }

    // Returned/consumed as local state (not instance fields) so concurrent requests on the
    // same RequestManager do not race on a shared timer.
#if NET7_0_OR_GREATER
    private static long StartStopwatch() => Stopwatch.GetTimestamp();

    private static long GetElapsedMilliseconds(long startTimestamp)
        => (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
#else
    private static Stopwatch StartStopwatch() => Stopwatch.StartNew();

    private static long GetElapsedMilliseconds(Stopwatch stopwatch)
        => stopwatch.ElapsedMilliseconds;
#endif

    private void Log(LogLevel level, Exception ex, string message, params object[] args)
    {
        if (_logger is null)
            return;

        _logger.Log(level, ex, message, RedactArgs(args));
    }

    private void Log(LogLevel level, string message, params object[] args)
    {
        if (_logger is null)
            return;

        _logger.Log(level, message, RedactArgs(args));
    }

    // Scrubs the configured API token (both raw and URL-escaped forms) from any string
    // argument before it reaches the logger, so it can never leak through URLs, parsed
    // exception messages, or echoed response content.
    private object[] RedactArgs(object[] args)
    {
        if (args is null || args.Length == 0)
            return args;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is string text
                && (text.Contains(_redactToken) || text.Contains(_redactTokenEscaped)))
            {
                args[i] = text.Replace(_redactToken, "***").Replace(_redactTokenEscaped, "***");
            }
        }

        return args;
    }

    private async Task<ErrorOr<Models.UserSearchResult>> LoginAsUser(CancellationToken cancellationToken)
    {
        var res = await MakeQueryAsync(_userLoginReq, true, cancellationToken).ConfigureAwait(false);
        if (!res.IsOk())
            return ErrorOr<Models.UserSearchResult>.FromError(res.Error);

        var val = res.Get();
        if (val.Session is null || val.User is null ||
            string.IsNullOrEmpty(val.Session.Id) || string.IsNullOrEmpty(val.Session.Name))
        {
            return ErrorOr<Models.UserSearchResult>.FromError(
                Error.FromMessage(ErrorType.Authentication, 401,
                    "The API returned an invalid login response."));
        }

        _session = new UserSession(val.Session.Id, val.Session.Name, val.Hash, val.User,
            _clock(), _sessionLifetime);

        // Only the internally managed CookieContainer feeds the HTTP pipeline; with an
        // external HttpClient/handler the session cookie is attached per-request instead.
        if (!_executor.UsesExternalTransport)
        {
            _executor.Cookies.Add(_baseUri, new Cookie() { Name = "name", Value = _session.Name, Expires = _session.Expires });
            _executor.Cookies.Add(_baseUri, new Cookie() { Name = "id", Value = _session.Id, Expires = _session.Expires });
        }

        Log(LogLevel.Information, "Logged in as {user} | {id}", val.User.Name, val.User.Id);
        return ErrorOr<Models.UserSearchResult>.From(val.User);
    }

    private async Task<Error> CheckSession(CancellationToken cancellationToken)
    {
        if (_userLoginReq is null)
            return null!;

        if (_session is not null && _session.IsValid(_clock()))
            return null!;

        var result = await LoginAsUserAsync(cancellationToken).ConfigureAwait(false);
        return result.IsOk() ? null! : result.Error;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _executor.Dispose();
        _loginLock.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RequestManager));
    }

    private static void ValidateAuth(Credentials.Auth auth)
    {
        if (string.IsNullOrWhiteSpace(auth.Token))
            throw new ArgumentException("The API token is required.", nameof(auth));
        if (string.IsNullOrWhiteSpace(auth.UserAgent))
            throw new ArgumentException("The user agent is required.", nameof(auth));

        if (auth is Credentials.UserAuth userAuth &&
            (string.IsNullOrWhiteSpace(userAuth.Nickname) || string.IsNullOrWhiteSpace(userAuth.Password)))
        {
            throw new ArgumentException("Both username and password are required for user authentication.", nameof(auth));
        }
    }
}