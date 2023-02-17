using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShindenAPI;

internal class RequestManager
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly Credentials.Auth _auth;
    private readonly CookieContainer _cookies;
    private readonly ILogger<Shinden> _logger;
    private readonly HttpClientHandler _handler;

    private UserSession _session;
    private Queries.LoginUser _userLoginReq;

    internal RequestManager(Uri baseUri, Credentials.Auth auth, ILogger<Shinden> logger = default!)
    {
        _auth = auth;
        _session = null;
        _logger = logger;
        _baseUri = baseUri;
        _userLoginReq = null;

        _cookies = new CookieContainer();
        _handler = new HttpClientHandler { CookieContainer = _cookies };
        _client = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(30) };

        _client.DefaultRequestHeaders.Add("Accept-Language", "pl");
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        _client.DefaultRequestHeaders.Add("User-Agent", _auth.UserAgent
            + $" (Shinden.NET/{Assembly.GetAssembly(typeof(RequestManager)).GetName().Version})");

        if (!string.IsNullOrEmpty(_auth.Marmolade))
        {
            _client.DefaultRequestHeaders.Add(_auth.Marmolade, "marmolada");
        }

        if (_auth is Credentials.UserAuth uAuth)
        {
            _userLoginReq = new Queries.LoginUser(uAuth);
        }
    }

    internal bool IsValidUserSession()
    {
        if (_userLoginReq is null)
            return false;

        return _session is not null && _session.IsValid();
    }

    internal async Task<ErrorOr<TReturn>> MakeQueryAsync<TReturn>(Queries.Request<TReturn> request, bool skipSession = false)
        where TReturn : class
    {
        if (!skipSession)
        {
            await CheckSession().ConfigureAwait(false);
        }

        var body = request.Build(_baseUri, _auth.Token);
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

        try
        {
            StartStopwatch();
            var response = await _client.SendAsync(body).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log(LogLevel.Error, "[{method}]: {uri} | {code} | {time} ms | {error}", body.Method, body.RequestUri.LocalPath,
                    (int)response.StatusCode, GetElapsedMilliseconds(), await response.Content.ReadAsStringAsync().ConfigureAwait(false));

                return await ErrorOr<TReturn>.FromHttpResponseMessage(response).ConfigureAwait(false);
            }

            Log(LogLevel.Information, "[{method}]: {uri} | {code} | {time} ms", body.Method, body.RequestUri.LocalPath,
                (int)response.StatusCode, GetElapsedMilliseconds());

            Log(LogLevel.Trace, "[{method}]: {uri} | parsing...", body.Method, body.RequestUri.LocalPath);

            StartStopwatch();
            var obj = request.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

            Log(LogLevel.Trace, "[{method}]: {uri} | parsed | {time} ms", body.Method, body.RequestUri.LocalPath, GetElapsedMilliseconds());

            return ErrorOr<TReturn>.From(obj);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Critical, ex, "[{method}]: {uri} | {msg}", body.Method, body.RequestUri.LocalPath, ex.Message);
            return ErrorOr<TReturn>.FromException(ex);
        }
    }

    private void StartStopwatch()
    {
#if NET7_0_OR_GREATER
        _startTime = Stopwatch.GetTimestamp();
#else
        _stopWatch = Stopwatch.StartNew();
#endif
    }

    private long GetElapsedMilliseconds()
    {
#if NET7_0_OR_GREATER
        return Stopwatch.GetElapsedTime(_startTime).Milliseconds;
#else
        return _stopWatch.ElapsedMilliseconds;
#endif
    }

    private void Log(LogLevel level, Exception ex, string message, params object[] args)
    {
        if (_logger is not null)
            _logger.Log(level, ex, message, args);
    }

    private void Log(LogLevel level, string message, params object[] args)
    {
        if (_logger is not null)
            _logger.Log(level, message, args);
    }

    private async Task LoginAsUser()
    {
        var res = await MakeQueryAsync(_userLoginReq, true).ConfigureAwait(false);
        if (res.IsOk())
        {
            var val = res.Get();
            _session = new UserSession(val.Session.Id, val.Session.Name, val.Hash);
            _cookies.Add(_baseUri, new Cookie() { Name = "name", Value = _session.Name, Expires = _session.Expires });
            _cookies.Add(_baseUri, new Cookie() { Name = "id", Value = _session.Id, Expires = _session.Expires });
            Log(LogLevel.Information, "Logged in as {user} | {id}", val.User.Name, val.User.Id);
        }
    }

    private async Task CheckSession()
    {
        if (_userLoginReq is null)
            return;

        if (_session is null || !_session.IsValid())
        {
            await LoginAsUser().ConfigureAwait(false);
        }
    }

#if NET7_0_OR_GREATER
    private long _startTime = 0;
#else
    private Stopwatch _stopWatch = Stopwatch.StartNew();
#endif
}