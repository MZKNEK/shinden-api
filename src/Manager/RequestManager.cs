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
    /// <summary>
    /// Everything that can change through <see cref="Shinden.Reconfigure(Config)"/>. Immutable: each request
    /// reads it once, so a request that is already running finishes with the settings it started with.
    /// </summary>
    internal sealed record State(Uri BaseUri, Credentials.Auth Auth, Queries.User.Login UserLoginReq,
        ShindenUrlResolver UrlResolver, TimeSpan SessionLifetime, Func<DateTimeOffset> Clock, ExecutorSettings Executor,
        RateLimitOptions RateLimit)
    {
        /// <summary>Changes whenever the credentials or address change; older PHP sessions are then ignored.</summary>
        internal int CredentialsVersion { get; init; }
        /// <summary>The 429 pause of these credentials; kept while the credentials and address stay the same.</summary>
        internal RateLimitBlock Block { get; init; }
        /// <summary>Optional client-side limiter; kept while the credentials, address and limit stay the same.</summary>
        internal ClientRateLimiter Limiter { get; init; }
    }

    private readonly bool _legacy;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly object _stateLock = new();
    private volatile State _state;
    private volatile UserSession _session;
    // Secrets of earlier configurations, still redacted from logs of requests that started before a change.
    private volatile string[] _retiredSecrets = Array.Empty<string>();
    private readonly ILogger<Shinden> _logger;
    private readonly HttpRequestExecutor _executor;

    private int _disposed;


    internal RequestManager(Uri baseUri, Credentials.Auth auth, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!,
        ShindenUrlResolver urlResolver = default!,
        TimeSpan requestTimeout = default, int maxRetries = 0,
        TimeSpan retryDelay = default, bool legacy = false,
        TimeSpan sessionLifetime = default, Func<DateTimeOffset> clock = null, RateLimitOptions rateLimit = null)
    {
        _legacy = legacy;
        _logger = logger;
        _state = Prepare(baseUri, auth, urlResolver, requestTimeout, maxRetries, retryDelay, sessionLifetime, clock, rateLimit);
        _executor = new HttpRequestExecutor(handler, client, legacy);
    }

    /// <summary>Validates settings and builds a new state without changing this manager.</summary>
    internal State Prepare(Uri baseUri, Credentials.Auth auth, ShindenUrlResolver urlResolver,
        TimeSpan requestTimeout, int maxRetries, TimeSpan retryDelay,
        TimeSpan sessionLifetime, Func<DateTimeOffset> clock, RateLimitOptions rateLimit = null)
    {
        if (baseUri is null)
            throw new ArgumentNullException(nameof(baseUri));
        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(baseUri));
        if (auth is null)
            throw new ArgumentNullException(nameof(auth));
        ValidateAuth(auth);

        return new State(
            _legacy ? Queries.LegacyRequest<object>.NormalizeBaseUri(baseUri) : baseUri,
            auth,
            _legacy && auth is Credentials.UserAuth userAuth ? new Queries.User.Login(userAuth) : null,
            urlResolver ?? new ShindenUrlResolver(baseUri),
            sessionLifetime > TimeSpan.Zero ? sessionLifetime : Config.DefaultSessionLifetime,
            clock ?? Config.DefaultClock,
            new ExecutorSettings(requestTimeout > TimeSpan.Zero ? requestTimeout : Config.DefaultRequestTimeout,
                maxRetries, retryDelay, auth),
            rateLimit ?? RateLimitOptions.Default)
        {
            Block = new RateLimitBlock(_logger, _legacy ? "PHP" : "new"),
            Limiter = rateLimit is { Requests: > 0 } ? new ClientRateLimiter(rateLimit.Requests, rateLimit.Window) : null
        };
    }

    /// <summary>Switches to a state from <see cref="Prepare"/>. The PHP session is kept only if the credentials did not change.</summary>
    internal void Apply(State next)
    {
        ThrowIfDisposed();
        if (next is null) throw new ArgumentNullException(nameof(next));
        lock (_stateLock)
        {
            var current = _state;
            var sameCredentials = current.BaseUri == next.BaseUri
                && current.Auth.Token == next.Auth.Token
                && (current.Auth as Credentials.UserAuth)?.Nickname == (next.Auth as Credentials.UserAuth)?.Nickname
                && (current.Auth as Credentials.UserAuth)?.Password == (next.Auth as Credentials.UserAuth)?.Password;

            var retired = new List<string>(_retiredSecrets) { current.Auth.Token, (current.Auth as Credentials.UserAuth)?.Password };
            if (!sameCredentials && _session is { } old)
                retired.AddRange(new[] { old.Id, old.Name, old.Hash }.Concat(old.CookieSecrets));
            _retiredSecrets = retired.Where(secret => !string.IsNullOrEmpty(secret)).Distinct().ToArray();

            var sameLimit = sameCredentials && current.RateLimit.Requests == next.RateLimit.Requests
                && current.RateLimit.Window == next.RateLimit.Window;
            _state = next with
            {
                CredentialsVersion = sameCredentials ? current.CredentialsVersion : current.CredentialsVersion + 1,
                // A 429 pause belongs to the credentials that received it; new credentials start unpaused.
                Block = sameCredentials ? current.Block : next.Block,
                Limiter = sameLimit ? current.Limiter : next.Limiter
            };
            if (!sameCredentials)
            {
                _session = null;
                current.Block.Dispose();
            }
            else
                next.Block.Dispose();
        }
    }

    internal Task<ErrorOr<TReturn>> MakeQueryAsync<TReturn>(Queries.Request<TReturn> request,
        CancellationToken cancellationToken = default) where TReturn : class
        => MakeQueryCoreAsync(request, false, cancellationToken, _state);

    private async Task<ErrorOr<TReturn>> MakeQueryCoreAsync<TReturn>(Queries.Request<TReturn> request,
        bool skipSession, CancellationToken cancellationToken, State state) where TReturn : class
    {
        ThrowIfDisposed();
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (_legacy && string.IsNullOrWhiteSpace(state.Auth.Token))
        {
            Log(LogLevel.Error, "[{query}]: the PHP API key is not configured; set WithLegacyAuth.", request.GetType().FullName);
            return ErrorOr<TReturn>.FromError(Error.FromMessage(ErrorType.Configuration, 401,
                "The PHP API key is not configured. Set WithLegacyAuth separately from the new API token."));
        }
        if (_legacy && !skipSession && state.UserLoginReq is not null)
        {
            var login = await LoginAsUserAsync(state, cancellationToken).ConfigureAwait(false);
            if (!login.IsOk()) return ErrorOr<TReturn>.FromError(login.Error);
        }

        HttpRequestMessage body;
        try
        {
            body = request.Build(state.BaseUri, state.Auth.Token);
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
            if (_legacy && !skipSession && CurrentSession(state) is { } session)
            {
                var cookie = session.GetCookieHeader(body.RequestUri);
                if (!string.IsNullOrEmpty(cookie)) body.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            var rateLimit = state.RateLimit;
            var maxWait = rateLimit.MaxWait;
            TransportResponse response;
            for (var rateLimitRetry = 0; ; rateLimitRetry++)
            {
                var refusal = await WaitForCapacityAsync(state, maxWait, cancellationToken).ConfigureAwait(false);
                if (refusal is not null)
                {
                    Log(LogLevel.Debug, "[{method}]: {uri} | not sent | {msg} | retry after {seconds} s", body.Method,
                        body.RequestUri.LocalPath, refusal.Message, refusal.RetryAfterSeconds);
                    return ErrorOr<TReturn>.FromError(refusal);
                }

                response = await _executor.SendAsync(body, state.Executor, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    break;

                Log(LogLevel.Error, "[{method}]: {uri} | {code} | {time} ms | {error}", body.Method, body.RequestUri.LocalPath,
                    (int)response.StatusCode, GetElapsedMilliseconds(requestStart), response.Content);
                var error = Error.FromHttpResponse(response.StatusCode, response.Content, response.RetryAfter);
                if ((int)response.StatusCode != 429)
                    return ErrorOr<TReturn>.FromError(error);

                // Only 429 pauses the client; other statuses with Retry-After (for example 503) do not.
                if (error.RetryAfter is { } pause)
                    state.Block.Pause(pause);
                // Requests that change data are never sent again after 429.
                if (rateLimitRetry >= rateLimit.Retries || !IsSafeMethod(body.Method))
                    return ErrorOr<TReturn>.FromError(error);

                maxWait = rateLimit.MaxWait > TimeSpan.Zero ? rateLimit.MaxWait : state.Executor.Timeout;
                if (state.Block.Remaining > maxWait)
                    return ErrorOr<TReturn>.FromError(error);
                // A 429 without Retry-After does not pause the client; wait the base retry delay instead.
                if (state.Block.Remaining == TimeSpan.Zero && state.Executor.RetryDelay > TimeSpan.Zero)
                    await Task.Delay(state.Executor.RetryDelay, cancellationToken).ConfigureAwait(false);
                Log(LogLevel.Information, "[{method}]: {uri} | retrying after 429 | attempt {attempt}",
                    body.Method, body.RequestUri.LocalPath, rateLimitRetry + 2);
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

            ShindenUrlResolver.Apply(obj, state.UrlResolver);
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

    private static bool IsSafeMethod(HttpMethod method)
        => method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    /// <summary>
    /// Waits, within <paramref name="maxWait"/>, until the 429 pause has passed and a client-side limiter slot is free.
    /// Returns the error to report when that takes longer, or null when the request may be sent.
    /// </summary>
    private static async Task<Error> WaitForCapacityAsync(State state, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var deadline = MonotonicClock.Now + (maxWait < RateLimitOptions.MaxDelay ? maxWait : RateLimitOptions.MaxDelay);
        var refusal = await WaitForPauseAsync(state.Block, deadline, cancellationToken).ConfigureAwait(false);
        if (refusal is not null || state.Limiter is null)
            return refusal;

        var left = deadline - MonotonicClock.Now;
        if (!state.Limiter.TryReserve(left > TimeSpan.Zero ? left : TimeSpan.Zero, out var delay))
            return Error.FromRateLimit(delay,
                $"The client-side limit of {state.RateLimit.Requests} requests per {state.RateLimit.Window} is reached.");
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        // A 429 may have paused the client while this request waited for its slot.
        return await WaitForPauseAsync(state.Block, deadline, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Error> WaitForPauseAsync(RateLimitBlock block, TimeSpan deadline, CancellationToken cancellationToken)
    {
        // The pause can be extended by other requests' 429 responses while this one waits.
        while (block.Remaining is var remaining && remaining > TimeSpan.Zero)
        {
            if (MonotonicClock.Now + remaining > deadline)
                return Error.FromRateLimit(remaining,
                    "Requests are paused after HTTP 429 until the API rate limit resets.");
            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
        }
        return null;
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
        var state = _state;
        var session = _session;
        var secrets = new List<string> { state.Auth.Token, (state.Auth as Credentials.UserAuth)?.Password,
            session?.Id, session?.Name, session?.Hash };
        if (session is not null) secrets.AddRange(session.CookieSecrets);
        secrets.AddRange(_retiredSecrets);
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

    // A session belongs to the credentials it was created with; after they change it is ignored.
    private UserSession CurrentSession(State state)
        => _session is { } session && session.CredentialsVersion == state.CredentialsVersion ? session : null;

    private UserSession ValidSession(State state)
        => CurrentSession(state) is { } session && session.IsValid(state.Clock()) ? session : null;

    internal bool IsValidUserSession()
    {
        ThrowIfDisposed();
        var state = _state;
        return state.UserLoginReq is not null && ValidSession(state) is not null;
    }

    internal async Task<Models.UserSearchResult> ForceLoginAsync(CancellationToken cancellationToken = default)
        => (await LoginAsUserAsync(cancellationToken).ConfigureAwait(false)).Get();

    internal Task<ErrorOr<Models.UserSearchResult>> LoginAsUserAsync(
        CancellationToken cancellationToken = default)
        => LoginAsUserAsync(_state, cancellationToken);

    private async Task<ErrorOr<Models.UserSearchResult>> LoginAsUserAsync(
        State state, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (state.UserLoginReq is null)
        {
            Log(LogLevel.Error, "[login]: user credentials are not configured.");
            return ErrorOr<Models.UserSearchResult>.FromError(
                Error.FromMessage(ErrorType.Authentication, 401,
                    "User credentials are not configured."));
        }

        if (ValidSession(state) is { } valid)
            return ErrorOr<Models.UserSearchResult>.From(valid.User);

        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ValidSession(state) is { } current)
                return ErrorOr<Models.UserSearchResult>.From(current.User);

            return await LoginAsUser(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<ErrorOr<Models.UserSearchResult>> LoginAsUser(State state, CancellationToken cancellationToken)
    {
        var res = await MakeQueryCoreAsync(state.UserLoginReq, true, cancellationToken, state).ConfigureAwait(false);
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
            using var loginRequest = state.UserLoginReq.Build(state.BaseUri, state.Auth.Token);
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
            state.Clock(), state.SessionLifetime, cookies, cookieSecrets, state.CredentialsVersion);

        ShindenUrlResolver.Apply(val.User, state.UrlResolver);

        Log(LogLevel.Information, "Logged in as {user} | {id}", val.User.Name, val.User.Id);
        return ErrorOr<Models.UserSearchResult>.From(val.User);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _executor.Dispose();
        _loginLock.Dispose();
        _state.Block.Dispose();
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
