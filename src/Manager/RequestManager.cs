using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace ShindenAPI;

internal class RequestManager
{
    private readonly Auth _auth;
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly CookieContainer _cookies;
    private readonly HttpClientHandler _handler;

    private UserSession _session;
    private Queries.Request<object> _userLoginReq;

    //TODO: add logger
    internal RequestManager(Uri baseUri, Auth auth)
    {
        _auth = auth;
        _session = null;
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

        if (_auth is UserAuth uAuth)
        {
            //TODO: create _userLoginReq request
        }
    }

    internal async Task<ErrorOr<TReturn>> MakeQueryAsync<TReturn>(Queries.Request<TReturn> request, bool skipSession = false)
        where TReturn : class
    {
        if (!skipSession)
        {
            await CheckSession().ConfigureAwait(false);
        }

        try
        {
            var body = request.Build(_baseUri, _auth.Token);
            var response = await _client.SendAsync(body).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return await ErrorOr<TReturn>.FromHttpResponseMessage(response).ConfigureAwait(false);

            var obj = request.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            return ErrorOr<TReturn>.From(obj);
        }
        catch (Exception ex)
        {
            //TODO: log ex
            return ErrorOr<TReturn>.FromException(ex);
        }
    }

    private async Task LoginAsUser()
    {
        var res = await MakeQueryAsync(_userLoginReq, true).ConfigureAwait(false);
        //TODO: create session from response
        AddSessionCookies();
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

    private void AddSessionCookies()
    {
        if (_session is null || !_session.IsValid())
            return;

        _cookies.Add(_baseUri, new Cookie() { Name = "name", Value = _session.Name, Expires = _session.Expires });
        _cookies.Add(_baseUri, new Cookie() { Name = "id", Value = _session.Id, Expires = _session.Expires });
    }
}