using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
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
    private readonly bool _legacy;
    private readonly TimeSpan _sessionLifetime;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private UserSession _session;
    private readonly Queries.User.Login _userLoginReq;
    private readonly ILogger<Shinden> _logger;
    private readonly ShindenUrlResolver _urlResolver;
    private readonly HttpRequestExecutor _executor;

    private int _disposed;


    internal RequestManager(Uri baseUri, Credentials.Auth auth, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!,
        ShindenUrlResolver urlResolver = default!,
        TimeSpan requestTimeout = default, int maxRetries = 0,
        TimeSpan retryDelay = default, bool legacy = false,
        TimeSpan sessionLifetime = default, Func<DateTimeOffset> clock = null)
    {
        if (baseUri is null)
            throw new ArgumentNullException(nameof(baseUri));
        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(baseUri));
        if (auth is null)
            throw new ArgumentNullException(nameof(auth));
        ValidateAuth(auth);
        _auth = auth;
        _legacy = legacy;
        _sessionLifetime = sessionLifetime > TimeSpan.Zero ? sessionLifetime : Config.DefaultSessionLifetime;
        _clock = clock ?? Config.DefaultClock;
        if (legacy && auth is Credentials.UserAuth userAuth)
            _userLoginReq = new Queries.User.Login(userAuth);
        _logger = logger;
        _baseUri = legacy ? Queries.LegacyRequest<object>.NormalizeBaseUri(baseUri) : baseUri;
        _urlResolver = urlResolver ?? new ShindenUrlResolver(baseUri);
        _executor = new HttpRequestExecutor(handler, client,
            requestTimeout > TimeSpan.Zero ? requestTimeout : Config.DefaultRequestTimeout,
            maxRetries, retryDelay, auth, legacy);

    }

    internal Task<ErrorOr<TReturn>> MakeQueryAsync<TReturn>(Queries.Request<TReturn> request,
        CancellationToken cancellationToken = default) where TReturn : class
        => MakeQueryCoreAsync(request, false, cancellationToken);

    private async Task<ErrorOr<TReturn>> MakeQueryCoreAsync<TReturn>(Queries.Request<TReturn> request,
        bool skipSession, CancellationToken cancellationToken) where TReturn : class
    {
        ThrowIfDisposed();
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (_legacy && string.IsNullOrWhiteSpace(_auth.Token))
        {
            Log(LogLevel.Error, "[{query}]: the PHP API key is not configured; set WithLegacyAuth.", request.GetType().FullName);
            return ErrorOr<TReturn>.FromError(Error.FromMessage(ErrorType.Configuration, 401,
                "The PHP API key is not configured. Set WithLegacyAuth separately from the new API token."));
        }
        if (_legacy && !skipSession && _userLoginReq is not null)
        {
            var login = await LoginAsUserAsync(cancellationToken).ConfigureAwait(false);
            if (!login.IsOk()) return ErrorOr<TReturn>.FromError(login.Error);
        }

        HttpRequestMessage body;
        try
        {
            body = request.Build(_baseUri, _auth.Token);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, ex, "[build]: {query} | request construction failed | {msg}", request.GetType().FullName, ex.Message);
            return ErrorOr<TReturn>.FromException(ex, ErrorType.Configuration);
        }

        var requestStart = StartStopwatch();
        try
        {
            Log(LogLevel.Trace, "[{method}]: {uri} | starting...", body.Method, body.RequestUri.LocalPath);

            if (request.RequireHttps() && body.RequestUri.Scheme != Uri.UriSchemeHttps)
                body.RequestUri = new UriBuilder(body.RequestUri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
            if (_legacy && !skipSession && _session is not null)
            {
                var cookie = _session.GetCookieHeader(body.RequestUri);
                if (!string.IsNullOrEmpty(cookie)) body.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            var response = await _executor.SendAsync(body, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log(LogLevel.Error, "[{method}]: {uri} | {code} | {time} ms | {error}", body.Method, body.RequestUri.LocalPath,
                    (int)response.StatusCode, GetElapsedMilliseconds(requestStart), response.Content);

                return ErrorOr<TReturn>.FromHttpResponse(response.StatusCode, response.Content, response.RetryAfterSeconds);
            }

            Log(LogLevel.Information, "[{method}]: {uri} | {code} | {time} ms", body.Method, body.RequestUri.LocalPath,
                (int)response.StatusCode, GetElapsedMilliseconds(requestStart));

            Log(LogLevel.Trace, "[{method}]: {uri} | parsing...", body.Method, body.RequestUri.LocalPath);

            var parseStart = StartStopwatch();
            TReturn obj;
            try
            {
                obj = request.Parse(response.Content);
                if (obj is null) throw new InvalidOperationException("The API returned an empty response.");
            }
            catch (Exception ex)
            {
                var error = Error.FromException(ex, ErrorType.Parsing);
                Log(LogLevel.Error, ex,
                    "[{method}]: {uri} | HTTP {code} | parsing failed | query: {query} | model: {model} | JSON path: {jsonPath} | line: {lineNumber} | position: {linePosition} | {time} ms | {msg}",
                    body.Method, body.RequestUri.LocalPath, (int)response.StatusCode, request.GetType().FullName,
                    typeof(TReturn).FullName, error.JsonPath, error.JsonLineNumber, error.JsonLinePosition, GetElapsedMilliseconds(parseStart), ex.Message);
                return ErrorOr<TReturn>.FromError(error);
            }

            ShindenUrlResolver.Apply(obj, _urlResolver);
            if (_legacy && skipSession && obj is Models.LoginResult login)
                login.SetCookieHeaders = response.SetCookieHeaders;

            Log(LogLevel.Trace, "[{method}]: {uri} | parsed | {time} ms", body.Method, body.RequestUri.LocalPath, GetElapsedMilliseconds(parseStart));

            return ErrorOr<TReturn>.From(obj);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            Log(LogLevel.Warning, ex, "[{method}]: {uri} | request timed out | {time} ms | {msg}",
                body.Method, body.RequestUri.LocalPath, GetElapsedMilliseconds(requestStart), ex.Message);
            return ErrorOr<TReturn>.FromException(ex, ErrorType.Timeout, 408);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, ex, "[{method}]: {uri} | request failed | {time} ms | {msg}",
                body.Method, body.RequestUri.LocalPath, GetElapsedMilliseconds(requestStart), ex.Message);
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
        if (_logger is null || !_logger.IsEnabled(level))
            return;

        // Exception messages can also reflect request headers or response bodies.
        var safeException = new LoggedException(ex, value => (string)RedactArgs(new object[] { value })[0]);
        _logger.Log(level, safeException, message + " | exception: {exceptionType}",
            RedactArgs(args.Concat(new object[] { ex.GetType().FullName }).ToArray()));
    }

    private void Log(LogLevel level, string message, params object[] args)
    {
        if (_logger is null || !_logger.IsEnabled(level))
            return;

        _logger.Log(level, message, RedactArgs(args));
    }

    // Scrubs the configured API token (both raw and URL-escaped forms) from any string
    // argument before it reaches the logger, so it can never leak through URLs, parsed
    // exception messages, or echoed response content.
    private object[] RedactArgs(object[] args)
    {
        if (args is null) return args;
        var secrets = new List<string> { _auth.Token, (_auth as Credentials.UserAuth)?.Password,
            _session?.Id, _session?.Name, _session?.Hash };
        if (_session is not null) secrets.AddRange(_session.CookieSecrets);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is not string value) continue;
            foreach (var secret in secrets)
            {
                if (string.IsNullOrEmpty(secret)) continue;
                value = value.Replace(secret, "***").Replace(Uri.EscapeDataString(secret), "***");
            }
            args[i] = value;
        }
        return args;
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
            Log(LogLevel.Error, "[login]: user credentials are not configured.");
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

    private async Task<ErrorOr<Models.UserSearchResult>> LoginAsUser(CancellationToken cancellationToken)
    {
        var res = await MakeQueryCoreAsync(_userLoginReq, true, cancellationToken).ConfigureAwait(false);
        if (!res.IsOk())
            return ErrorOr<Models.UserSearchResult>.FromError(res.Error);

        var val = res.Get();
        if (val.Session is null || val.User is null ||
            string.IsNullOrEmpty(val.Session.Id) || string.IsNullOrEmpty(val.Session.Name))
        {
            Log(LogLevel.Error, "[login]: the API returned an invalid login response.");
            return ErrorOr<Models.UserSearchResult>.FromError(
                Error.FromMessage(ErrorType.Authentication, 401,
                    "The API returned an invalid login response."));
        }

        CookieContainer cookies = null;
        if (val.SetCookieHeaders.Count > 0)
        {
            cookies = new CookieContainer();
            using var loginRequest = _userLoginReq.Build(_baseUri, _auth.Token);
            var loginUri = loginRequest.RequestUri.Scheme == Uri.UriSchemeHttps ? loginRequest.RequestUri
                : new UriBuilder(loginRequest.RequestUri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
            try
            {
                foreach (var header in val.SetCookieHeaders) cookies.SetCookies(loginUri, header);
            }
            catch (CookieException)
            {
                // CookieException can contain new session secrets that are not yet known to the redactor.
                Log(LogLevel.Error, "[login]: the response contains invalid session cookies.");
                return ErrorOr<Models.UserSearchResult>.FromError(Error.FromMessage(ErrorType.Parsing, 500,
                    "The login response contains invalid session cookies."));
            }
        }
        var cookieSecrets = val.SetCookieHeaders.Select(header => header.Split(';')[0].Split(new[] { '=' }, 2))
            .Where(parts => parts.Length == 2).Select(parts => parts[1].Trim().Trim('"')).ToArray();
        _session = new UserSession(val.Session.Id, val.Session.Name, val.Hash, val.User,
            _clock(), _sessionLifetime, cookies, cookieSecrets);

        ShindenUrlResolver.Apply(val.User, _urlResolver);

        Log(LogLevel.Information, "Logged in as {user} | {id}", val.User.Name, val.User.Id);
        return ErrorOr<Models.UserSearchResult>.From(val.User);
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
        if (auth is Credentials.UserAuth userAuth &&
            (string.IsNullOrWhiteSpace(userAuth.Nickname) || string.IsNullOrWhiteSpace(userAuth.Password)))
            throw new ArgumentException("Both username and password are required for user authentication.", nameof(auth));
        if (string.IsNullOrWhiteSpace(auth.UserAgent))
            throw new ArgumentException("The user agent is required.", nameof(auth));
    }
}
